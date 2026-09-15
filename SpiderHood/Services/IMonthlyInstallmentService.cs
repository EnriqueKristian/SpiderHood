// Interfaces/IMonthlyInstallmentService.cs
using SpiderHood.Models;
using SpiderHood.Services;
using static SpiderHood.Components.Pages.BudgetPages.MonthlyInstallmentGeneration;

namespace SpiderHood.Services
{
    public interface IMonthlyInstallmentService
    {
        Task<GenerationResult> GenerateMonthlyInstallmentAsync(
            int mes,
            int anio,
            DateTime fechaVencimiento,
            List<Guid> gastosIds);

        Task<MonthlyInstallmentBatch> GetInstallmentBatchAsync(int cuotaId);
        Task<List<MonthlyInstallmentBatch>> GetInstallmentBatchesAsync(int? anio = null, int? mes = null);
        Task<List<InstallmentDetailViewModel>> GetInstallmentDetailsAsync(int cuotaId);
        Task<List<ExpenseViewModel>> GetIncludedExpensesAsync(int cuotaId);
        Task<List<int>> GetAvailableYearsAsync();
        Task<bool> ProcessInstallmentBatchAsync(int cuotaId);
        Task<bool> ReverseInstallmentBatchAsync(int cuotaId);
        Task<byte[]> ExportInstallmentBatchPdfAsync(int cuotaId);
        Task<byte[]> ExportInstallmentBatchExcelAsync(int cuotaId);
        Task<InstallmentSummary> GetInstallmentSummaryAsync(int cuotaId);
    }
}