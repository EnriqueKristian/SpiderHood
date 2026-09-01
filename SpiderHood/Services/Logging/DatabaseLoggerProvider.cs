using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

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

        public DatabaseLoggerProvider(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
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

        // Fire-and-forget: ILogger.Log es síncrono y se llama en el hot path de toda la
        // app, así que escribir a BD no puede bloquearlo. Cualquier falla se traga acá
        // mismo -- nunca debe volver a pasar por ILogger (evita loops).
        internal void Enqueue(string category, LogLevel level, string message, Exception? exception)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var ec = new BDLayout(_contextFactory);
                    await ec.AddNewRecordAsync(new SystemLogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = level.ToString(),
                        Category = category,
                        Message = message,
                        Exception = exception?.ToString()
                    });
                }
                catch
                {
                    // Ídem: nunca propagar ni volver a loguear un fallo de logging.
                }
            });
        }

        private static LogLevel ParseLevel(string level) =>
            Enum.TryParse<LogLevel>(level, ignoreCase: true, out var parsed) ? parsed : LogLevel.Error;

        public void Dispose() { }
    }
}
