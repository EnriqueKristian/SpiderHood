// Services/CuotaService.cs
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Collections.Generic;
using System.Globalization;


namespace SpiderHood.Services
{
    public class CuotaService : ICuotaService
    {
        private readonly ILogger<CuotaService> _logger;
        [Inject]
        private ParameterService ParameterService { get; set; } = default!;

        public CuotaService(ILogger<CuotaService> logger)
        {
            _logger = logger;
        }

        public async Task<ResultadoGeneracion> GenerarCuotaMensualAsync(
            int mes,
            int anio,
            DateTime fechaVencimiento,
            List<Guid> gastosIds)
        {
            var resultado = new ResultadoGeneracion();
            /*
            try
            {
                // Validar que no exista cuota para el mismo periodo
                //var existeCuota = await _context.CuotasMensuales
                //var cuotasMensuales = await ec.GetCuotasMensuales(ParameterService.IdBuilding);
                // If cuotasMensuales is a single CuotaViewModel, check its properties directly
                // If it's a collection, use .Any(...) as below
                

                bool existeCuota = false;
                if (cuotasMensuales is IEnumerable<CuotaViewModel> collection)
                {
                    existeCuota = collection.Any(c => c.Mes == mes && c.Anio == anio && !c.Procesada);
                }
                else if (cuotasMensuales != null)
                {
                    existeCuota = cuotasMensuales.Mes == mes && cuotasMensuales.Anio == anio && !cuotasMensuales.Procesada;
                }
                
                var existeCuota = await _context.CuotasMensuales
                .AnyAsync(c => c.Mes == mes && c.Anio == anio && !c.Procesada);

                if (existeCuota)
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "Ya existe una cuota pendiente para este periodo";
                    return resultado;
                }

                // Obtener departamentos activos
                /*var departamentos = await _context.Departamentos
                    .Where(d => d.Activo)
                    .ToListAsync();

                var departamentos = new List<Departamento>(); //await ParameterService.ec.GetDptos();

                if (!departamentos.Any())
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No hay departamentos activos para distribuir";
                    return resultado;
                }

                var totalArea = departamentos.Sum(d => d.AreaM2);

                // Calcular porcentajes
                foreach (var depto in departamentos)
                {
                    depto.PorcentajeArea = totalArea > 0 ? (depto.AreaM2 / totalArea) * 100 : 0;
                }

                // Obtener gastos seleccionados
                var gastos = await _context.ViewExpense
                    .Include(g => g.Category)
                    .Where(g => gastosIds.Contains(g.IdExpense) &&
                               g.Status != StatusExpense.Paid &&
                               !g.IncludeInQuota)
                    .ToListAsync();

                if (!gastos.Any())
                {
                    resultado.Exito = false;
                    resultado.Mensaje = "No hay gastos válidos para incluir en la cuota";
                    return resultado;
                }

                // Crear cuota
                var cuota = new CuotaMensual
                {
                    Mes = mes,
                    Anio = anio,
                    FechaGeneracion = DateTime.Now,
                    TotalDistribuido = gastos.Sum(g => g.Amount),
                    UsuarioGeneracion = "UsuarioActual", // En producción, obtener del usuario autenticado
                    Procesada = false
                };

                _context.CuotasMensuales.Add(cuota);
                await _context.SaveChangesAsync();

                // Generar detalles por departamento
                foreach (var gasto in gastos)
                {
                    await GenerarDetallesPorGasto(cuota.Id, gasto, departamentos, fechaVencimiento);
                }

                // Marcar gastos como considerados
                foreach (var gasto in gastos)
                {
                    gasto.IncludeInQuota = true;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation($"Cuota generada exitosamente - ID: {cuota.Id}");

                resultado.Exito = true;
                resultado.CuotaId = cuota.Id;
                resultado.Mensaje = "Cuota generada exitosamente";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar cuota mensual");
                resultado.Exito = false;
                resultado.Mensaje = $"Error al generar cuota: {ex.Message}";
            }
            */
            return resultado;
        }

        private async Task GenerarDetallesPorGasto(
            int cuotaId,
            ViewExpense gasto,
            List<Departamento> departamentos,
            DateTime fechaVencimiento)
        {
            /*if (gasto.Category == TipoDistribucion.Fija.ToString())
            {
                // Distribución igualitaria
                var montoPorDepto = Math.Round(gasto.Amount / departamentos.Count, 2);

                // Ajuste por redondeo
                var totalDistribuido = montoPorDepto * departamentos.Count;
                var diferencia = gasto.Amount - totalDistribuido;

                var detalles = new List<DetalleCuota>();

                for (int i = 0; i < departamentos.Count; i++)
                {
                    var montoFinal = montoPorDepto;

                    // Ajustar el último departamento si hay diferencia por redondeo
                    if (i == departamentos.Count - 1)
                    {
                        montoFinal += diferencia;
                    }

                    var detalle = new DetalleCuota
                    {
                        CuotaId = cuotaId,
                        DepartamentoId = departamentos[i].Id,
                        GastoId = gasto.IdExpense,
                        Monto = montoFinal,
                        FechaVencimiento = fechaVencimiento,
                        Pagado = false
                    };

                    detalles.Add(detalle);
                }

                await _context.DetallesCuota.AddRangeAsync(detalles);
            }
            else // Porcentual
            {
                // Distribución por porcentaje de área
                var detalles = new List<DetalleCuota>();
                decimal totalDistribuido = 0;

                for (int i = 0; i < departamentos.Count; i++)
                {
                    var montoDepto = Math.Round(gasto.Amount * (departamentos[i].PorcentajeArea / 100), 2);

                    // Ajustar el último departamento para que coincida el total exacto
                    if (i == departamentos.Count - 1)
                    {
                        montoDepto = gasto.Amount - totalDistribuido;
                    }
                    else
                    {
                        totalDistribuido += montoDepto;
                    }

                    var detalle = new DetalleCuota
                    {
                        CuotaId = cuotaId,
                        DepartamentoId = departamentos[i].Id,
                        GastoId = gasto.IdExpense,
                        Monto = montoDepto,
                        FechaVencimiento = fechaVencimiento,
                        Pagado = false
                    };

                    detalles.Add(detalle);
                }

                await _context.DetallesCuota.AddRangeAsync(detalles);
            }

            await _context.SaveChangesAsync();*/
        }

        public async Task<CuotaMensual> ObtenerCuotaAsync(int cuotaId)
        {
            var cuotas = new List<CuotaMensual>(); // await ParameterService.ec.ObtenerCuotaMensuales();
            return cuotas.FirstOrDefault(c => c.Id == cuotaId)!;
        }

        public async Task<List<CuotaMensual>> ObtenerCuotasAsync(int? anio = null, int? mes = null)
        {
            /*var query = _context.CuotasMensuales.AsQueryable();

            if (anio.HasValue && anio > 0)
            {
                query = query.Where(c => c.Anio == anio.Value);
            }

            if (mes.HasValue && mes > 0)
            {
                query = query.Where(c => c.Mes == mes.Value);
            }*/

            return new List<CuotaMensual>(); // await ParameterService.ec.ObtenerCuotaMensuales();

            /*return await query
                .OrderByDescending(c => c.Anio)
                .ThenByDescending(c => c.Mes)
                .ToListAsync();*/
        }

        public async Task<List<DetalleCuotaViewModel>> ObtenerDetallesCuotaAsync(int cuotaId)
        {
            return new List<DetalleCuotaViewModel>(); // await ParameterService.ec.GetDetallesCuotaByCuotaId(cuotaId);
            /*
            return await _context.DetallesCuota
                .Include(d => d.Departamentos)
                .Include(d => d.Gasto)
                    .ThenInclude(g => g.Categoria)
                .Where(d => d.CuotaId == cuotaId)
                .Select(d => new DetalleCuotaViewModel
                {
                    Id = d.Id,
                    CuotaId = d.CuotaId,
                    DepartamentoId = d.DepartamentoId,
                    DepartamentoNombre = d.Departamento.Nombre,
                    AreaM2 = d.Departamento.AreaM2,
                    PorcentajeArea = d.Departamento.PorcentajeArea,
                    GastoId = d.GastoId,
                    CategoriaNombre = d.Gasto.Categoria.Nombre,
                    DescripcionGasto = d.Gasto.Descripcion,
                    TipoDistribucion = d.Gasto.Categoria.TipoDistribucion,
                    Monto = d.Monto,
                    FechaVencimiento = d.FechaVencimiento,
                    Pagado = d.Pagado
                })
                .ToListAsync();
            */
        }

        public async Task<List<GastoViewModel>> ObtenerGastosIncluidosAsync(int cuotaId)
        {
            // Return an empty list to avoid possible null reference return
            return new List<GastoViewModel>();
            /*
            return await _context.DetallesCuota
                .Where(d => d.CuotaId == cuotaId)
                .Select(d => d.Gasto)
                .Distinct()
                .Select(g => new GastoViewModel
                {
                    Id = g.Id,
                    CategoriaNombre = g.Categoria.Nombre,
                    Descripcion = g.Descripcion,
                    Monto = g.Monto,
                    FechaGasto = g.FechaGasto,
                    FechaVencimiento = g.FechaVencimiento,
                    TipoDistribucion = g.Categoria.TipoDistribucion,
                    Pagado = g.Pagado
                })
                .ToListAsync();*/
        }

        public async Task<List<int>> ObtenerAñosDisponiblesAsync()
        {

            List<int> x =  [];
            x.Add(2022);
            x.Add(2023);
            x.Add(2024);
            return x;

            /*return await _context.CuotasMensuales
                .Select(c => c.Anio)
                .Distinct()
                .OrderByDescending(a => a)
                .ToListAsync();*/
        }

        public async Task<bool> ProcesarCuotaAsync(int cuotaId)
        {
            try
            {
                /*var cuota = await _context.CuotasMensuales
                    .FirstOrDefaultAsync(c => c.Id == cuotaId);

                if (cuota == null || cuota.Procesada)
                    return false;

                cuota.Procesada = true;

                // Marcar gastos como pagados
                var gastosIds = await _context.DetallesCuota
                    .Where(d => d.CuotaId == cuotaId)
                    .Select(d => d.GastoId)
                    .Distinct()
                    .ToListAsync();

                var gastos = await _context.ViewExpense
                    .Where(g => gastosIds.Contains(g.IdExpense))
                    .ToListAsync();

                foreach (var gasto in gastos)
                {
                    gasto.Status = StatusExpense.Paid;
                    gasto.ExpenseDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();*/
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al procesar cuota {cuotaId}");
                return false;
            }
        }

        public async Task<bool> ReversarCuotaAsync(int cuotaId)
        {
            try
            {
                /*var cuota = await _context.CuotasMensuales
                    .FirstOrDefaultAsync(c => c.Id == cuotaId);

                if (cuota == null || cuota.Procesada)
                    return false;

                // Obtener gastos de la cuota
                var gastosIds = await _context.DetallesCuota
                    .Where(d => d.CuotaId == cuotaId)
                    .Select(d => d.GastoId)
                    .Distinct()
                    .ToListAsync();

                var gastos = await _context.ViewExpense
                    .Where(g => gastosIds.Contains(g.IdExpense))
                    .ToListAsync();

                // Reversar estado de gastos
                foreach (var gasto in gastos)
                {
                    gasto.IncludeInQuota = false;
                }

                // Eliminar detalles de la cuota
                var detalles = await _context.DetallesCuota
                    .Where(d => d.CuotaId == cuotaId)
                    .ToListAsync();

                _context.DetallesCuota.RemoveRange(detalles);

                // Eliminar la cuota
                _context.CuotasMensuales.Remove(cuota);

                await _context.SaveChangesAsync();*/
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al reversar cuota {cuotaId}");
                return false;
            }
        }

        public async Task<byte[]> ExportarCuotaPDFAsync(int cuotaId)
        {
            // Implementación básica - usar una librería como QuestPDF o iTextSharp
            throw new NotImplementedException();
        }

        public async Task<byte[]> ExportarCuotaExcelAsync(int cuotaId)
        {
            // Implementación básica - usar una librería como ClosedXML
            throw new NotImplementedException();
        }

        public async Task<ResumenCuota> ObtenerResumenCuotaAsync(int cuotaId)
        {
            var cuota = await ObtenerCuotaAsync(cuotaId);
            var detalles = await ObtenerDetallesCuotaAsync(cuotaId);

            if (cuota == null || !detalles.Any())
                return null!;

            var departamentos = detalles
                .GroupBy(d => d.DepartamentoNombre)
                .Select(g => g.Key)
                .ToList();

            var categorias = detalles
                .GroupBy(d => d.CategoriaNombre)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(d => d.Monto)
                );

            var distribucionDepto = detalles
                .GroupBy(d => d.DepartamentoNombre)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(d => d.Monto)
                );

            var gastosPrincipales = new List<GastoResumen>(); // await ParameterService.ec.GetGastosPrincipales();

            /*var gastosPrincipales = await _context.DetallesCuota
                .Where(d => d.CuotaId == cuotaId)
                .Select(d => d.Gasto)
                .Distinct()
                .Select(g => new GastoResumen
                {
                    Categoria = g.Categoria.Nombre,
                    Descripcion = g.Descripcion,
                    Monto = g.Monto,
                    PorcentajeDelTotal = (g.Monto / cuota.TotalDistribuido) * 100
                })
                .OrderByDescending(g => g.Monto)
                .Take(5)
                .ToListAsync();
            */

            return new ResumenCuota
            {
                CuotaId = cuotaId,
                Periodo = $"{GetNombreMes(cuota.Mes)} {cuota.Anio}",
                TotalDepartamentos = departamentos.Count,
                TotalGastos = gastosPrincipales.Count,
                TotalMonto = cuota.TotalDistribuido,
                PromedioPorDepartamento = cuota.TotalDistribuido / departamentos.Count,
                DistribucionPorCategoria = categorias,
                DistribucionPorDepartamento = distribucionDepto,
                GastosPrincipales = gastosPrincipales
            };
        }

        private string GetNombreMes(int mes)
        {
            return CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes);
        }
    }
}
