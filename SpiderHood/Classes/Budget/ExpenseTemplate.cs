namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Conciliacion.md #5 -- "Guardar como plantilla para
    // transacciones similares" en CreateExpenseFromTransactionModal.razor. Match por
    // prefijo: una transacción nueva "es de esta plantilla" si su Description empieza
    // con DescriptionPattern (case-insensitive) -- ver
    // IExpenseTemplateService.BuscarPlantillaAsync.
    public class ExpenseTemplate
    {
        public Guid IdExpenseTemplate { get; set; } = Guid.NewGuid();
        public Guid IdBuilding { get; set; }
        public string DescriptionPattern { get; set; } = string.Empty;
        public Guid IdCategory { get; set; }
        public TypeDistribution Distribution { get; set; }
        public string? Supplier { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
