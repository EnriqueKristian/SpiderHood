using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IServiceReadingService
    {
        Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period);
        Task AddServiceReadingAsync(Models.ServiceReading newservice);

        Task AddPeriodAsync(Models.Period newperiod);
        Task AddServiceReadingDetailAsync(List<Models.ServiceReadingDetail> newdetails);
    }

    public class ServiceReadingService : IServiceReadingService
    {

        private readonly ILogger<IBudgetService> _logger;
        private readonly AuthService _authService;
        private BDLayout ec { get; set; }

        public ServiceReadingService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<IBudgetService> logger, AuthService authService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            ec = new BDLayout(contextFactory);
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
        }

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period)
        {
            return await ec.GetServiceReadingDetailbyPeriodAsync(period);
        }



        public async Task AddServiceReadingDetailAsync(List<Models.ServiceReadingDetail> newdetails)
        {
            try
            {
                await ec.AddNewRecordAsync(newdetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el detail: {ex.Message}");
            }
        }
        public async Task AddServiceReadingAsync(Models.ServiceReading newservice)
        {
            try
            {
                await ec.AddNewRecordAsync(newservice);
                await ec.StampAuditAsync(AuditableEntity.ServiceReading, newservice.IdServiceReading, await GetPerformedByAsync(), isCreate: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task AddPeriodAsync(Models.Period newperiod)
        {
            try
            {
                await ec.AddNewRecordAsync(newperiod);
                await ec.StampAuditAsync(AuditableEntity.Period, newperiod.IdPeriod, await GetPerformedByAsync(), isCreate: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el periodo: {ex.Message}");
            }
        }

    }
}