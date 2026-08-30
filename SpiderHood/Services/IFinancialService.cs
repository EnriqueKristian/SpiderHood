using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IFinancialService
    {
        Task<FinancialSummary> GetSummaryAsync(Guid userId);
        Task<List<PaymentHistory>> GetPaymentHistoryAsync(Guid userId, int limit = 10);
        Task<PaymentHistory?> GetNextDuePaymentAsync(Guid userId);
        Task<decimal> GetBalanceAsync(Guid userId);
        Task<decimal> GetOutstandingDebtAsync(Guid userId);
    }

    public class FinancialSummary
    {
        public decimal Balance { get; set; }
        public decimal OutstandingDebt { get; set; }
        public DateTime NextDueDate { get; set; }
        public List<PaymentHistory> RecentPayments { get; set; } = [];
    }

    public class PaymentHistory
    {
        public DateTime Date { get; set; }
        public string Concept { get; set; } = string.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reference { get; set; }
    }

    public class FinancialService : IFinancialService
    {
        private readonly BDLayout _db;
        private readonly ILogger<FinancialService> _logger;

        public FinancialService(BDLayout db, ILogger<FinancialService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<FinancialSummary> GetSummaryAsync(Guid userId)
        {
            try
            {
                var summary = new FinancialSummary
                {
                    Balance = await GetBalanceAsync(userId),
                    OutstandingDebt = await GetOutstandingDebtAsync(userId),
                    NextDueDate = DateTime.Now.AddDays(15), // Placeholder - implementar lógica real
                    RecentPayments = await GetPaymentHistoryAsync(userId, 5)
                };

                return summary;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen financiero para usuario {UserId}", userId);
                return new FinancialSummary();
            }
        }

        public async Task<List<PaymentHistory>> GetPaymentHistoryAsync(Guid userId, int limit = 10)
        {
            try
            {
                // Obtener los edificios del usuario
                var userBuildings = new List<Building>();// await _db.GetUserBuildingAssociationsAsync(userId);
                var buildingIds = userBuildings.Select(ub => ub.IdBuilding).ToList();

                if (!buildingIds.Any())
                    return [];

                // Obtener historial de pagos de esos edificios
                return null;// await _db.GetPaymentHistoryAsync(buildingIds, limit);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de pagos para usuario {UserId}", userId);
                return [];
            }
        }

        public async Task<PaymentHistory?> GetNextDuePaymentAsync(Guid userId)
        {
            try
            {
                var userBuildings = new List<Building>();// await _db.GetUserBuildingAssociationsAsync(userId);
                var buildingIds = userBuildings.Select(ub => ub.IdBuilding).ToList();

                if (!buildingIds.Any())
                    return null;

                return null; // await _db.GetNextDuePaymentAsync(buildingIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener próximo pago para usuario {UserId}", userId);
                return null;
            }
        }

        public async Task<decimal> GetBalanceAsync(Guid userId)
        {
            try
            {
                var history = await GetPaymentHistoryAsync(userId);
                return history.Sum(p => p.Status == "Pagado" ? p.Amount : 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener saldo para usuario {UserId}", userId);
                return 0;
            }
        }

        public async Task<decimal> GetOutstandingDebtAsync(Guid userId)
        {
            try
            {
                var history = await GetPaymentHistoryAsync(userId);
                return history.Sum(p => p.Status == "Pendiente" ? p.Amount : 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener deuda para usuario {UserId}", userId);
                return 0;
            }
        }
    }
}
