using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Módulo Employee y Planillas -- Fase 2 (planillas), ver
    // Database/Scripts/2026-09-15_109_Employee_Planillas_Fase2.sql.

    public enum LaborRegimeType
    {
        Microempresa,
        PequenaEmpresa,
        RegimenGeneral
    }

    // Un registro vigente por Cuenta; historizable si el régimen cambia
    // (crecimiento de ventas) -- GET_LaborRegimeConfigurationVigente trae la
    // fila más reciente cuya FechaVigenciaDesde ya pasó.
    public class LaborRegimeConfiguration
    {
        public Guid IdLaborRegimeConfiguration { get; set; }
        public Guid IdAccount { get; set; }
        public string TipoRegimen { get; set; } = nameof(Models.LaborRegimeType.Microempresa);
        public string? RUC { get; set; }
        public string? RazonSocial { get; set; }
        public DateTime FechaVigenciaDesde { get; set; } = DateTime.Today;
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        // Días de vacaciones ganados por año según el régimen (sección 3 de la
        // especificación) -- nunca hardcodeado fuera de este único lugar.
        public int DiasVacationPorAnio => TipoRegimen == nameof(Models.LaborRegimeType.RegimenGeneral) ? 30 : 15;
        public bool AplicaGratificacion => TipoRegimen != nameof(Models.LaborRegimeType.Microempresa);
        public bool AplicaAsignacionFamiliar => TipoRegimen == nameof(Models.LaborRegimeType.RegimenGeneral);
        public bool AplicaEsSalud => TipoRegimen != nameof(Models.LaborRegimeType.Microempresa);
    }

    // Versionado por año dentro de cada Cuenta (UIT, asignación familiar, costo
    // SIS, %EsSalud/ONP/AFP) -- cada administradora actualiza sus propios
    // valores vigentes cuando el gobierno los cambia, sin depender de una
    // nueva versión de SpiderHood.
    public class LegalParameters
    {
        public Guid IdLegalParameters { get; set; }
        public Guid IdAccount { get; set; }
        public int Anio { get; set; } = DateTime.Today.Year;
        [Precision(18, 2)]
        public decimal ValorUIT { get; set; } = 5500m;
        [Precision(18, 2)]
        public decimal MontoAsignacionFamiliar { get; set; } = 113m;
        [Precision(18, 2)]
        public decimal CostoSISMensual { get; set; } = 15m;
        [Precision(5, 2)]
        public decimal PorcentajeEsSalud { get; set; } = 9m;
        [Precision(5, 2)]
        public decimal PorcentajeONP { get; set; } = 13m;
        // Aproximado -- el aporte AFP real varía por fondo (aporte + comisión +
        // prima de seguro), ver comentario en IPlanillaService.GenerarPayslipAsync.
        [Precision(5, 2)]
        public decimal PorcentajeAFP { get; set; } = 12.5m;
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }
    }

    public enum VacationStatus
    {
        Pendiente,
        Aprobada,
        Rechazada,
        Gozada
    }

    public enum LeaveRequestStatus
    {
        Pendiente,
        Aprobado,
        Rechazado
    }

    // Fase 3 -- Permisos y licencias. Mismo patrón que Vacation (una fila por
    // solicitud, aprobación vía WorkflowAuditLog, Module = 'LeaveRequest'),
    // pero a diferencia de Vacation un permiso SIN goce de haber SÍ descuenta
    // de la boleta -- ver GET_LeaveRequestSinGoceDiasByEmployeeMes e
    // IPlanillaService.GenerarPayslipAsync.
    public class LeaveRequest
    {
        public Guid IdLeaveRequest { get; set; }
        public Guid IdEmployee { get; set; }
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public DateTime FechaFin { get; set; } = DateTime.Today;
        public bool ConGoceDeHaber { get; set; } = true;
        public string? Motivo { get; set; }
        public string Estado { get; set; } = nameof(LeaveRequestStatus.Pendiente);
        public Guid? IdAprobador { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        // Sólo poblado por GET_LeaveRequestPendientesByAccount (join contra Employee).
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }

        public int DiasSolicitados => (FechaFin.Date - FechaInicio.Date).Days + 1;
    }

    // Una fila por SOLICITUD de goce -- el saldo (ganados/gozados/pendientes)
    // se deriva en tiempo de lectura (ver IPlanillaService.GetVacationBalanceAsync),
    // no se guarda como un total aparte. Reutiliza WorkflowAuditLog (Module =
    // 'Vacation') para el historial de aprobación -- mismo motor que
    // Presupuesto/Cuota.
    public class Vacation
    {
        public Guid IdVacation { get; set; }
        public Guid IdEmployee { get; set; }
        public int Anio { get; set; } = DateTime.Today.Year;
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public DateTime FechaFin { get; set; } = DateTime.Today;
        public int DiasSolicitados { get; set; }
        public string Estado { get; set; } = nameof(VacationStatus.Pendiente);
        public Guid? IdAprobador { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        // Sólo poblado por GET_VacationPendientesByAccount (join contra Employee).
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
    }

    // Saldo de vacaciones de un Employee para un año -- calculado, no
    // almacenado (DiasGanados sale del régimen vigente, DiasGozados de
    // GET_VacationGozadasByEmployeeAnio).
    public class VacationBalance
    {
        public int Anio { get; set; }
        public int DiasGanados { get; set; }
        public int DiasGozados { get; set; }
        public int DiasPendientes => DiasGanados - DiasGozados;
    }

    // TipoRegimen se copia al momento del cálculo -- una boleta ya emitida
    // nunca cambia si el régimen de la Cuenta cambia después (sección 11,
    // decisión "nunca retroactivo").
    public class Payslip
    {
        public Guid IdPayslip { get; set; }
        public Guid IdEmployee { get; set; }
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string TipoRegimen { get; set; } = "";
        [Precision(18, 2)]
        public decimal TotalIngresos { get; set; }
        [Precision(18, 2)]
        public decimal TotalDescuentos { get; set; }
        [Precision(18, 2)]
        public decimal NetoAPagar { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public string GeneradoPor { get; set; } = "";

        // Sólo poblado por GET_PayslipById/GET_PayslipsByAccountAndPeriodo (join
        // contra Employee).
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
        public string? DNI { get; set; }
        public string? Cargo { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public DateTime? FechaCese { get; set; }
        public string? SistemaPensionario { get; set; }
        public Guid? IdAccount { get; set; }

        public string NombreEmployee => $"{Nombres} {Apellidos}".Trim();
        public string NombrePeriodo => new DateTime(Anio, Mes, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("es-PE"));

        // Usado por PayslipDetail.razor para evitar que un Administrador de
        // otra Cuenta abra la boleta de un Employee ajeno sólo por conocer/
        // adivinar el IdPayslip -- IdAccount viene NULL si el join contra
        // Employee no lo trajo (dato faltante), así que nunca "pertenece".
        public bool BelongsToAccount(Guid idAccount) => IdAccount == idAccount;

        [NotMapped]
        public List<PayslipDetail> Detalle { get; set; } = new();
    }

    // Una fila por concepto (BASICO, HEXT25, GRATIF, ESSALUD, ONP...) -- el
    // motor de reglas decide qué filas genera según el régimen, en vez de
    // columnas fijas que cambiarían de esquema cada vez que cambia una regla.
    public class PayslipDetail
    {
        public Guid IdPayslipDetail { get; set; }
        public Guid IdPayslip { get; set; }
        // Ingreso/DescuentoTrabajador/AporteEmpleador -- sólo Ingreso y
        // DescuentoTrabajador afectan NetoAPagar; AporteEmpleador (EsSalud/SIS)
        // es informativo, lo asume el empleador, no se descuenta del trabajador.
        public string TipoConcepto { get; set; } = "";
        public string CodigoConcepto { get; set; } = "";
        public string Descripcion { get; set; } = "";
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public bool EsRemunerativo { get; set; }
    }
}
