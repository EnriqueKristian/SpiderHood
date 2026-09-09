-- =============================================================================
-- Docs/Pendientes-Negocio-Conciliacion.md #1 -- "Ignorar transacción" necesita
-- motivo + tipo, no solo un flag.
--
-- Encontrado al implementar esto: "Ignorar" NO HACÍA NADA de verdad hasta ahora --
-- ni siquiera guardaba el flag booleano:
--   - TransactionBankDetail.Ignored era [NotMapped] (Classes/Movement.cs) -- no
--     existía la columna en dbo.AccountStatementDetail.
--   - GET_BankTransactionsNoConcilied devolvía la columna Ignored como el literal
--     @FALSE, no leída de ninguna tabla (ver
--     2026-09-08_59_Fix_GET_BankTransactionsNoConcilied_OriginalReference.sql, que
--     trae el texto completo del proc de donde sale este script).
--   - IBankAccountService.MarcarTransaccionComoIgnoradaAsync era un stub
--     (Task.Delay + Console.WriteLine, sin tocar la BD).
--   - Ninguna pantalla leía transaccion.Ignored para filtrar ni mostrar nada.
-- En los hechos, "Ignorar" no persistía ni ocultaba nada -- recargar la página
-- hacía que la transacción "ignorada" volviera a aparecer como pendiente. Este
-- script agrega la persistencia real (columnas + SP de escritura) que hacía
-- falta antes de poder agregarle motivo/tipo.
--
-- Idempotente: agrega columnas sólo si no existen; los CREATE OR ALTER PROCEDURE
-- no fallan si el proc ya existe.
--
-- Ejecutar contra la base de datos de la app (ver DEPLOY-Production.md §2).
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'Ignored')
    ALTER TABLE dbo.AccountStatementDetail ADD Ignored BIT NOT NULL CONSTRAINT DF_AccountStatementDetail_Ignored DEFAULT (0);
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'IgnoredReason')
    ALTER TABLE dbo.AccountStatementDetail ADD IgnoredReason NVARCHAR(500) NULL;
GO

-- Coincide con Classes/Movement.cs (IgnoredReasonType): 1=ErrorBancario,
-- 2=DepositoRevertido, 3=Otro. Sin FK/CHECK hacia un catálogo -- es un enum C#
-- fijo, no una tabla de Parameter (ver el comentario en Classes/Movement.cs).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'IgnoredType')
    ALTER TABLE dbo.AccountStatementDetail ADD IgnoredType INT NULL;
GO

-- Mismo texto que 2026-09-08_59_Fix_GET_BankTransactionsNoConcilied_OriginalReference.sql
-- (última versión conocida del proc), reemplazando el literal @FALSE Ignored por las
-- columnas reales.
CREATE OR ALTER PROCEDURE dbo.GET_BankTransactionsNoConcilied
    @IdBankAccount  UNIQUEIDENTIFIER,
    @StarDate       DATETIME = NULL,
    @EndDate        DATETIME = NULL
AS
BEGIN

    SELECT  md.IdStatementDetail,
            md.IdStatementHeader,
            mh.IdBankAccount,
            md.StatementDate,
            md.Description,
            md.Amount,
            md.Currency,
            ISNULL(md.idParent, '00000000-0000-0000-0000-000000000000') AS [idParent],
            md.Origen,
            md.SequenceNumber,
            md.OriginalReference,
            md.ReconciliationStatus,
            md.ReconciliationDate,
            md.Ignored,
            md.IgnoredReason,
            md.IgnoredType,
            CAST(0 AS BIT)              Selected,
            ISNULL(id.AmountPaid, 0)               AS [AmountPaid],
            md.Amount - ISNULL(id.AmountPaid, 0)   AS [Balance],
            ISNULL(id.IdGroupUnit, '00000000-0000-0000-0000-000000000000')    AS [IdGroupUnit]
    FROM    AccountStatementDetail md
    JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
    LEFT JOIN VW_SUM_InstallmentPaidTransaction id ON md.IdStatementDetail = id.IdTransaction
    WHERE   mh.IdBankAccount = @IdBankAccount
            AND (@StarDate IS NULL OR md.StatementDate >= @StarDate)
            AND (@EndDate   IS NULL OR md.StatementDate <= @EndDate)
    ORDER BY md.StatementDate
END;
GO

-- Reemplaza el stub de IBankAccountService.MarcarTransaccionComoIgnoradaAsync.
CREATE OR ALTER PROCEDURE dbo.UPD_AccountStatementDetail_Ignored
    @IdStatementDetail  UNIQUEIDENTIFIER,
    @Ignored            BIT,
    @IgnoredReason      NVARCHAR(500) = NULL,
    @IgnoredType        INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.AccountStatementDetail
    SET Ignored = @Ignored,
        IgnoredReason = @IgnoredReason,
        IgnoredType = @IgnoredType
    WHERE IdStatementDetail = @IdStatementDetail;
END
GO
