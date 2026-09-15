// Services/IPendingExpenseService.cs
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IPendingExpenseService
    {
        Task<List<PendingExpenseViewModel>> GetPendingExpensesAsync();
        Task<ViewExpense> GetExpenseByIdAsync(Guid id);
        Task<List<ViewExpense>> GetExpensesByPeriodAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<ViewExpense> CreateExpenseAsync(ViewExpense gasto);
        Task<bool> UpdateExpenseAsync(ViewExpense gasto);
        Task<bool> DeleteExpenseAsync(Guid id);
        //Task<List<CategoriaGasto>> ObtenerCategoriasAsync();
    }
}