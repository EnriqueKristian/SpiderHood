-- =============================================================================
-- Fase 2 de Aprobaciones: columna nueva ExpenseApprovalThreshold en
-- dbo.BuildingConfiguration (monto a partir del cual un gasto manual requiere
-- aprobación de la Junta -- NULL = nunca requiere aprobación) + una SP chica y
-- aparte para guardarla.
--
-- Se agrega con su propia SP (UPD_BuildingConfiguration_ExpenseThreshold) en vez
-- de sumarla a los parámetros de UPD_BuildingConfiguration a propósito: esa SP
-- recibe sus ~16 parámetros POSICIONALMENTE desde el código -- agregar uno ahí
-- exige tocar la llamada en BDLayout.Update.cs Y esta SP en el mismo orden
-- exacto, sin que nada avise si se desalinean (ya nos pasó con otras SPs este
-- mes). Una SP nueva y chica sólo para este campo no corre ese riesgo y no
-- necesita conocer los otros 16 parámetros.
--
-- GET_BuildingConfiguration ya usa mapeo automático por nombre de columna
-- (ExecuteQueryListAsync<BuildingConfiguration> hace SELECT ... FromSqlRaw, no
-- posicional) -- SI esa SP hace "SELECT *" no hace falta tocarla, la columna
-- nueva aparece sola. Si en cambio lista las columnas a mano, hay que agregar
-- ExpenseApprovalThreshold a esa lista -- correr primero el bloque de
-- diagnóstico de abajo para saber cuál de los dos casos es.
-- =============================================================================

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'ExpenseApprovalThreshold'
)
    ALTER TABLE dbo.BuildingConfiguration ADD ExpenseApprovalThreshold DECIMAL(18,2) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BuildingConfiguration_ExpenseThreshold
    @IdBuildingConfiguration UNIQUEIDENTIFIER,
    @ExpenseApprovalThreshold DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BuildingConfiguration
    SET ExpenseApprovalThreshold = @ExpenseApprovalThreshold
    WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
END
GO

-- --- Diagnóstico: pegame este resultado si GET_BuildingConfiguration no es
-- --- "SELECT *" -- hace falta un segundo ajuste puntual a esa SP.
PRINT '--- Texto de GET_BuildingConfiguration (revisar si es SELECT * o columnas explícitas) ---';
EXEC sp_helptext 'GET_BuildingConfiguration';
