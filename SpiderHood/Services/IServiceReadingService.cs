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
        private BDLayout ec { get; set; }

        public ServiceReadingService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<IBudgetService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(contextFactory);
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el periodo: {ex.Message}");
            }
        }

    }
}
