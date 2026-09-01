namespace SpiderHood.Models
{
    // Registro de auditoría para transiciones de workflow (aprobar/rechazar/publicar/etc.)
    // -- distinto de Auditoría de cabecera (CreatedBy/ModifiedBy en la fila misma, ver
    // Classes/Audit.cs): esto es un historial append-only de decisiones, no un estado.
    public class WorkflowAuditEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Module { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public WorkflowAction Action { get; set; }
        public string PerformedBy { get; set; } = string.Empty;
        public DateTime PerformedOn { get; set; } = DateTime.UtcNow;
        public string? Comment { get; set; }
        public Guid IdBuilding { get; set; }
    }

    public enum WorkflowAction
    {
        Submitted,
        Approved,
        Rejected,
        Published,
        Closed,
        // Agregados para Incidentes (Fase C) -- se guardan como string (ver
        // BDLayout.Add.cs AddNewRecordAsync(WorkflowAuditEntry)), así que sumar
        // valores acá no rompe nada de lo ya guardado.
        Reviewed,
        Assigned,
        Resolved,
        Reopened
    }
}
