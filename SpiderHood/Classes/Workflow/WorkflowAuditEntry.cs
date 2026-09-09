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
        Reopened,
        // Agregados para Conciliación en dos pasos (Fase B) -- mismo motivo que arriba,
        // se guarda como string, no rompe historial existente.
        Reconciled,
        Corrected,
        // Agregado para el borrado de edificios de prueba (SysAdmin, Docs/Pendientes-
        // Negocio-Migracion.md #6.3) -- mismo motivo que arriba, se guarda como string.
        Deleted,
        // Agregado para "Marcar como Saldo Inicial" en Conciliación (Docs/Pendientes-
        // Negocio-Migracion.md #3) -- mismo motivo que arriba.
        InitialBalanceSet,
        // Agregado para "Ignorar transacción" con motivo + tipo (Docs/Pendientes-
        // Negocio-Conciliacion.md #1) -- mismo motivo que arriba.
        Ignored,
        // Agregado para Lecturas de Agua (BlockWaterReading.razor.GuardarLecturas) --
        // antes no dejaba ningún registro de quién cargó o modificó las lecturas de un
        // período, a diferencia del resto de los guardados de la app. Mismo motivo que
        // arriba, se guarda como string.
        WaterReadingSaved
    }
}
