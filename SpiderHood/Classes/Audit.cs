namespace SpiderHood.Models
{
    // Tablas cabecera que ya tienen columnas CreatedBy/CreatedOn/ModifiedBy/ModifiedOn
    // (ver Database/Scripts/2026-09-01_01_Audit_HeaderColumns.sql) y pueden usarse con
    // BDLayout.StampAuditAsync. Ampliar esta lista (y el script SQL correspondiente) es
    // el patrón para sumar más cabeceras a auditoría.
    public enum AuditableEntity
    {
        Building,
        Owner,
        BudgetHeader,
        Expense,
        Period,
        ServiceReading,
        BankAccount,
        Category,
        BuildingConfiguration
    }
}
