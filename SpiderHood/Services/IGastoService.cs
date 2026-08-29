// Services/IGastoService.cs
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IGastoService
    {
        Task<List<GastoPendienteViewModel>> ObtenerGastosPendientesAsync();
        Task<ViewExpense> ObtenerGastoPorIdAsync(Guid id);
        Task<List<ViewExpense>> ObtenerGastosPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin);
        Task<ViewExpense> CrearGastoAsync(ViewExpense gasto);
        Task<bool> ActualizarGastoAsync(ViewExpense gasto);
        Task<bool> EliminarGastoAsync(Guid id);
        //Task<List<CategoriaGasto>> ObtenerCategoriasAsync();
    }
}
