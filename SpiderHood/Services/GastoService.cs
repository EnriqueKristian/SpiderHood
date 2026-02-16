// Services/GastoService.cs
using SpiderHood.Data;
using SpiderHood.Models;
using SpiderHood.Services;
using System.Collections.Generic;
using static SpiderHood.Components.Pages.BudgetPages.GeneracionCuota;

namespace SpiderHood.Services
{
    public class GastoService : IGastoService
    {
        public SpiderHoodContext _context = default!;
        private ParameterService ParameterService { get; set; } = default!;

        public GastoService(SpiderHoodContext context)
        {
            _context = context;
            //ParameterService = new ParameterService(_context);
        }

        public async Task<List<GastoPendienteViewModel>> ObtenerGastosPendientesAsync()
        {
            return new List<GastoPendienteViewModel>(); // await ParameterService.ec.ObtenerGastosPendientes();
            /*return await _context.Gasto
                .Include(g => g.Categoria)
                .Where(g => !g.Pagado && !g.ConsiderarEnCuota)
                .Select(g => new GastoPendienteViewModel
                {
                    Id = g.Id,
                    CategoriaNombre = g.Categoria.Nombre,
                    Descripcion = g.Descripcion,
                    Monto = g.Monto,
                    FechaGasto = g.FechaGasto,
                    FechaVencimiento = g.FechaVencimiento,
                    TipoDistribucion = g.Categoria.TipoDistribucion,
                    Seleccionado = false
                })
                .OrderByDescending(g => g.FechaGasto)
                .ToListAsync();*/
        }

        public async Task<ViewExpense> ObtenerGastoPorIdAsync(Guid id)
        {
            var gastos = new List<ViewExpense>();// await ParameterService.ec.GetGastos();
            return gastos.FirstOrDefault(g => g.IdExpense == id)!;
            /*return await _context.Gasto
                .Include(g => g.Categoria)
                .FirstOrDefaultAsync(g => g.Id == id);*/
        }

        public async Task<List<ViewExpense>> ObtenerGastosPorPeriodoAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            return new List<ViewExpense>();//await ParameterService.ec.GetGastos();
            /*return await _context.Gasto
                .Include(g => g.Categoria)
                .Where(g => g.FechaGasto >= fechaInicio && g.FechaGasto <= fechaFin)
                .OrderByDescending(g => g.FechaGasto)
                .ToListAsync();*/
        }

        public async Task<ViewExpense> CrearGastoAsync(ViewExpense gasto)
        {
            //_context.Gastos.Add(gasto);
            await _context.SaveChangesAsync();
            return gasto;
        }

        public async Task<bool> ActualizarGastoAsync(ViewExpense gasto)
        {
            /*var gastoExistente = await _context.Gastos.FindAsync(gasto.Id);
            if (gastoExistente == null)
                return false;

            _context.Entry(gastoExistente).CurrentValues.SetValues(gasto);*/
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EliminarGastoAsync(Guid id)
        {
            /*var gasto = await _context.Gastos.FindAsync(id);
            if (gasto == null)
                return false;

            _context.Gastos.Remove(gasto);*/
            await _context.SaveChangesAsync();
            return true;
        }

        /*public async Task<List<CategoriaGasto>> ObtenerCategoriasAsync()
        {
            //return new List<CategoriaGasto>();// await ParameterService.ec.GetCategoriasGasto();
            return await _context.CategoriasGasto
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombre)
                .ToListAsync();
        }*/
    }
}