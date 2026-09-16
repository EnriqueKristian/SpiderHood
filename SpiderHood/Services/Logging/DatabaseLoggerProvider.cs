using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Threading.Channels;

namespace SpiderHood.Services.Logging
{
    // Sink de logging a BD (tabla SystemLog), configurable en caliente por el Super
    // Usuario desde /Settings/SystemLogs (tabla SystemLogSettings) -- apagado por defecto
    // para no llenar la BD si nadie lo pidió. Se registra como Singleton (ver Program.cs
    // AddSingleton<ILoggerProvider, DatabaseLoggerProvider>), así que usa directamente
    // IDbContextFactory<SpiderHoodContext> (también Singleton) para abrir sus propios
    // DbContext de corta vida, igual que BDLayout.
    //
    // La config se cachea en memoria y se refresca en segundo plano cada
    // SettingsCacheDuration -- IsEnabled() nunca espera a la BD, así que nunca bloquea
    // ni ralentiza el pipeline de logging normal de la app.
    public sealed class DatabaseLoggerProvider : ILoggerProvider
    {
        private readonly IDbContextFactory<SpiderHoodContext> _contextFactory;
        private static readonly TimeSpan SettingsCacheDuration = TimeSpan.FromMinutes(2);

        private volatile bool _cachedIsEnabled;
        private volatile LogLevel _cachedMinLevel = LogLevel.Error;
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private int _refreshInFlight;

        // Docs/Pendientes-Negocio-Consolidado.md -- reportado por el usuario con el
        // logging a BD activado: cambiar de usuario (logout + login) a veces disparaba
        // una carga muy lenta del menú/dashboard (3 de cada 5 veces). Causa: Enqueue()
        // antes abría un DbContext nuevo y hacía un INSERT por cada línea de log, cada
        // uno en su propio Task.Run -- una sola carga de dashboard genera ~20-30 líneas
        // (una por cada "Executed DbCommand" de EF Core más los logs propios de la app),
        // así que dos circuitos solapados (el viejo terminando de cerrar, el nuevo recién
        // arrancando) podían disparar 50+ inserts concurrentes contra la misma tabla al
        // mismo tiempo -- contención real de locks/página en SystemLog y del pool de
        // conexiones, intermitente porque depende de que las dos ráfagas coincidan. Se
        // reemplaza por una cola en memoria con UN solo consumidor en background que
        // escribe secuencialmente -- se sigue guardando cada línea, sin la estampida de
        // conexiones concurrentes. Acotada (no ilimitada) y descarta lo más viejo si se
        // llena, para no crecer sin límite si la BD queda inalcanzable un rato largo --
        // mismo espíritu "best effort" que el resto de esta clase.
        private readonly Channel<SystemLogEntry> _queue = Channel.CreateBounded<SystemLogEntry>(
            new BoundedChannelOptions(1000) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly CancellationTokenSource _cts = new();

        public DatabaseLoggerProvider(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _ = ProcessQueueAsync(_cts.Token);
        }

        public ILogger CreateLogger(string categoryName) => new DatabaseLogger(categoryName, this);

        internal bool IsEnabled(LogLevel logLevel)
        {
            EnsureSettingsFresh();
            return _cachedIsEnabled && logLevel != LogLevel.None && logLevel >= _cachedMinLevel;
        }

        // Kickea un refresh en segundo plano si el cache expiró -- nunca espera el
        // resultado (best effort: si la BD está caída, se queda con el último valor
        // conocido en vez de tumbar el logging o el pipeline que lo dispara).
        private void EnsureSettingsFresh()
        {
            if (DateTime.UtcNow - _lastRefreshUtc < SettingsCacheDuration) return;
            if (Interlocked.CompareExchange(ref _refreshInFlight, 1, 0) != 0) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var ec = new BDLayout(_contextFactory);
                    var settings = await ec.GetSystemLogSettingsAsync();
                    _cachedIsEnabled = settings.IsEnabled;
                    _cachedMinLevel = ParseLevel(settings.MinLevel);
                    _lastRefreshUtc = DateTime.UtcNow;
                }
                catch
                {
                    // Silencioso a propósito: un logger que falla no puede tumbar la app
                    // que lo usa. Se reintenta en el próximo IsEnabled() tras el cache.
                }
                finally
                {
                    Interlocked.Exchange(ref _refreshInFlight, 0);
                }
            });
        }

        // ILogger.Log es síncrono y se llama en el hot path de toda la app -- encolar es
        // instantáneo (TryWrite nunca espera a la BD), el trabajo real lo hace
        // ProcessQueueAsync en background, uno a la vez.
        internal void Enqueue(string category, LogLevel level, string message, Exception? exception)
        {
            _queue.Writer.TryWrite(new SystemLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level.ToString(),
                Category = category,
                Message = message,
                Exception = exception?.ToString()
            });
        }

        // Único consumidor de la cola -- procesa una entrada a la vez, así que nunca hay
        // más de un INSERT a SystemLog en vuelo por esta vía, sin importar cuántas líneas
        // se loguearon de golpe ni cuántos circuitos estén activos al mismo tiempo.
        private async Task ProcessQueueAsync(CancellationToken cancellationToken)
        {
            await foreach (var entry in _queue.Reader.ReadAllAsync(cancellationToken))
            {
                try
                {
                    var ec = new BDLayout(_contextFactory);
                    await ec.AddNewRecordAsync(entry);
                }
                catch
                {
                    // Silencioso a propósito, igual que antes: un fallo de logging no
                    // puede tumbar nada ni volver a pasar por ILogger (evita loops).
                }
            }
        }

        private static LogLevel ParseLevel(string level) =>
            Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsed) ? parsed : LogLevel.Error;

        public void Dispose()
        {
            _queue.Writer.TryComplete();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
