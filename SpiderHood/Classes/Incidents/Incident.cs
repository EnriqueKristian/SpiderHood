namespace SpiderHood.Models
{
    // Reported: recién creado por el Residente/Administrador.
    // InReview: Administrador lo revisó, todavía sin asignar.
    // InProgress: asignado, en ejecución.
    // Resolved: el trabajo terminó, pendiente de confirmación.
    // Closed: confirmado/cerrado.
    // Rejected: inválido/duplicado (se rechaza desde InReview).
    // Reopened: el Residente no quedó conforme con un Resolved.
    public enum IncidentStatus
    {
        Reported,
        InReview,
        InProgress,
        Resolved,
        Closed,
        Rejected,
        Reopened
    }

    public class Incident
    {
        public Guid IdIncident { get; set; } = Guid.NewGuid();
        public Guid IdBuilding { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        // Tipo y Prioridad ya no son enums fijos -- referencian Parameter.Value
        // dentro de sus respectivos grupos ("Tipo Incidente"/"Prioridad
        // Incidente", ver Database/Scripts/2026-09-02_14_*), para que un
        // Administrador pueda agregar/desactivar valores sin desplegar código
        // nuevo. TypeName/PriorityName se traen por JOIN en GET_Incidents*
        // (mismo criterio que ReportedByName/UnitName más abajo).
        public int Type { get; set; }
        public string? TypeName { get; set; }
        public int Priority { get; set; } = 2; // "Media" en la seed -- ver script
        public string? PriorityName { get; set; }
        public IncidentStatus Status { get; set; } = IncidentStatus.Reported;
        public Guid? IdGroupUnit { get; set; }
        public Guid ReportedBy { get; set; }
        public Guid? AssignedTo { get; set; }
        public DateTime? ResolvedOn { get; set; }
        public DateTime? ClosedOn { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }

        // Poblados por los SPs GET_Incidents* (JOIN), vacíos al escribir -- no
        // hace falta [NotMapped] porque BDLayout no serializa el objeto entero,
        // solo pasa los parámetros que cada INS_/UPD_ necesita explícitamente.
        public string ReportedByName { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public string? UnitName { get; set; }
    }

    public class IncidentComment
    {
        public Guid IdComment { get; set; } = Guid.NewGuid();
        public Guid IdIncident { get; set; }
        public Guid AuthorId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsInternal { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public string AuthorName { get; set; } = string.Empty;
    }
}
