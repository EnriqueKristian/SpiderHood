-- =============================================================================
-- Fix: GET_BankTransactionsNoConcilied no traía la columna OriginalReference,
-- que Classes/Movement.cs (TransactionBankDetail) sí tiene mapeada desde
-- Database/Scripts/2026-09-08_56_TransactionBankDetail_OriginalReference.sql --
-- FromSqlRaw<TransactionBankDetail> exige que el SELECT devuelva TODAS las
-- columnas mapeadas de la entidad, así que cualquier llamada a este SP tiraba
-- "The required column 'OriginalReference' was not present in the results of a
-- 'FromSql' operation" -- rompía por completo la pantalla de Conciliación
-- (ReconciliationWorkspace.razor), no sólo el importador que originalmente
-- agregó la columna.
--
-- Se preserva el resto del procedure tal cual está hoy en producción (sin el
-- filtro ReconciliationStatus = 1 hardcodeado, con el filtro de fecha activo)
-- -- sólo se agrega md.OriginalReference al SELECT.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.GET_BankTransactionsNoConcilied
    @IdBankAccount  UNIQUEIDENTIFIER,
    @StarDate       DATETIME = NULL,
    @EndDate        DATETIME = NULL
AS
BEGIN

    DECLARE @FALSE BIT
    SET     @FALSE = 0

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
            @FALSE                      Ignored,
            @FALSE                      Selected,
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
