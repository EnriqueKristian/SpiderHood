using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    // Módulo Personal y Planillas -- Fase 1 (núcleo operativo), ver
    // Database/Scripts/2026-09-15_108_Personal_Planillas_Fase1.sql. Personal
    // cuelga de IdAccount (la administradora), no de un Building -- el mismo
    // criterio que Building.IdAccount (Docs/Design-Account-Facturacion.md).
    public class Personal
    {
        public Guid IdPersonal { get; set; }
        public Guid IdAccount { get; set; }
        public string DNI { get; set; } = "";
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public string Cargo { get; set; } = "";
        public DateTime FechaIngreso { get; set; } = DateTime.Today;
        public DateTime? FechaCese { get; set; }
        [Precision(18, 2)]
        public decimal RemuneracionBase { get; set; }
        public string SistemaPensionario { get; set; } = "ONP";
        public string? Telefono { get; set; }
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        // Fase 2 -- Asignación Familiar (Régimen General, S/ 113/mes si tiene
        // hijos, ver Database/Scripts/2026-09-15_109_Personal_Planillas_Fase2.sql).
        public bool TieneHijos { get; set; }

        public string NombreCompleto => $"{Nombres} {Apellidos}".Trim();
    }

    // Plantilla reutilizable de horario ("Diurno 7am-3pm") -- se define una vez
    // por Cuenta y se asigna a cualquier Personal vía AsignacionPersonalTurno.
    public class Turno
    {
        public Guid IdTurno { get; set; }
        public Guid IdAccount { get; set; }
        public string Nombre { get; set; } = "";
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        // CSV de días 1(lunes)..7(domingo), ej "1,2,3,4,5" -- ver DiasSemanaList.
        public string DiasSemana { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        public List<int> DiasSemanaList =>
            DiasSemana.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(d => int.TryParse(d, out var n) ? n : 0)
                .Where(n => n is >= 1 and <= 7)
                .ToList();
    }

    // Qué Turno tuvo cada Personal en cada periodo -- historizable (FechaHasta
    // NULL = vigente). INS_AsignacionPersonalTurno cierra la anterior sola.
    public class AsignacionPersonalTurno
    {
        public Guid IdAsignacionPersonalTurno { get; set; }
        public Guid IdPersonal { get; set; }
        public Guid IdTurno { get; set; }
        public DateTime FechaDesde { get; set; } = DateTime.Today;
        public DateTime? FechaHasta { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        // Sólo viene poblado en las filas que trae GET_AsignacionesTurnoByPersonal
        // (join contra Turno) -- no se manda de vuelta en el INSERT.
        public string? TurnoNombre { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string? DiasSemana { get; set; }
    }

    // ESTO es la rotación (sección 5 de la especificación): asignación de
    // Personal a Building con su propio rango de fechas y, si el reparto no es
    // todo-o-nada, un porcentaje de dedicación. Un Personal puede tener VARIAS
    // filas vigentes a la vez (distintos edificios en distintos días de la
    // semana) -- a diferencia de Turno, INS_AsignacionPersonalEdificio no cierra
    // ninguna asignación previa.
    public class AsignacionPersonalEdificio
    {
        public Guid IdAsignacionPersonalEdificio { get; set; }
        public Guid IdPersonal { get; set; }
        public Guid IdBuilding { get; set; }
        public DateTime FechaDesde { get; set; } = DateTime.Today;
        public DateTime? FechaHasta { get; set; }
        [Precision(5, 2)]
        public decimal PorcentajeDedicacion { get; set; } = 100m;
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        // Sólo poblado por los GET (join contra Building/Personal según el caso).
        public string? BuildingName { get; set; }
        public string? Nombres { get; set; }
        public string? Apellidos { get; set; }
        public string? Cargo { get; set; }
    }

    // MVP: lo llena únicamente el administrador del edificio -- sin auto-registro
    // del Personal ni biometría (decisión confirmada, sección 11 de la
    // especificación). HorasExtra25/35 quedan separadas por franja a nivel de
    // cada día para poder auditar el cálculo después, no recién al emitir la
    // boleta.
    public class RegistroHoras
    {
        public Guid IdRegistroHoras { get; set; }
        public Guid IdPersonal { get; set; }
        public Guid IdAsignacionEdificio { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;
        [Precision(5, 2)]
        public decimal HorasOrdinarias { get; set; }
        [Precision(5, 2)]
        public decimal HorasExtra25 { get; set; }
        [Precision(5, 2)]
        public decimal HorasExtra35 { get; set; }
        public bool EsFeriado { get; set; }
        public string? Observaciones { get; set; }
        public Guid IdUsuarioRegistro { get; set; }
        public DateTime CreatedOn { get; set; }

        // Sólo poblado por GET_RegistroHorasByPersonal (join contra
        // AsignacionPersonalEdificio -> Building).
        public string? BuildingName { get; set; }

        public decimal TotalHoras => HorasOrdinarias + HorasExtra25 + HorasExtra35;
    }

    // Sembrada con los feriados nacionales (IdAccount = NULL), pero editable --
    // cada Cuenta puede agregar los suyos (sección 7 de la especificación: el
    // Ejecutivo declara no-laborables compensables por decreto supremo cada año).
    public class ConfiguracionFeriado
    {
        public Guid IdConfiguracionFeriados { get; set; }
        public Guid? IdAccount { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Today;
        public string Nombre { get; set; } = "";
        public string Tipo { get; set; } = "Nacional";
        public int Anio { get; set; }
        public string CreatedBy { get; set; } = "";
        public DateTime CreatedOn { get; set; }

        public bool EsNacional => IdAccount == null;
    }
}
