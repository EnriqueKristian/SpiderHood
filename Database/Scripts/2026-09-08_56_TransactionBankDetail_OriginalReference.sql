-- =============================================================================
-- Migración de datos históricos (Services/IMigrationImportService.cs) -- agrega
-- una referencia externa opcional a cada movimiento bancario importado, para
-- poder enlazar de verdad los pagos migrados de "Cuotas y Pagos" contra el
-- movimiento real de "Estado de Cuenta" que los pagó (hoy quedan sin vincular,
-- ver Docs/Pendientes-Negocio-Migracion.md #7).
--
-- IMPORTANTE -- nombres de tabla: la clase C# se llama TransactionBankDetail,
-- pero el Insert real (BDLayout.Add.cs) usa el Stored Procedure
-- INS_AccountStatementDetail (cabecera: INS_MovementHeader) -- asumo que las
-- tablas reales se llaman dbo.AccountStatementDetail y dbo.MovementHeader.
-- Revisa contra tu diagrama real antes de correr y ajusta los nombres si no
-- coinciden (mismo caso que Owner/ApartmentOwner y Period/Periods en scripts
-- anteriores).
--
-- Alcance -- solo migración, no toca nada de uso diario:
--   - La columna nueva (OriginalReference) no la lee ni la escribe ninguna
--     pantalla ni Stored Procedure existente (INS_/UPD/GET_AccountStatement...,
--     GET_BankTransactionsNoConcilied, etc.) -- queda NULL para toda fila que
--     no venga de una migración.
--   - Los 2 Stored Procedures nuevos (UPD_.../GET_..._ByOriginalReference) los
--     usa exclusivamente IMigrationImportService.
--
-- AccountStatementDetail no guarda IdBankAccount directo -- se filtra por
-- cuenta a través de MovementHeader.IdStatementHeader (igual que ya hace
-- GET_BankTransactionsNoConcilied/GET_MovementHeaders).
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'OriginalReference')
    ALTER TABLE dbo.AccountStatementDetail ADD OriginalReference NVARCHAR(50) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_TransactionBankDetail_OriginalReference
    @IdStatementDetail UNIQUEIDENTIFIER,
    @OriginalReference NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AccountStatementDetail
    SET OriginalReference = @OriginalReference
    WHERE IdStatementDetail = @IdStatementDetail;
END
GO

-- Devuelve SOLO el Guid (no todas las columnas) -- el C# lo consume vía
-- SqlQueryRaw<Guid>, no vía el mapeo estricto de FromSqlRaw<TransactionBankDetail>
-- (que exigiría reproducir acá la lista completa de columnas que EF mapeó para
-- esa entidad, incluidas las que llegan por JOIN, sin poder confirmarla contra
-- el diagrama real).
CREATE OR ALTER PROCEDURE dbo.GET_TransactionBankDetail_ByOriginalReference
    @IdBankAccount UNIQUEIDENTIFIER,
    @OriginalReference NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 d.IdStatementDetail
    FROM dbo.AccountStatementDetail d
    INNER JOIN dbo.MovementHeader h ON h.IdStatementHeader = d.IdStatementHeader
    WHERE h.IdBankAccount = @IdBankAccount
      AND d.OriginalReference = @OriginalReference;
END
GO
