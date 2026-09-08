-- =============================================================================
-- Fix: crear un presupuesto (BudgetService.CreatePresupuestoAsync) tumbaba con
-- "String or binary data would be truncated" en UPD_BudgetHeaderAudit -- no es
-- un bug del proc de auditoría (su @PerformedBy ya es NVARCHAR(256), ver
-- 2026-09-01_01_Audit_HeaderColumns.sql), sino que dbo.BudgetHeader.CreatedBy
-- es una columna original del esquema (de antes de la auditoría agregada en
-- este repo, mucho más angosta -- confirmado en producción: un email de
-- 25 caracteres se truncaba a los primeros 20). Cualquier usuario cuyo email
-- pase ese límite no puede crear presupuestos, migración de datos históricos
-- incluida (Services/IMigrationImportService.cs).
--
-- Se amplía a NVARCHAR(256) para que coincida con ModifiedBy (agregado ya así
-- en 2026-09-01_01_Audit_HeaderColumns.sql) y con el resto de columnas
-- CreatedBy/ModifiedBy de auditoría en toda la app.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BudgetHeader') AND name = 'CreatedBy' AND max_length <> -1 AND max_length < 512
)
    ALTER TABLE dbo.BudgetHeader ALTER COLUMN CreatedBy NVARCHAR(256) NULL;
GO
