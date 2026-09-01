using SpiderHood.Models;

namespace SpiderHood.Data
{
    public partial class BDLayout
    {
        #region Audit Operations

        // Estampa CreatedBy/CreatedOn (solo si isCreate) y ModifiedBy/ModifiedOn en la
        // tabla cabecera correspondiente. Es un UPDATE separado del INSERT/UPDATE
        // "de negocio" que ya hace AddNewRecordAsync/UpdateRecordAsync -- así no hace
        // falta tocar los Stored Procedures existentes (cuyo cuerpo no vive en este
        // repo) para agregar auditoría. Ver Database/Scripts/2026-09-01_01_Audit_HeaderColumns.sql.
        public async Task StampAuditAsync(
            AuditableEntity entity,
            object idValue,
            string performedBy,
            bool isCreate,
            CancellationToken cancellationToken = default)
        {
            ValidateEntity(idValue, nameof(idValue));

            var storedProcedureName = entity switch
            {
                AuditableEntity.Building => StoredProcedures.UPD_BuildingAudit,
                AuditableEntity.Owner => StoredProcedures.UPD_OwnerAudit,
                AuditableEntity.BudgetHeader => StoredProcedures.UPD_BudgetHeaderAudit,
                AuditableEntity.Expense => StoredProcedures.UPD_ExpenseAudit,
                AuditableEntity.Period => StoredProcedures.UPD_PeriodAudit,
                AuditableEntity.ServiceReading => StoredProcedures.UPD_ServiceReadingAudit,
                AuditableEntity.BankAccount => StoredProcedures.UPD_BankAccountAudit,
                AuditableEntity.Category => StoredProcedures.UPD_CategoryAudit,
                AuditableEntity.BuildingConfiguration => StoredProcedures.UPD_BuildingConfigurationAudit,
                _ => throw new ArgumentOutOfRangeException(nameof(entity), entity, "Entidad sin Stored Procedure de auditoría asociado")
            };

            await ExecuteWithErrorHandlingAsync(async () =>
            {
                await ExecuteStoredProcedureAsync(
                    storedProcedureName,
                    cancellationToken,
                    idValue,
                    performedBy,
                    isCreate);
                return true;
            }, $"StampAudit_{entity}", cancellationToken);
        }

        #endregion
    }
}
