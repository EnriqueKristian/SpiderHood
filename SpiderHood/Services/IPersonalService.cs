using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Módulo Personal y Planillas -- Fase 1 (núcleo operativo): Personal, Turno,
    // rotación entre edificios (AsignacionPersonalEdificio) y registro de
    // horas/feriados. NO incluye cálculo de planillas -- eso es Fase 2.
    public interface IPersonalService
    {
        Task<List<Models.Personal>> GetPersonalByAccountAsync(Guid idAccount);
        Task<Models.Personal> GetPersonalByIdAsync(Guid idPersonal);
        Task<Models.Personal> CreatePersonalAsync(Models.Personal personal);
        Task<Models.Personal> UpdatePersonalAsync(Models.Personal personal);

        Task<List<Models.Turno>> GetTurnosByAccountAsync(Guid idAccount);
        Task<Models.Turno> CreateTurnoAsync(Models.Turno turno);
        Task<Models.Turno> UpdateTurnoAsync(Models.Turno turno);

        Task<List<Models.AsignacionPersonalTurno>> GetAsignacionesTurnoByPersonalAsync(Guid idPersonal);
        Task<Models.AsignacionPersonalTurno> AsignarTurnoAsync(Guid idPersonal, Guid idTurno, DateTime fechaDesde);

        Task<List<Models.AsignacionPersonalEdificio>> GetAsignacionesEdificioByPersonalAsync(Guid idPersonal);
        Task<List<Models.AsignacionPersonalEdificio>> GetAsignacionesEdificioByBuildingAsync(Guid idBuilding);
        Task<Models.AsignacionPersonalEdificio> AsignarEdificioAsync(Guid idPersonal, Guid idBuilding, DateTime fechaDesde, decimal porcentajeDedicacion, string createdBy);
        Task CerrarAsignacionEdificioAsync(Guid idAsignacionPersonalEdificio, DateTime fechaHasta);

        Task<List<Models.RegistroHoras>> GetRegistroHorasByPersonalAsync(Guid idPersonal, DateTime fechaDesde, DateTime fechaHasta);
        Task<OperationResult> RegistrarHorasAsync(Models.RegistroHoras registro);
        Task<Models.RegistroHoras> UpdateRegistroHorasAsync(Models.RegistroHoras registro);
        Task DeleteRegistroHorasAsync(Models.RegistroHoras registro);

        Task<List<Models.ConfiguracionFeriado>> GetFeriadosAsync(Guid? idAccount, int anio);
        Task<Models.ConfiguracionFeriado> CreateFeriadoAsync(Models.ConfiguracionFeriado feriado);
        Task<Models.ConfiguracionFeriado> UpdateFeriadoAsync(Models.ConfiguracionFeriado feriado);
        Task DeleteFeriadoAsync(Models.ConfiguracionFeriado feriado);
    }

    public class PersonalService : IPersonalService
    {
        private BDLayout ec { get; set; }
        private readonly AuthService _authService;

        public PersonalService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
        {
            ec = new BDLayout(contextFactory);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
        }

        // BDLayout envuelve la excepción real (la de SQL Server) en una
        // RepositoryException genérica -- mismo patrón que BuildingService.DescribeError.
        private static string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }

        public async Task<List<Models.Personal>> GetPersonalByAccountAsync(Guid idAccount)
            => await ec.GetPersonalByAccountAsync(idAccount);

        public async Task<Models.Personal> GetPersonalByIdAsync(Guid idPersonal)
            => await ec.GetPersonalByIdAsync(idPersonal);

        public async Task<Models.Personal> CreatePersonalAsync(Models.Personal personal)
        {
            if (personal.IdPersonal == Guid.Empty)
                personal.IdPersonal = Guid.NewGuid();

            personal.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(personal);
            return personal;
        }

        public async Task<Models.Personal> UpdatePersonalAsync(Models.Personal personal)
        {
            personal.ModifiedBy = await GetPerformedByAsync();
            await ec.UpdateRecordAsync(personal);
            return personal;
        }

        public async Task<List<Models.Turno>> GetTurnosByAccountAsync(Guid idAccount)
            => await ec.GetTurnosByAccountAsync(idAccount);

        public async Task<Models.Turno> CreateTurnoAsync(Models.Turno turno)
        {
            if (turno.IdTurno == Guid.Empty)
                turno.IdTurno = Guid.NewGuid();

            turno.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(turno);
            return turno;
        }

        public async Task<Models.Turno> UpdateTurnoAsync(Models.Turno turno)
        {
            await ec.UpdateRecordAsync(turno);
            return turno;
        }

        public async Task<List<Models.AsignacionPersonalTurno>> GetAsignacionesTurnoByPersonalAsync(Guid idPersonal)
            => await ec.GetAsignacionesTurnoByPersonalAsync(idPersonal);

        // INS_AsignacionPersonalTurno cierra sola la asignación vigente anterior
        // (ver el script de BD) -- acá sólo se arma la fila nueva.
        public async Task<Models.AsignacionPersonalTurno> AsignarTurnoAsync(Guid idPersonal, Guid idTurno, DateTime fechaDesde)
        {
            var asignacion = new Models.AsignacionPersonalTurno
            {
                IdAsignacionPersonalTurno = Guid.NewGuid(),
                IdPersonal = idPersonal,
                IdTurno = idTurno,
                FechaDesde = fechaDesde,
                CreatedBy = await GetPerformedByAsync()
            };
            await ec.AddNewRecordAsync(asignacion);
            return asignacion;
        }

        public async Task<List<Models.AsignacionPersonalEdificio>> GetAsignacionesEdificioByPersonalAsync(Guid idPersonal)
            => await ec.GetAsignacionesEdificioByPersonalAsync(idPersonal);

        public async Task<List<Models.AsignacionPersonalEdificio>> GetAsignacionesEdificioByBuildingAsync(Guid idBuilding)
            => await ec.GetAsignacionesEdificioByBuildingAsync(idBuilding);

        // ESTO es la rotación (sección 5 de la especificación) -- un Personal
        // puede tener varias filas vigentes a la vez, así que a diferencia de
        // AsignarTurnoAsync acá no se cierra nada previo.
        public async Task<Models.AsignacionPersonalEdificio> AsignarEdificioAsync(Guid idPersonal, Guid idBuilding, DateTime fechaDesde, decimal porcentajeDedicacion, string createdBy)
        {
            var asignacion = new Models.AsignacionPersonalEdificio
            {
                IdAsignacionPersonalEdificio = Guid.NewGuid(),
                IdPersonal = idPersonal,
                IdBuilding = idBuilding,
                FechaDesde = fechaDesde,
                PorcentajeDedicacion = porcentajeDedicacion,
                CreatedBy = createdBy
            };
            await ec.AddNewRecordAsync(asignacion);
            return asignacion;
        }

        public async Task CerrarAsignacionEdificioAsync(Guid idAsignacionPersonalEdificio, DateTime fechaHasta)
            => await ec.CerrarAsignacionPersonalEdificioAsync(idAsignacionPersonalEdificio, fechaHasta);

        public async Task<List<Models.RegistroHoras>> GetRegistroHorasByPersonalAsync(Guid idPersonal, DateTime fechaDesde, DateTime fechaHasta)
            => await ec.GetRegistroHorasByPersonalAsync(idPersonal, fechaDesde, fechaHasta);

        // UQ_RegistroHoras_Personal_Fecha (una fila por Personal+Fecha) es la
        // fuente de verdad -- acá sólo se traduce la violación en un mensaje
        // legible, mismo criterio que DeleteBuildingAsync/IsForeignKeyViolation.
        public async Task<OperationResult> RegistrarHorasAsync(Models.RegistroHoras registro)
        {
            try
            {
                if (registro.IdRegistroHoras == Guid.Empty)
                    registro.IdRegistroHoras = Guid.NewGuid();

                await ec.AddNewRecordAsync(registro);
                return OperationResult.Success(registro);
            }
            catch (Exception ex)
            {
                if (ex.InnerException?.Message.Contains("UQ_RegistroHoras_Personal_Fecha", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return OperationResult.Failure("Ya existe un registro de horas para esta persona en esa fecha. Edítalo en vez de crear uno nuevo.");
                }

                return OperationResult.Failure($"No se pudo registrar las horas: {DescribeError(ex)}");
            }
        }

        public async Task<Models.RegistroHoras> UpdateRegistroHorasAsync(Models.RegistroHoras registro)
        {
            await ec.UpdateRecordAsync(registro);
            return registro;
        }

        public async Task DeleteRegistroHorasAsync(Models.RegistroHoras registro)
            => await ec.DeleteRecordAsync(registro);

        public async Task<List<Models.ConfiguracionFeriado>> GetFeriadosAsync(Guid? idAccount, int anio)
            => await ec.GetFeriadosByAccountAndYearAsync(idAccount, anio);

        public async Task<Models.ConfiguracionFeriado> CreateFeriadoAsync(Models.ConfiguracionFeriado feriado)
        {
            if (feriado.IdConfiguracionFeriados == Guid.Empty)
                feriado.IdConfiguracionFeriados = Guid.NewGuid();

            feriado.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(feriado);
            return feriado;
        }

        public async Task<Models.ConfiguracionFeriado> UpdateFeriadoAsync(Models.ConfiguracionFeriado feriado)
        {
            await ec.UpdateRecordAsync(feriado);
            return feriado;
        }

        public async Task DeleteFeriadoAsync(Models.ConfiguracionFeriado feriado)
            => await ec.DeleteRecordAsync(feriado);
    }
}
