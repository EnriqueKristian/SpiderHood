using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Resultado de generar una cuota extraordinaria: cuántas unidades quedaron con
    // cargo y el total repartido, para mostrar una confirmación en la UI.
    public class CuotaExtraordinariaResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdBudgetHeader { get; set; }
        public int UnidadesConCargo { get; set; }
        public decimal TotalRepartido { get; set; }
    }

    // Resultado de correr el proceso de Multas y Mora: qué se generó en esta corrida
    // (no es un acumulado histórico, solo lo que se creó ahora).
    public class AplicacionCargosResultado
    {
        public bool Exito { get; set; } = true;
        public string Mensaje { get; set; } = string.Empty;
        public int CuotasRevisadas { get; set; }
        public int UnidadesConMulta { get; set; }
        public int UnidadesConMora { get; set; }
        public decimal TotalMultas { get; set; }
        public decimal TotalMora { get; set; }
        public List<string> Detalle { get; set; } = [];
    }

    public interface IExtraChargeService
    {
        // Unidades activas (propietarios) del edificio, para armar la grilla de reparto
        // de una cuota extraordinaria (partes iguales o monto manual por unidad).
        Task<List<OwnerUnitView>> GetUnidadesAsync(Guid idBuilding);

        // El presupuesto Ordinario (BudgetType vacío) vigente del edificio — el ciclo
        // mensual normal, distinto de los BudgetHeader "Extraordinario"/"Cargos" que usa
        // este mismo servicio. Null si el edificio no tiene ninguno Activo todavía.
        Task<BudgetHeader?> GetPresupuestoActivoAsync(Guid idBuilding);

        // El periodo marcado como "Actual" en /periods (Period.IsCurrentPeriod) — el
        // ciclo vigente del edificio para aplicar una cuota extraordinaria, sin
        // depender de si el presupuesto Ordinario de ese periodo ya fue publicado
        // (Activo) o todavía está en borrador/revisión. Null si el edificio no tiene
        // ningún periodo marcado como actual.
        Task<Models.Period?> GetPeriodoActivoAsync(Guid idBuilding);

        // Crea un BudgetHeader (BudgetType = "Extraordinario") y una cuota (Installment,
        // Type = Extraordinaria) por cada unidad con monto > 0 en montosPorUnidad. El
        // periodo de la cuota SIEMPRE es el periodo marcado como Actual del edificio —
        // no se puede elegir un periodo distinto ni aplicar retroactivamente (falla si
        // no hay periodo Actual o si fechaVencimiento ya pasó).
        Task<CuotaExtraordinariaResultado> GenerarCuotaExtraordinariaAsync(
            Guid idBuilding,
            string descripcion,
            DateTime fechaVencimiento,
            Dictionary<Guid, decimal> montosPorUnidad,
            string usuario);

        // Cuotas Ordinarias vencidas (Debt > 0, DueDate < hoy) del edificio — para
        // mostrar la previsualización antes de aplicar Multas y Mora.
        Task<List<Installment>> GetCuotasVencidasAsync(Guid idBuilding);

        // Recorre las cuotas Ordinarias vencidas del edificio y genera, bajo un
        // BudgetHeader compartido (BudgetType = "Cargos"): una Multa fija (una sola vez
        // por cuota, la primera vez que se detecta vencida) y una Mora = Deuda x
        // TasaInterésMora% x meses de atraso, cobrando solo el incremento respecto de lo
        // ya generado en corridas anteriores para esa misma cuota (sin duplicar).
        Task<AplicacionCargosResultado> AplicarMultasYMoraAsync(Building building, string usuario);

        // Cuotas Extraordinarias (mismo mes/año que cada cuota Ordinaria) y Multas/Mora
        // (SourceInstallmentId apuntando a esa cuota Ordinaria) asociadas a las cuotas
        // dadas — para mostrarlas como items adicionales en "Ver Detalle" y en el recibo
        // de una cuota Ordinaria, sin mezclarlas con su desglose de BudgetDetail.
        Task<List<Installment>> GetCargosAdicionalesAsync(Guid idBuilding, List<Installment> cuotasOrdinarias);
    }

    public class ExtraChargeService : IExtraChargeService
    {
        private readonly ILogger<ExtraChargeService> _logger;
        private BDLayout ec { get; set; }

        public ExtraChargeService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<ExtraChargeService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<OwnerUnitView>> GetUnidadesAsync(Guid idBuilding)
        {
            var unidades = await ec.GetOwnersByBuildingAsync(idBuilding);
            return unidades.Where(c => c.Role == 1 && c.TypeUnit == 1).OrderBy(u => u.Number).ToList();
        }

        public async Task<BudgetHeader?> GetPresupuestoActivoAsync(Guid idBuilding)
        {
            var presupuestos = await ec.GetBudgetsAsync(idBuilding);
            return presupuestos.FirstOrDefault(b => b.Status == BudgetStatus.Active && string.IsNullOrEmpty(b.BudgetType));
        }

        public async Task<Models.Period?> GetPeriodoActivoAsync(Guid idBuilding)
        {
            var periodos = await ec.GetPeriodsByBuildingAsync(idBuilding);
            return periodos.FirstOrDefault(p => p.IsCurrentPeriod);
        }

        public async Task<CuotaExtraordinariaResultado> GenerarCuotaExtraordinariaAsync(
            Guid idBuilding,
            string descripcion,
            DateTime fechaVencimiento,
            Dictionary<Guid, decimal> montosPorUnidad,
            string usuario)
        {
            var resultado = new CuotaExtraordinariaResultado();

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                resultado.Mensaje = "Debe indicar una descripción para la cuota extraordinaria.";
                return resultado;
            }

            if (montosPorUnidad == null || !montosPorUnidad.Any(m => m.Value > 0))
            {
                resultado.Mensaje = "Debe asignar un monto mayor a cero a al menos una unidad.";
                return resultado;
            }

            if (fechaVencimiento.Date < DateTime.Today)
            {
                resultado.Mensaje = "La fecha de vencimiento no puede ser una fecha pasada.";
                return resultado;
            }

            var periodoActivo = await GetPeriodoActivoAsync(idBuilding);
            if (periodoActivo == null)
            {
                resultado.Mensaje = "No hay un periodo marcado como Actual para este edificio. " +
                    "Configure el periodo vigente en Periodos antes de crear una cuota extraordinaria.";
                return resultado;
            }

            try
            {
                var unidades = await GetUnidadesAsync(idBuilding);

                // El periodo SIEMPRE es el marcado como Actual — la cuota extraordinaria se
                // aplica al ciclo vigente, nunca a uno pasado ni futuro elegido a mano. No
                // depende de que el presupuesto Ordinario de ese periodo ya esté publicado
                // (Activo): son cargos independientes, sólo comparten el mismo ciclo.
                var header = new BudgetHeader
                {
                    IdBudgetHeader = Guid.NewGuid(),
                    BudgetName = descripcion,
                    BudgetDate = periodoActivo.StartDate,
                    BudgetType = "Extraordinario",
                    IdBuilding = idBuilding,
                    IdPeriod = periodoActivo.IdPeriod,
                    Status = BudgetStatus.Active,
                    CreatedBy = usuario,
                    CreatedOn = DateTime.Now
                };

                var cuotas = new List<Installment>();

                foreach (var unidad in unidades)
                {
                    if (!montosPorUnidad.TryGetValue(unidad.IdGroupUnit, out var monto) || monto <= 0)
                        continue;

                    cuotas.Add(new Installment
                    {
                        IdInstallment = Guid.NewGuid(),
                        IdBudgetHeader = header.IdBudgetHeader,
                        Number = unidad.Number,
                        UnitName = unidad.UnitNumber,
                        OwnerName = unidad.FirstName,
                        CreationDate = DateTime.Now,
                        Amount = Math.Round(monto, 2),
                        Percent = 0,
                        TotalArea = unidad.TotalArea,
                        CreatedBy = usuario,
                        Status = ConcilationType.NoConciliada,
                        IdGroupUnit = unidad.IdGroupUnit,
                        DueDate = fechaVencimiento,
                        Type = InstallmentType.Extraordinaria,
                        Concept = descripcion
                    });
                }

                if (!cuotas.Any())
                {
                    resultado.Mensaje = "Ninguna de las unidades con monto asignado es una unidad activa del edificio.";
                    return resultado;
                }

                header.Amount = cuotas.Sum(c => c.Amount);
                header.AnnualAmount = header.Amount;

                await ec.AddNewRecordAsync(header);

                foreach (var cuota in cuotas)
                    await ec.AddNewRecordAsync(cuota);

                resultado.Exito = true;
                resultado.IdBudgetHeader = header.IdBudgetHeader;
                resultado.UnidadesConCargo = cuotas.Count;
                resultado.TotalRepartido = header.Amount;
                resultado.Mensaje = "Cuota extraordinaria generada exitosamente.";

                _logger.LogInformation(
                    "Cuota extraordinaria generada - BudgetHeader: {Id}, Unidades: {Unidades}, Total: {Total}",
                    header.IdBudgetHeader, resultado.UnidadesConCargo, resultado.TotalRepartido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar cuota extraordinaria");
                resultado.Exito = false;
                resultado.Mensaje = $"Error al generar la cuota extraordinaria: {ex.Message}";
            }

            return resultado;
        }

        public async Task<List<Installment>> GetCuotasVencidasAsync(Guid idBuilding)
        {
            var pendientes = await ec.GetPendingInstallmentsAsync(idBuilding);
            return pendientes
                .Where(i => i.Type == InstallmentType.Ordinaria && i.Debt > 0 && i.DueDate.Date < DateTime.Today)
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.UnitName)
                .ToList();
        }

        public async Task<List<Installment>> GetCargosAdicionalesAsync(Guid idBuilding, List<Installment> cuotasOrdinarias)
        {
            var resultado = new List<Installment>();
            if (cuotasOrdinarias == null || !cuotasOrdinarias.Any())
                return resultado;

            var idsOrdinarias = cuotasOrdinarias.Select(c => c.IdInstallment).ToHashSet();
            var idsGroupUnit = cuotasOrdinarias.Select(c => c.IdGroupUnit).ToHashSet();
            var periodos = cuotasOrdinarias.Select(c => (c.Period.Year, c.Period.Month)).ToHashSet();

            var presupuestos = await ec.GetBudgetsAsync(idBuilding);

            // Cuotas Extraordinarias del mismo mes/año que la(s) cuota(s) Ordinaria(s) —
            // puede haber más de una campaña generada para el mismo periodo.
            var extraordinarios = presupuestos
                .Where(b => b.BudgetType == "Extraordinario" && periodos.Contains((b.BudgetDate.Year, b.BudgetDate.Month)))
                .ToList();

            foreach (var header in extraordinarios)
            {
                var installments = await ec.GetInstallmentsByBudgetAsync(header.IdBudgetHeader);
                resultado.AddRange(installments.Where(i => idsGroupUnit.Contains(i.IdGroupUnit)));
            }

            // Multas y Mora generadas específicamente contra estas cuotas Ordinarias.
            var cargosHeader = presupuestos.FirstOrDefault(b => b.BudgetType == "Cargos");
            if (cargosHeader != null)
            {
                var cargos = await ec.GetInstallmentsByBudgetAsync(cargosHeader.IdBudgetHeader);
                resultado.AddRange(cargos.Where(c => idsOrdinarias.Contains(c.SourceInstallmentId)));
            }

            return resultado.OrderBy(i => i.Type).ThenBy(i => i.CreationDate).ToList();
        }

        public async Task<AplicacionCargosResultado> AplicarMultasYMoraAsync(Building building, string usuario)
        {
            var resultado = new AplicacionCargosResultado();

            try
            {
                var vencidas = await GetCuotasVencidasAsync(building.IdBuilding);
                resultado.CuotasRevisadas = vencidas.Count;

                if (!vencidas.Any())
                    return resultado;

                var cargosHeader = await ObtenerOCrearCargosHeaderAsync(building.IdBuilding, usuario);
                var cargosExistentes = await ec.GetInstallmentsByBudgetAsync(cargosHeader.IdBudgetHeader);

                var fineAmount = Math.Round(building.Configuration.FineAmount, 2);
                var lateRate = building.Configuration.LateInterestRate;

                foreach (var cuota in vencidas)
                {
                    var mesesAtraso = MesesAtraso(cuota.DueDate, DateTime.Today);
                    if (mesesAtraso <= 0)
                        continue;

                    // Multa fija: una sola vez por cuota, la primera vez que se detecta vencida.
                    if (fineAmount > 0)
                    {
                        var yaTieneMulta = cargosExistentes.Any(c =>
                            c.SourceInstallmentId == cuota.IdInstallment && c.Type == InstallmentType.Multa);

                        if (!yaTieneMulta)
                        {
                            var multa = NuevoCargo(cuota, cargosHeader.IdBudgetHeader, InstallmentType.Multa, fineAmount,
                                $"Multa por atraso - Cuota {cuota.Period:MMM-yyyy}", usuario);

                            await ec.AddNewRecordAsync(multa);
                            cargosExistentes.Add(multa);

                            resultado.UnidadesConMulta++;
                            resultado.TotalMultas += multa.Amount;
                            resultado.Detalle.Add($"{cuota.UnitName}: Multa S/ {multa.Amount:N2}");
                        }
                    }

                    // Mora = Deuda x TasaInterésMora% x meses de atraso, cobrando solo el
                    // incremento respecto de lo ya generado en corridas anteriores.
                    if (lateRate > 0)
                    {
                        var moraTotalDebida = Math.Round(cuota.Debt * (lateRate / 100m) * mesesAtraso, 2);
                        var moraYaCobrada = cargosExistentes
                            .Where(c => c.SourceInstallmentId == cuota.IdInstallment && c.Type == InstallmentType.Mora)
                            .Sum(c => c.Amount);

                        var delta = Math.Round(moraTotalDebida - moraYaCobrada, 2);

                        if (delta > 0.01m)
                        {
                            var mora = NuevoCargo(cuota, cargosHeader.IdBudgetHeader, InstallmentType.Mora, delta,
                                $"Mora ({mesesAtraso} mes(es) de atraso) - Cuota {cuota.Period:MMM-yyyy}", usuario);

                            await ec.AddNewRecordAsync(mora);
                            cargosExistentes.Add(mora);

                            resultado.UnidadesConMora++;
                            resultado.TotalMora += mora.Amount;
                            resultado.Detalle.Add($"{cuota.UnitName}: Mora S/ {mora.Amount:N2}");
                        }
                    }
                }

                _logger.LogInformation(
                    "Multas y Mora aplicadas - Edificio: {Building}, Multas: {Multas}, Mora: {Mora}",
                    building.IdBuilding, resultado.UnidadesConMulta, resultado.UnidadesConMora);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al aplicar Multas y Mora");
                resultado.Exito = false;
                resultado.Mensaje = $"Error al aplicar Multas y Mora: {ex.Message}";
            }

            return resultado;
        }

        // Todos los cargos de Multas y Mora de un edificio viven bajo un único
        // BudgetHeader "Cargos" (creado la primera vez que se corre el proceso), en vez
        // de uno nuevo por corrida — así GetInstallmentsByBudgetAsync trae de una sola
        // vez el histórico completo (pagado o no) necesario para calcular la mora
        // incremental sin duplicar cargos ya generados.
        private async Task<BudgetHeader> ObtenerOCrearCargosHeaderAsync(Guid idBuilding, string usuario)
        {
            var presupuestos = await ec.GetBudgetsAsync(idBuilding);
            var existente = presupuestos.FirstOrDefault(b => b.BudgetType == "Cargos");
            if (existente != null)
                return existente;

            var header = new BudgetHeader
            {
                IdBudgetHeader = Guid.NewGuid(),
                BudgetName = "Cargos por Multas y Mora",
                BudgetDate = DateTime.Today,
                BudgetType = "Cargos",
                IdBuilding = idBuilding,
                Status = BudgetStatus.Active,
                CreatedBy = usuario,
                CreatedOn = DateTime.Now
            };

            await ec.AddNewRecordAsync(header);
            return header;
        }

        private static Installment NuevoCargo(
            Installment origen, Guid idBudgetHeader, InstallmentType tipo, decimal monto, string concepto, string usuario) => new()
        {
            IdInstallment = Guid.NewGuid(),
            IdBudgetHeader = idBudgetHeader,
            Number = origen.Number,
            UnitName = origen.UnitName,
            OwnerName = origen.OwnerName,
            CreationDate = DateTime.Now,
            Amount = monto,
            Percent = 0,
            TotalArea = origen.TotalArea,
            CreatedBy = usuario,
            Status = ConcilationType.NoConciliada,
            IdGroupUnit = origen.IdGroupUnit,
            DueDate = DateTime.Today,
            Type = tipo,
            Concept = concepto,
            SourceInstallmentId = origen.IdInstallment
        };

        // Meses completos de atraso entre el vencimiento y la fecha de referencia
        // (0 si aún no vence o vence hoy mismo).
        private static int MesesAtraso(DateTime dueDate, DateTime referencia)
        {
            var meses = ((referencia.Year - dueDate.Year) * 12) + referencia.Month - dueDate.Month;
            if (referencia.Day < dueDate.Day)
                meses--;
            return Math.Max(meses, 0);
        }
    }
}
