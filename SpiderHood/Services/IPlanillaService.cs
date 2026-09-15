using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Módulo Personal y Planillas -- Fase 2: régimen laboral, parámetros
    // legales, vacaciones y generación de boleta de pago. Depende de
    // IPersonalService para leer Personal/RegistroHoras/asignaciones (Fase 1),
    // no duplica esas consultas acá.
    public interface IPlanillaService
    {
        Task<Models.ConfiguracionRegimenLaboral?> GetRegimenVigenteAsync(Guid idAccount, DateTime? fecha = null);
        Task<List<Models.ConfiguracionRegimenLaboral>> GetHistorialRegimenAsync(Guid idAccount);
        Task<Models.ConfiguracionRegimenLaboral> CrearRegimenAsync(Models.ConfiguracionRegimenLaboral regimen);

        Task<Models.ParametrosLegales?> GetParametrosLegalesAsync(Guid idAccount, int anio);
        Task<Models.ParametrosLegales> CrearParametrosLegalesAsync(Models.ParametrosLegales parametros);
        Task<Models.ParametrosLegales> ActualizarParametrosLegalesAsync(Models.ParametrosLegales parametros);

        Task<List<Models.Vacaciones>> GetVacacionesByPersonalAsync(Guid idPersonal);
        Task<List<Models.Vacaciones>> GetVacacionesPendientesByAccountAsync(Guid idAccount);
        Task<Models.SaldoVacaciones> GetSaldoVacacionesAsync(Guid idPersonal, Guid idAccount, int anio);
        Task<OperationResult> SolicitarVacacionesAsync(Models.Vacaciones vacaciones, string performedBy);
        Task AprobarVacacionesAsync(Guid idVacaciones, Guid idPersonal, Guid idAprobador, string performedBy);
        Task RechazarVacacionesAsync(Guid idVacaciones, Guid idPersonal, Guid idAprobador, string performedBy, string? motivo);

        Task<OperationResult> GenerarBoletaAsync(Guid idPersonal, int anio, int mes, string performedBy);
        Task<(int Generadas, int YaExistian, List<string> Errores)> GenerarBoletasDelMesAsync(Guid idAccount, int anio, int mes, string performedBy);
        Task<List<Models.BoletaPago>> GetBoletasByPersonalAsync(Guid idPersonal);
        Task<List<Models.BoletaPago>> GetBoletasByAccountAndPeriodoAsync(Guid idAccount, int anio, int mes);
        Task<Models.BoletaPago?> GetBoletaConDetalleAsync(Guid idBoletaPago);

        // Fase 3 -- Permisos y licencias
        Task<List<Models.PermisoLicencia>> GetPermisosByPersonalAsync(Guid idPersonal);
        Task<List<Models.PermisoLicencia>> GetPermisosPendientesByAccountAsync(Guid idAccount);
        Task<OperationResult> SolicitarPermisoAsync(Models.PermisoLicencia permiso, string performedBy);
        Task AprobarPermisoAsync(Guid idPermisoLicencia, Guid idPersonal, Guid idAprobador, string performedBy);
        Task RechazarPermisoAsync(Guid idPermisoLicencia, Guid idPersonal, Guid idAprobador, string performedBy, string? motivo);
    }

    public class PlanillaService : IPlanillaService
    {
        private BDLayout ec { get; set; }
        private readonly IPersonalService _personalService;
        private readonly IWorkflowAuditService _workflowAuditService;

        public PlanillaService(IDbContextFactory<SpiderHoodContext> contextFactory, IPersonalService personalService, IWorkflowAuditService workflowAuditService)
        {
            ec = new BDLayout(contextFactory);
            _personalService = personalService ?? throw new ArgumentNullException(nameof(personalService));
            _workflowAuditService = workflowAuditService ?? throw new ArgumentNullException(nameof(workflowAuditService));
        }

        private static string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }

        // WorkflowAuditLog pide un IdBuilding -- Vacaciones/Personal cuelgan de
        // IdAccount, no de un Building específico (un Personal puede rotar entre
        // varios), así que se usa el de su asignación vigente como referencia, o
        // Guid.Empty si no tiene ninguna (mismo fail-open que Building.IdAccount).
        private async Task<Guid> ResolveIdBuildingAsync(Guid idPersonal)
        {
            var asignaciones = await _personalService.GetAsignacionesEdificioByPersonalAsync(idPersonal);
            return asignaciones.FirstOrDefault(a => a.FechaHasta == null)?.IdBuilding ?? Guid.Empty;
        }

        public async Task<Models.ConfiguracionRegimenLaboral?> GetRegimenVigenteAsync(Guid idAccount, DateTime? fecha = null)
            => await ec.GetConfiguracionRegimenLaboralVigenteAsync(idAccount, fecha);

        public async Task<List<Models.ConfiguracionRegimenLaboral>> GetHistorialRegimenAsync(Guid idAccount)
            => await ec.GetConfiguracionRegimenLaboralHistorialAsync(idAccount);

        public async Task<Models.ConfiguracionRegimenLaboral> CrearRegimenAsync(Models.ConfiguracionRegimenLaboral regimen)
        {
            regimen.IdConfiguracionRegimenLaboral = Guid.NewGuid();
            await ec.AddNewRecordAsync(regimen);
            return regimen;
        }

        public async Task<Models.ParametrosLegales?> GetParametrosLegalesAsync(Guid idAccount, int anio)
            => await ec.GetParametrosLegalesByAccountAndYearAsync(idAccount, anio);

        public async Task<Models.ParametrosLegales> CrearParametrosLegalesAsync(Models.ParametrosLegales parametros)
        {
            parametros.IdParametrosLegales = Guid.NewGuid();
            await ec.AddNewRecordAsync(parametros);
            return parametros;
        }

        public async Task<Models.ParametrosLegales> ActualizarParametrosLegalesAsync(Models.ParametrosLegales parametros)
        {
            await ec.UpdateRecordAsync(parametros);
            return parametros;
        }

        public async Task<List<Models.Vacaciones>> GetVacacionesByPersonalAsync(Guid idPersonal)
            => await ec.GetVacacionesByPersonalAsync(idPersonal);

        public async Task<List<Models.Vacaciones>> GetVacacionesPendientesByAccountAsync(Guid idAccount)
            => await ec.GetVacacionesPendientesByAccountAsync(idAccount);

        public async Task<Models.SaldoVacaciones> GetSaldoVacacionesAsync(Guid idPersonal, Guid idAccount, int anio)
        {
            var regimen = await ec.GetConfiguracionRegimenLaboralVigenteAsync(idAccount, new DateTime(anio, 12, 31));
            var diasGozados = await ec.GetVacacionesGozadasByPersonalAnioAsync(idPersonal, anio);
            return new Models.SaldoVacaciones
            {
                Anio = anio,
                DiasGanados = regimen?.DiasVacacionesPorAnio ?? 15,
                DiasGozados = diasGozados
            };
        }

        public async Task<OperationResult> SolicitarVacacionesAsync(Models.Vacaciones vacaciones, string performedBy)
        {
            try
            {
                vacaciones.IdVacaciones = Guid.NewGuid();
                vacaciones.CreatedBy = performedBy;
                await ec.AddNewRecordAsync(vacaciones);

                var idBuilding = await ResolveIdBuildingAsync(vacaciones.IdPersonal);
                await _workflowAuditService.LogAsync("Vacaciones", vacaciones.IdVacaciones, WorkflowAction.Submitted, performedBy, idBuilding);

                return OperationResult.Success(vacaciones);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"No se pudo registrar la solicitud de vacaciones: {DescribeError(ex)}");
            }
        }

        public async Task AprobarVacacionesAsync(Guid idVacaciones, Guid idPersonal, Guid idAprobador, string performedBy)
        {
            await ec.UpdateVacacionesEstadoAsync(idVacaciones, nameof(EstadoVacaciones.Aprobada), idAprobador);
            var idBuilding = await ResolveIdBuildingAsync(idPersonal);
            await _workflowAuditService.LogAsync("Vacaciones", idVacaciones, WorkflowAction.Approved, performedBy, idBuilding);
        }

        public async Task RechazarVacacionesAsync(Guid idVacaciones, Guid idPersonal, Guid idAprobador, string performedBy, string? motivo)
        {
            await ec.UpdateVacacionesEstadoAsync(idVacaciones, nameof(EstadoVacaciones.Rechazada), idAprobador);
            var idBuilding = await ResolveIdBuildingAsync(idPersonal);
            await _workflowAuditService.LogAsync("Vacaciones", idVacaciones, WorkflowAction.Rejected, performedBy, idBuilding, motivo);
        }

        // Fase 3 -- Permisos y licencias. Mismo patrón exacto que Vacaciones,
        // sólo cambia el Module de WorkflowAuditLog.
        public async Task<List<Models.PermisoLicencia>> GetPermisosByPersonalAsync(Guid idPersonal)
            => await ec.GetPermisoLicenciaByPersonalAsync(idPersonal);

        public async Task<List<Models.PermisoLicencia>> GetPermisosPendientesByAccountAsync(Guid idAccount)
            => await ec.GetPermisoLicenciaPendientesByAccountAsync(idAccount);

        public async Task<OperationResult> SolicitarPermisoAsync(Models.PermisoLicencia permiso, string performedBy)
        {
            try
            {
                permiso.IdPermisoLicencia = Guid.NewGuid();
                permiso.CreatedBy = performedBy;
                await ec.AddNewRecordAsync(permiso);

                var idBuilding = await ResolveIdBuildingAsync(permiso.IdPersonal);
                await _workflowAuditService.LogAsync("PermisoLicencia", permiso.IdPermisoLicencia, WorkflowAction.Submitted, performedBy, idBuilding);

                return OperationResult.Success(permiso);
            }
            catch (Exception ex)
            {
                return OperationResult.Failure($"No se pudo registrar la solicitud de permiso: {DescribeError(ex)}");
            }
        }

        public async Task AprobarPermisoAsync(Guid idPermisoLicencia, Guid idPersonal, Guid idAprobador, string performedBy)
        {
            await ec.UpdatePermisoLicenciaEstadoAsync(idPermisoLicencia, nameof(EstadoPermisoLicencia.Aprobado), idAprobador);
            var idBuilding = await ResolveIdBuildingAsync(idPersonal);
            await _workflowAuditService.LogAsync("PermisoLicencia", idPermisoLicencia, WorkflowAction.Approved, performedBy, idBuilding);
        }

        public async Task RechazarPermisoAsync(Guid idPermisoLicencia, Guid idPersonal, Guid idAprobador, string performedBy, string? motivo)
        {
            await ec.UpdatePermisoLicenciaEstadoAsync(idPermisoLicencia, nameof(EstadoPermisoLicencia.Rechazado), idAprobador);
            var idBuilding = await ResolveIdBuildingAsync(idPersonal);
            await _workflowAuditService.LogAsync("PermisoLicencia", idPermisoLicencia, WorkflowAction.Rejected, performedBy, idBuilding, motivo);
        }

        // Lee RegistroHoras del mes y aplica las reglas del régimen vigente
        // (sección 9 de la especificación: la estructura de la boleta no cambia
        // por régimen, sólo qué filas de BoletaPagoDetalle aparecen).
        //
        // Simplificaciones deliberadas de esta Fase 2 (ver comentarios inline):
        // CTS no se calcula (no es parte de "lo mínimo indispensable" de la
        // boleta mensual, es un depósito aparte); PorcentajeAFP es un aproximado,
        // no el aporte exacto por fondo AFP real; renta de 5ta categoría se
        // omite mientras la remuneración anualizada no supere 7 UIT (el caso
        // normal para personal de edificio) -- por encima de ese umbral la
        // boleta NO se genera (mejor fallar explícito que declarar un monto
        // incorrecto), el cálculo progresivo por tramos queda para una
        // iteración futura.
        public async Task<OperationResult> GenerarBoletaAsync(Guid idPersonal, int anio, int mes, string performedBy)
        {
            try
            {
                var personal = await _personalService.GetPersonalByIdAsync(idPersonal);
                if (personal == null)
                    return OperationResult.Failure("No se encontró a esta persona.");

                var fechaCorte = new DateTime(anio, mes, DateTime.DaysInMonth(anio, mes));
                var regimen = await ec.GetConfiguracionRegimenLaboralVigenteAsync(personal.IdAccount, fechaCorte);
                if (regimen == null)
                    return OperationResult.Failure("Configura primero el régimen laboral de tu Cuenta (Personal > Régimen laboral).");

                var parametros = await ec.GetParametrosLegalesByAccountAndYearAsync(personal.IdAccount, anio);
                if (parametros == null)
                    return OperationResult.Failure($"Configura primero los parámetros legales del año {anio} (Personal > Régimen laboral).");

                var fechaDesde = new DateTime(anio, mes, 1);
                var registros = await _personalService.GetRegistroHorasByPersonalAsync(idPersonal, fechaDesde, fechaCorte);

                var horasExtra25 = registros.Sum(r => r.HorasExtra25);
                var horasExtra35 = registros.Sum(r => r.HorasExtra35);

                // Valor hora = remuneración mensual / 30 / 8 (sección 6 de la especificación).
                var valorHora = personal.RemuneracionBase / 30m / 8m;
                var montoExtra25 = Math.Round(horasExtra25 * valorHora * 1.25m, 2);
                var montoExtra35 = Math.Round(horasExtra35 * valorHora * 1.35m, 2);

                var detalles = new List<Models.BoletaPagoDetalle>();

                void AgregarIngreso(string codigo, string descripcion, decimal monto, bool remunerativo = true)
                {
                    if (monto <= 0) return;
                    detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "Ingreso", CodigoConcepto = codigo, Descripcion = descripcion, Monto = monto, EsRemunerativo = remunerativo });
                }

                AgregarIngreso("BASICO", "Remuneración básica", personal.RemuneracionBase);
                AgregarIngreso("HEXT25", "Horas extra 25%", montoExtra25);
                AgregarIngreso("HEXT35", "Horas extra 35%", montoExtra35);

                // Gratificación (jul/dic) -- no aplica a Microempresa.
                if (regimen.AplicaGratificacion && (mes == 7 || mes == 12))
                {
                    var montoGratif = regimen.TipoRegimen == nameof(TipoRegimenLaboral.RegimenGeneral)
                        ? personal.RemuneracionBase
                        : personal.RemuneracionBase / 2m;
                    AgregarIngreso("GRATIF", $"Gratificación {(mes == 7 ? "Fiestas Patrias" : "Navidad")}", montoGratif);
                }

                // Asignación familiar -- sólo Régimen General, sólo si tiene hijos.
                if (regimen.AplicaAsignacionFamiliar && personal.TieneHijos)
                    AgregarIngreso("ASIGFAM", "Asignación familiar", parametros.MontoAsignacionFamiliar);

                var totalIngresos = detalles.Where(d => d.TipoConcepto == "Ingreso").Sum(d => d.Monto);
                var baseComputable = detalles.Where(d => d.TipoConcepto == "Ingreso" && d.EsRemunerativo).Sum(d => d.Monto);

                // Renta de 5ta -- ver comentario del método. Se valida ANTES de
                // insertar nada: si corresponde el cálculo por tramos (no
                // implementado), la boleta no se genera.
                if (personal.RemuneracionBase * 12m > parametros.ValorUIT * 7m)
                {
                    return OperationResult.Failure(
                        $"La remuneración anualizada de {personal.NombreCompleto} supera las 7 UIT -- el cálculo de renta de 5ta categoría por tramos todavía no está implementado en SpiderHood. No se generó la boleta.");
                }

                // Descuento del trabajador: ONP (13% fijo) o AFP (aproximado) según
                // Personal.SistemaPensionario.
                if (personal.SistemaPensionario == "AFP")
                {
                    var montoAfp = Math.Round(baseComputable * parametros.PorcentajeAFP / 100m, 2);
                    if (montoAfp > 0)
                        detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "DescuentoTrabajador", CodigoConcepto = "AFP", Descripcion = "Aporte AFP (aproximado)", Monto = montoAfp, EsRemunerativo = false });
                }
                else
                {
                    var montoOnp = Math.Round(baseComputable * parametros.PorcentajeONP / 100m, 2);
                    if (montoOnp > 0)
                        detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "DescuentoTrabajador", CodigoConcepto = "ONP", Descripcion = "Aporte ONP", Monto = montoOnp, EsRemunerativo = false });
                }

                // Permisos SIN goce de haber (Fase 3) -- se descuentan proporcional a
                // los días que caen dentro de este mes y ya fueron Aprobados. Un
                // permiso CON goce no descuenta nada (igual que unas vacaciones).
                // Simplificación: ONP/AFP arriba se calculan sobre la base completa,
                // sin restar estos días -- el ajuste fino de la base computable por
                // ausencias parciales queda para una iteración futura.
                var diasSinGoce = await ec.GetPermisoLicenciaSinGoceDiasByPersonalMesAsync(idPersonal, fechaDesde, fechaCorte);
                if (diasSinGoce > 0)
                {
                    var montoPermisoSinGoce = Math.Round(diasSinGoce * (personal.RemuneracionBase / 30m), 2);
                    if (montoPermisoSinGoce > 0)
                        detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "DescuentoTrabajador", CodigoConcepto = "PERMISO_SG", Descripcion = $"Permiso sin goce de haber ({diasSinGoce} día(s))", Monto = montoPermisoSinGoce, EsRemunerativo = false });
                }

                var totalDescuentos = detalles.Where(d => d.TipoConcepto == "DescuentoTrabajador").Sum(d => d.Monto);
                var netoAPagar = totalIngresos - totalDescuentos;

                // Aporte del empleador -- informativo, no descuenta el neto del
                // trabajador (EsSalud y SIS los paga la administradora, no el
                // Personal).
                if (regimen.TipoRegimen == nameof(TipoRegimenLaboral.Microempresa))
                {
                    detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "AporteEmpleador", CodigoConcepto = "SIS", Descripcion = "SIS Microempresas (a cargo del empleador)", Monto = parametros.CostoSISMensual, EsRemunerativo = false });
                }
                else
                {
                    var montoEsSalud = Math.Round(baseComputable * parametros.PorcentajeEsSalud / 100m, 2);
                    detalles.Add(new Models.BoletaPagoDetalle { TipoConcepto = "AporteEmpleador", CodigoConcepto = "ESSALUD", Descripcion = "EsSalud (a cargo del empleador)", Monto = montoEsSalud, EsRemunerativo = false });
                }

                var boleta = new Models.BoletaPago
                {
                    IdBoletaPago = Guid.NewGuid(),
                    IdPersonal = idPersonal,
                    Anio = anio,
                    Mes = mes,
                    TipoRegimen = regimen.TipoRegimen,
                    TotalIngresos = totalIngresos,
                    TotalDescuentos = totalDescuentos,
                    NetoAPagar = netoAPagar,
                    GeneradoPor = performedBy
                };

                await ec.AddNewRecordAsync(boleta);

                foreach (var detalle in detalles)
                {
                    detalle.IdBoletaPagoDetalle = Guid.NewGuid();
                    detalle.IdBoletaPago = boleta.IdBoletaPago;
                    await ec.AddNewRecordAsync(detalle);
                }

                boleta.Detalle = detalles;
                boleta.Nombres = personal.Nombres;
                boleta.Apellidos = personal.Apellidos;
                boleta.DNI = personal.DNI;
                boleta.Cargo = personal.Cargo;
                boleta.FechaIngreso = personal.FechaIngreso;
                boleta.FechaCese = personal.FechaCese;
                boleta.SistemaPensionario = personal.SistemaPensionario;
                boleta.IdAccount = personal.IdAccount;

                return OperationResult.Success(boleta);
            }
            catch (Exception ex)
            {
                if (ex.InnerException?.Message.Contains("UQ_BoletaPago_Personal_Periodo", StringComparison.OrdinalIgnoreCase) == true)
                    return OperationResult.Failure("Ya existe una boleta generada para esta persona en ese período.");

                return OperationResult.Failure($"No se pudo generar la boleta: {DescribeError(ex)}");
            }
        }

        public async Task<(int Generadas, int YaExistian, List<string> Errores)> GenerarBoletasDelMesAsync(Guid idAccount, int anio, int mes, string performedBy)
        {
            var personal = await _personalService.GetPersonalByAccountAsync(idAccount);
            int generadas = 0, yaExistian = 0;
            var errores = new List<string>();

            foreach (var p in personal.Where(p => p.IsActive))
            {
                var result = await GenerarBoletaAsync(p.IdPersonal, anio, mes, performedBy);
                if (result.IsSuccess)
                    generadas++;
                else if (result.ErrorMessage?.Contains("Ya existe una boleta", StringComparison.OrdinalIgnoreCase) == true)
                    yaExistian++;
                else
                    errores.Add($"{p.NombreCompleto}: {result.ErrorMessage}");
            }

            return (generadas, yaExistian, errores);
        }

        public async Task<List<Models.BoletaPago>> GetBoletasByPersonalAsync(Guid idPersonal)
            => await ec.GetBoletasByPersonalAsync(idPersonal);

        public async Task<List<Models.BoletaPago>> GetBoletasByAccountAndPeriodoAsync(Guid idAccount, int anio, int mes)
            => await ec.GetBoletasByAccountAndPeriodoAsync(idAccount, anio, mes);

        public async Task<Models.BoletaPago?> GetBoletaConDetalleAsync(Guid idBoletaPago)
        {
            var boleta = await ec.GetBoletaByIdAsync(idBoletaPago);
            if (boleta == null)
                return null;

            boleta.Detalle = await ec.GetBoletaPagoDetalleByBoletaAsync(idBoletaPago);
            return boleta;
        }
    }
}
