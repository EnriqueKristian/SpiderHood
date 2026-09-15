using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Módulo Employee y Payroll -- Fase 1 (núcleo operativo): Employee, Shift,
    // rotación entre edificios (EmployeeBuildingAssignment) y registro de
    // horas/feriados. NO incluye cálculo de planillas -- eso es Fase 2.
    public interface IEmployeeService
    {
        Task<List<Models.Employee>> GetEmployeeByAccountAsync(Guid idAccount);
        Task<Models.Employee> GetEmployeeByIdAsync(Guid idEmployee);
        Task<Models.Employee> CreateEmployeeAsync(Models.Employee personal);
        Task<Models.Employee> UpdateEmployeeAsync(Models.Employee personal);

        Task<List<Models.Shift>> GetShiftsByAccountAsync(Guid idAccount);
        Task<Models.Shift> CreateShiftAsync(Models.Shift turno);
        Task<Models.Shift> UpdateShiftAsync(Models.Shift turno);

        Task<List<Models.EmployeeShiftAssignment>> GetAsignacionesShiftByEmployeeAsync(Guid idEmployee);
        Task<Models.EmployeeShiftAssignment> AsignarShiftAsync(Guid idEmployee, Guid idShift, DateTime fechaDesde);

        Task<List<Models.EmployeeBuildingAssignment>> GetAsignacionesEdificioByEmployeeAsync(Guid idEmployee);
        Task<List<Models.EmployeeBuildingAssignment>> GetAsignacionesEdificioByBuildingAsync(Guid idBuilding);
        Task<Models.EmployeeBuildingAssignment> AsignarEdificioAsync(Guid idEmployee, Guid idBuilding, DateTime fechaDesde, decimal porcentajeDedicacion, string createdBy);
        Task CerrarAsignacionEdificioAsync(Guid idEmployeeBuildingAssignment, DateTime fechaHasta);

        Task<List<Models.TimeEntry>> GetTimeEntryByEmployeeAsync(Guid idEmployee, DateTime fechaDesde, DateTime fechaHasta);
        Task<OperationResult> RegistrarHorasAsync(Models.TimeEntry registro);
        Task<Models.TimeEntry> UpdateTimeEntryAsync(Models.TimeEntry registro);
        Task DeleteTimeEntryAsync(Models.TimeEntry registro);

        Task<List<Models.HolidayConfiguration>> GetFeriadosAsync(Guid? idAccount, int anio);
        Task<Models.HolidayConfiguration> CreateFeriadoAsync(Models.HolidayConfiguration feriado);
        Task<Models.HolidayConfiguration> UpdateFeriadoAsync(Models.HolidayConfiguration feriado);
        Task DeleteFeriadoAsync(Models.HolidayConfiguration feriado);
    }

    public class EmployeeService : IEmployeeService
    {
        private BDLayout ec { get; set; }
        private readonly AuthService _authService;

        public EmployeeService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
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

        public async Task<List<Models.Employee>> GetEmployeeByAccountAsync(Guid idAccount)
            => await ec.GetEmployeeByAccountAsync(idAccount);

        public async Task<Models.Employee> GetEmployeeByIdAsync(Guid idEmployee)
            => await ec.GetEmployeeByIdAsync(idEmployee);

        public async Task<Models.Employee> CreateEmployeeAsync(Models.Employee personal)
        {
            if (personal.IdEmployee == Guid.Empty)
                personal.IdEmployee = Guid.NewGuid();

            personal.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(personal);
            return personal;
        }

        public async Task<Models.Employee> UpdateEmployeeAsync(Models.Employee personal)
        {
            personal.ModifiedBy = await GetPerformedByAsync();
            await ec.UpdateRecordAsync(personal);
            return personal;
        }

        public async Task<List<Models.Shift>> GetShiftsByAccountAsync(Guid idAccount)
            => await ec.GetShiftsByAccountAsync(idAccount);

        public async Task<Models.Shift> CreateShiftAsync(Models.Shift turno)
        {
            if (turno.IdShift == Guid.Empty)
                turno.IdShift = Guid.NewGuid();

            turno.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(turno);
            return turno;
        }

        public async Task<Models.Shift> UpdateShiftAsync(Models.Shift turno)
        {
            await ec.UpdateRecordAsync(turno);
            return turno;
        }

        public async Task<List<Models.EmployeeShiftAssignment>> GetAsignacionesShiftByEmployeeAsync(Guid idEmployee)
            => await ec.GetAsignacionesShiftByEmployeeAsync(idEmployee);

        // INS_EmployeeShiftAssignment cierra sola la asignación vigente anterior
        // (ver el script de BD) -- acá sólo se arma la fila nueva.
        public async Task<Models.EmployeeShiftAssignment> AsignarShiftAsync(Guid idEmployee, Guid idShift, DateTime fechaDesde)
        {
            var asignacion = new Models.EmployeeShiftAssignment
            {
                IdEmployeeShiftAssignment = Guid.NewGuid(),
                IdEmployee = idEmployee,
                IdShift = idShift,
                FechaDesde = fechaDesde,
                CreatedBy = await GetPerformedByAsync()
            };
            await ec.AddNewRecordAsync(asignacion);
            return asignacion;
        }

        public async Task<List<Models.EmployeeBuildingAssignment>> GetAsignacionesEdificioByEmployeeAsync(Guid idEmployee)
            => await ec.GetAsignacionesEdificioByEmployeeAsync(idEmployee);

        public async Task<List<Models.EmployeeBuildingAssignment>> GetAsignacionesEdificioByBuildingAsync(Guid idBuilding)
            => await ec.GetAsignacionesEdificioByBuildingAsync(idBuilding);

        // ESTO es la rotación (sección 5 de la especificación) -- un Employee
        // puede tener varias filas vigentes a la vez, así que a diferencia de
        // AsignarShiftAsync acá no se cierra nada previo.
        public async Task<Models.EmployeeBuildingAssignment> AsignarEdificioAsync(Guid idEmployee, Guid idBuilding, DateTime fechaDesde, decimal porcentajeDedicacion, string createdBy)
        {
            var asignacion = new Models.EmployeeBuildingAssignment
            {
                IdEmployeeBuildingAssignment = Guid.NewGuid(),
                IdEmployee = idEmployee,
                IdBuilding = idBuilding,
                FechaDesde = fechaDesde,
                PorcentajeDedicacion = porcentajeDedicacion,
                CreatedBy = createdBy
            };
            await ec.AddNewRecordAsync(asignacion);
            return asignacion;
        }

        public async Task CerrarAsignacionEdificioAsync(Guid idEmployeeBuildingAssignment, DateTime fechaHasta)
            => await ec.CerrarEmployeeBuildingAssignmentAsync(idEmployeeBuildingAssignment, fechaHasta);

        public async Task<List<Models.TimeEntry>> GetTimeEntryByEmployeeAsync(Guid idEmployee, DateTime fechaDesde, DateTime fechaHasta)
            => await ec.GetTimeEntryByEmployeeAsync(idEmployee, fechaDesde, fechaHasta);

        // UQ_TimeEntry_Employee_Fecha (una fila por Employee+Fecha) es la
        // fuente de verdad -- acá sólo se traduce la violación en un mensaje
        // legible, mismo criterio que DeleteBuildingAsync/IsForeignKeyViolation.
        public async Task<OperationResult> RegistrarHorasAsync(Models.TimeEntry registro)
        {
            try
            {
                if (registro.IdTimeEntry == Guid.Empty)
                    registro.IdTimeEntry = Guid.NewGuid();

                await ec.AddNewRecordAsync(registro);
                return OperationResult.Success(registro);
            }
            catch (Exception ex)
            {
                if (ex.InnerException?.Message.Contains("UQ_TimeEntry_Employee_Fecha", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return OperationResult.Failure("Ya existe un registro de horas para esta persona en esa fecha. Edítalo en vez de crear uno nuevo.");
                }

                return OperationResult.Failure($"No se pudo registrar las horas: {DescribeError(ex)}");
            }
        }

        public async Task<Models.TimeEntry> UpdateTimeEntryAsync(Models.TimeEntry registro)
        {
            await ec.UpdateRecordAsync(registro);
            return registro;
        }

        public async Task DeleteTimeEntryAsync(Models.TimeEntry registro)
            => await ec.DeleteRecordAsync(registro);

        public async Task<List<Models.HolidayConfiguration>> GetFeriadosAsync(Guid? idAccount, int anio)
            => await ec.GetFeriadosByAccountAndYearAsync(idAccount, anio);

        public async Task<Models.HolidayConfiguration> CreateFeriadoAsync(Models.HolidayConfiguration feriado)
        {
            if (feriado.IdHolidayConfiguration == Guid.Empty)
                feriado.IdHolidayConfiguration = Guid.NewGuid();

            feriado.CreatedBy = await GetPerformedByAsync();
            await ec.AddNewRecordAsync(feriado);
            return feriado;
        }

        public async Task<Models.HolidayConfiguration> UpdateFeriadoAsync(Models.HolidayConfiguration feriado)
        {
            await ec.UpdateRecordAsync(feriado);
            return feriado;
        }

        public async Task DeleteFeriadoAsync(Models.HolidayConfiguration feriado)
            => await ec.DeleteRecordAsync(feriado);
    }
}
