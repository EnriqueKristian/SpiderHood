using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Services/IPresupuestoService.cs
    public interface IInstallmentService
    {

        //Installments
        Task<List<Models.Installment>> GetInstallmentsByBudgetAsync(Guid IdBudgetHeader);
        Task<List<Models.Installment>> GetPendingInstallmentsAsync(Guid IdBuilding);
        Task<Models.InstallmentPaid> AgregarPagoAsync(InstallmentPaid paid);
        Task<List<Models.InstallmentPaid>> GetInstallmentsPaidAsync(Guid IdBuilding);
    }

    public class InstallmentService : IInstallmentService
    {
        public List<Installment> _Installments { get; set; } = [];
        public Installment _Installment { get; set; } = new();
        public List<InstallmentPaid> _InstallmentPaids { get; set; } = [];
        public InstallmentPaid _InstallmentPaid { get; set; } = new();

        public SpiderHoodContext _context = default!;
        private readonly ILogger<BudgetService> _logger;
        private BDLayout ec { get; set; }

        public InstallmentService(SpiderHoodContext context, ILogger<BudgetService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(context);
        }

        public async Task<List<Installment>> GetInstallmentsByBudgetAsync(Guid IdBudgetHeader)
        {
            try
            {
                return await ec.GetInstallmentsByBudgetAsync(IdBudgetHeader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener los installments por budget: {ex.Message}");
                return new List<Installment>();
            }
            
        }

        public async Task<List<Installment>> GetPendingInstallmentsAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetPendingInstallmentsAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar Pago de Cuota: {ex.Message}");
                return new List<Installment>();
            }
        }

        public async Task<Models.InstallmentPaid> AgregarPagoAsync(InstallmentPaid paid)
        {
            try
            {
                return await ec.AddNewRecordAsync(paid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar Pago de Cuota: {ex.Message}");
                return new Models.InstallmentPaid();
            }
        }

        public async Task<List<InstallmentPaid>> GetInstallmentsPaidAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetInstallmentsPaidAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obtener los pagos de cuotas: {ex.Message}");
                return [];
            }
        }
    }

}



