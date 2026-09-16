-- Bug encontrado durante pruebas exhaustivas de Multimoneda (2026-09-16): Conciliación
-- de Pagos/Gastos quedaba completamente rota ("Error al cargar transacciones: Operation
-- GetBankTransactionsNoConcilied failed: The required column 'Origin' was not present in
-- the results of a 'FromSql' operation.") para CUALQUIER cuenta bancaria, sin importar su
-- moneda -- no es un bug de Multimoneda en sí, pero bloqueaba probarlo.
--
-- Causa raíz: el rename Movement/Reconciliation (2026-09-15, script 116-123) renombró
-- TransactionBankDetail.Origen -> Origin del lado de C# (Classes/Movement.cs), pero nunca
-- tocó la columna real en dbo.AccountStatementDetail ni los 3 SPs que la leen/escriben --
-- se les pasó por alto porque viven en scripts previos a esa tanda de rename. Luego, el
-- script de Multimoneda Foundation (2026-09-15_105) recreó esos mismos 3 SPs (para agregar
-- AmountInReportingCurrency) reusando el nombre de columna Origen que todavía era el
-- correcto en ese momento -- así que no "revivió" nada, simplemente el rename original
-- nunca llegó a esta columna. EF Core mapea el resultado de FromSql por nombre de columna
-- exacto contra la propiedad Origin del modelo, así que "Origen" (sin traducir) rompe la
-- consulta con cualquier fila que exista.
--
-- Fix: renombrar la columna real (preserva todos los datos, no ALTER TABLE ADD+DROP) y
-- actualizar los 3 SPs que la referencian.

EXEC sp_rename 'dbo.AccountStatementDetail.Origen', 'Origin', 'COLUMN';
GO

CREATE OR ALTER PROCEDURE [dbo].[INS_AccountStatementDetail]
    @IdStatementDetail      UNIQUEIDENTIFIER,
    @IdStatementHeader      UNIQUEIDENTIFIER,
    @StatementDate          DATETIME,
    @Description            VARCHAR(200),
    @ITF                    DECIMAL(18,2),
    @Currency               VARCHAR(10),
    @Amount                 DECIMAL(18,2),
    @SequenceNumber         INT,
    @ReconciliationStatus   BIT = 0,
    @ReconciliationDate     DATETIME = NULL,
    @IdParent               UNIQUEIDENTIFIER = NULL,
    @Origin                 INT = 0
AS
BEGIN
    DECLARE @IdBankAccount UNIQUEIDENTIFIER;
    DECLARE @ExchangeRate  DECIMAL(18,6);
    DECLARE @NextSequence  INT;

    SELECT  @IdBankAccount = mh.IdBankAccount,
            @ExchangeRate  = mh.ExchangeRate
    FROM    AccountStatementHeader mh
    WHERE   mh.IdStatementHeader = @IdStatementHeader;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT  @NextSequence = ISNULL(MAX(md.SequenceNumber), 0) + 1
        FROM    AccountStatementDetail md WITH (UPDLOCK, HOLDLOCK)
        JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
        WHERE   mh.IdBankAccount = @IdBankAccount;

        INSERT INTO AccountStatementDetail
            (IdStatementDetail, IdStatementHeader, StatementDate, Description, ITF, Currency, Amount,
             SequenceNumber, ReconciliationStatus, ReconciliationDate, IdParent, Origin, AmountInReportingCurrency)
        VALUES
            (@IdStatementDetail, @IdStatementHeader, @StatementDate, @Description, @ITF, @Currency, @Amount,
             @NextSequence, @ReconciliationStatus, @ReconciliationDate, @IdParent, @Origin,
             @Amount * ISNULL(@ExchangeRate, 1));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_BankTransactionsNoConcilied]
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
            md.AmountInReportingCurrency,
            md.Currency,
            ISNULL(md.idParent, '00000000-0000-0000-0000-000000000000') AS [idParent],
            md.Origin,
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

CREATE OR ALTER PROCEDURE [dbo].[GET_TransactionBankDetailById]
    @IdStatementDetail UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  md.IdStatementDetail,
            mh.IdBankAccount,
            md.IdStatementHeader,
            ISNULL(md.IdParent, '00000000-0000-0000-0000-000000000000')      AS IdParent,
            ISNULL(id.IdGroupUnit, '00000000-0000-0000-0000-000000000000')   AS IdGroupUnit,
            md.StatementDate,
            md.Description,
            md.Amount,
            md.AmountInReportingCurrency,
            md.SequenceNumber,
            md.OriginalReference,
            md.Currency,
            md.Origin,
            md.ReconciliationStatus,
            md.ReconciliationDate,
            ISNULL(id.AmountPaid, 0)                                        AS AmountPaid,
            md.Amount - ISNULL(id.AmountPaid, 0)                            AS Balance,
            md.Ignored,
            md.IgnoredReason,
            md.IgnoredType
    FROM    AccountStatementDetail md
    JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
    LEFT JOIN VW_SUM_InstallmentPaidTransaction id ON md.IdStatementDetail = id.IdTransaction
    WHERE   md.IdStatementDetail = @IdStatementDetail
END
GO
