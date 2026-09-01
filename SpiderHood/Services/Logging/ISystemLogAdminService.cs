using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services.Logging
{
    // Backend de /Settings/SystemLogs (Super Usuario / SysAdmin): leer/guardar la
    // configuración de logging y listar los logs recientes. Separado de
    // DatabaseLoggerProvider (que sólo LEE la config, en caché, para decidir si loguea) --
    // este es el que la EDITA.
    public interface ISystemLogAdminService
    {
        Task<SystemLogSettings> GetSettingsAsync();
        Task SaveSettingsAsync(SystemLogSettings settings, string performedBy);
        Task<List<SystemLogEntry>> GetRecentLogsAsync(int top = 500);
    }

    public class SystemLogAdminService : ISystemLogAdminService
    {
        private readonly BDLayout ec;

        public SystemLogAdminService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<SystemLogSettings> GetSettingsAsync() => await ec.GetSystemLogSettingsAsync();

        public async Task SaveSettingsAsync(SystemLogSettings settings, string performedBy)
        {
            settings.UpdatedBy = performedBy;
            await ec.UpdateSystemLogSettingsAsync(settings);
        }

        public async Task<List<SystemLogEntry>> GetRecentLogsAsync(int top = 500) => await ec.GetRecentSystemLogsAsync(top);
    }
}
