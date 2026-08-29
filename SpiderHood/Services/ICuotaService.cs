// Interfaces/ICuotaService.cs
using SpiderHood.Models;
using SpiderHood.Services;
using static SpiderHood.Components.Pages.BudgetPages.GeneracionCuota;

namespace SpiderHood.Services
{
    public interface ICuotaService
    {
        Task<ResultadoGeneracion> GenerarCuotaMensualAsync(
            int mes,
            int anio,
            DateTime fechaVencimiento,
            List<Guid> gastosIds);

        Task<CuotaMensual> ObtenerCuotaAsync(int cuotaId);
        Task<List<CuotaMensual>> ObtenerCuotasAsync(int? anio = null, int? mes = null);
        Task<List<DetalleCuotaViewModel>> ObtenerDetallesCuotaAsync(int cuotaId);
        Task<List<GastoViewModel>> ObtenerGastosIncluidosAsync(int cuotaId);
        Task<List<int>> ObtenerAñosDisponiblesAsync();
        Task<bool> ProcesarCuotaAsync(int cuotaId);
        Task<bool> ReversarCuotaAsync(int cuotaId);
        Task<byte[]> ExportarCuotaPDFAsync(int cuotaId);
        Task<byte[]> ExportarCuotaExcelAsync(int cuotaId);
        Task<ResumenCuota> ObtenerResumenCuotaAsync(int cuotaId);
    }
}