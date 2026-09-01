using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;

namespace SpiderHood.Services.Logging
{
    // Corre 1 vez al día y borra de SystemLog todo lo más viejo que RetentionDays (ver
    // SystemLogSettings, editable en /Settings/SystemLogs) -- así la tabla de logs no
    // crece sin límite aunque el Super Usuario deje el logging prendido.
    public sealed class SystemLogPurgeService(IDbContextFactory<SpiderHoodContext> contextFactory) : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ec = new BDLayout(contextFactory);
                    var settings = await ec.GetSystemLogSettingsAsync(stoppingToken);
                    var retentionDays = settings.RetentionDays > 0 ? settings.RetentionDays : 30;
                    var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);

                    await ec.PurgeSystemLogsAsync(cutoffUtc, stoppingToken);
                }
                catch
                {
                    // No tumbar el host por un fallo de purga (p.ej. BD momentáneamente
                    // inalcanzable) -- se reintenta en el próximo ciclo.
                }

                try
                {
                    await Task.Delay(Interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Apagado normal del host.
                }
            }
        }
    }
}
