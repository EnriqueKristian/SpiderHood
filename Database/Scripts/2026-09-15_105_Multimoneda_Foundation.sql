-- =============================================================================
-- Multimoneda -- fundación (Docs/Pendientes-Negocio-Consolidado.md #5,
-- Docs/Pendientes-Negocio-Conciliacion.md #10). Alcance acordado con el
-- usuario: moneda de reporte fija por edificio (como hoy) + cada Cuenta
-- Bancaria puede estar en una moneda distinta, con un tipo de cambio por
-- LOTE de carga (no por fila del Excel, no tabla de catálogo aparte) para
-- convertir a la moneda de reporte. Para el caso más común (todo en la
-- misma moneda) esto es 100% transparente: ExchangeRate queda NULL,
-- AmountInReportingCurrency = Amount siempre.
--
-- 1. BankAccount.Currency (nueva) -- moneda de la cuenta, default = moneda
--    del edificio al crearla. Backfill: cuentas existentes heredan
--    BuildingConfiguration.Currency. Inmutable después de creada (mismo
--    criterio que InitialBalance -- UPD_BankAccount no la toca).
-- 2. AccountStatementHeader.ExchangeRate (nueva, nullable) -- tipo de
--    cambio real que aplicó el banco a ESE lote completo, sólo se pide en
--    el formulario de carga cuando la cuenta es de otra moneda que el
--    edificio.
-- 3. AccountStatementDetail.AmountInReportingCurrency (nueva) -- calculada
--    server-side en INS_AccountStatementDetail (= Amount * ExchangeRate si
--    el lote tiene tipo de cambio, si no = Amount). Es la columna que usan
--    Conciliación/Reportes/Dashboard para sumar de ahora en más -- Amount
--    se sigue mostrando tal cual para que cuadre contra el estado de
--    cuenta real del banco.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------
-- 1. BankAccount.Currency
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'Currency')
    ALTER TABLE dbo.BankAccount ADD Currency NVARCHAR(3) NOT NULL CONSTRAINT DF_BankAccount_Currency DEFAULT ('PEN');
GO

UPDATE ba
SET ba.Currency = ISNULL(bc.Currency, 'PEN')
FROM dbo.BankAccount ba
LEFT JOIN dbo.BuildingConfiguration bc ON bc.IdBuilding = ba.IdBuilding
WHERE ba.Currency = 'PEN'; -- sólo las que todavía están en el default recién agregado
GO

-- ---------------------------------------------------------------------------
-- 2. AccountStatementHeader.ExchangeRate
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementHeader') AND name = 'ExchangeRate')
    ALTER TABLE dbo.AccountStatementHeader ADD ExchangeRate DECIMAL(18, 6) NULL;
GO

-- ---------------------------------------------------------------------------
-- 3. AccountStatementDetail.AmountInReportingCurrency
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'AmountInReportingCurrency')
    ALTER TABLE dbo.AccountStatementDetail ADD AmountInReportingCurrency DECIMAL(18, 2) NOT NULL CONSTRAINT DF_ASD_AmountInReportingCurrency DEFAULT (0);
GO

UPDATE dbo.AccountStatementDetail
SET AmountInReportingCurrency = Amount
WHERE AmountInReportingCurrency = 0; -- backfill: hasta hoy todo estaba 1:1 en la misma moneda
GO

-- ---------------------------------------------------------------------------
-- SPs: alta de Cuenta Bancaria
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.INS_BankAccount
    @IdBankAccount      UNIQUEIDENTIFIER,
    @AccountName        NVARCHAR(100),
    @AccountNumber      NVARCHAR(30),
    @BankName           NVARCHAR(50),
    @AccountType        INT,
    @IdBuilding         UNIQUEIDENTIFIER,
    @Status             INT = NULL,
    @CCI                NVARCHAR(30) = NULL,
    @InitialBalance     DECIMAL(18, 2) = 0,
    @Currency           NVARCHAR(3) = 'PEN'
AS
BEGIN
    INSERT INTO dbo.BankAccount
        (IdBankAccount, AccountName, AccountNumber, BankName, AccountType, IdBuilding, Status, CCI,
         InitialBalance, CurrentBalance, ReconciledBalance, LastReconciliation, Currency)
    VALUES
        (@IdBankAccount, @AccountName, @AccountNumber, @BankName, @AccountType, @IdBuilding, @Status, @CCI,
         @InitialBalance, 0, 0, NULL, @Currency);
END;
GO

CREATE OR ALTER PROCEDURE dbo.GET_BankAccountsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdBankAccount,
            AccountName,
            AccountNumber,
            BankName,
            AccountType,
            InitialBalance,
            CurrentBalance,
            ReconciledBalance,
            LastReconciliation,
            IdBuilding,
            Status,
            CCI,
            Currency
    FROM    BankAccount b
    WHERE   IdBuilding = @IdBuilding
END
GO

-- ---------------------------------------------------------------------------
-- SPs: carga de Estado de Cuenta (header con tipo de cambio del lote)
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[INS_MovementHeader]
    @IdMovHeader    UNIQUEIDENTIFIER,
    @FileName       VARCHAR(200),
    @IdUser         UNIQUEIDENTIFIER,
    @TotalRecords   INT,
    @UploadState    INT,
    @IdBankAccount  UNIQUEIDENTIFIER,
    @ExchangeRate   DECIMAL(18, 6) = NULL
AS
BEGIN
    INSERT INTO AccountStatementHeader (IdStatementHeader, FileName, UploadDate, IdUser, TotalRecords, UploadState, IdBankAccount, ExchangeRate)
    VALUES (@IdMovHeader, @FileName, GETDATE(), @IdUser, @TotalRecords, @UploadState, @IdBankAccount, @ExchangeRate)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_MovementHeaders]
    @IdBuilding     UNIQUEIDENTIFIER,
    @IdBankAccount  UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  h.IdStatementHeader,
            h.FileName,
            h.UploadDate,
            h.IdUser,
            h.TotalRecords,
            h.UploadState,
            h.IdBankAccount,
            h.ExchangeRate
    FROM    dbo.AccountStatementHeader h
    INNER JOIN dbo.BankAccount b ON b.IdBankAccount = h.IdBankAccount
    WHERE   b.IdBuilding = @IdBuilding
      AND   (@IdBankAccount IS NULL OR h.IdBankAccount = @IdBankAccount)
    ORDER BY h.UploadDate DESC;
END
GO
-- GET_MovementByName usa SELECT * sobre AccountStatementHeader -- no hace
-- falta tocarlo, ExchangeRate ya viaja solo apenas se agrega la columna.

-- ---------------------------------------------------------------------------
-- SPs: detalle de transacciones (conversión aplicada al insertar)
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.INS_AccountStatementDetail
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
    @Origen                 INT = 0
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
             SequenceNumber, ReconciliationStatus, ReconciliationDate, IdParent, Origen, AmountInReportingCurrency)
        VALUES
            (@IdStatementDetail, @IdStatementHeader, @StatementDate, @Description, @ITF, @Currency, @Amount,
             @NextSequence, @ReconciliationStatus, @ReconciliationDate, @IdParent, @Origen,
             @Amount * ISNULL(@ExchangeRate, 1));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

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
            md.AmountInReportingCurrency,
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

CREATE OR ALTER PROCEDURE dbo.GET_TransactionBankDetailById
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
            md.Origen,
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

CREATE OR ALTER PROCEDURE [dbo].[GET_AccountStatementDetailByHeader]
    @IdStatementHeader UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.IdStatementDetail,
            d.IdStatementHeader,
            d.StatementDate,
            d.Description,
            d.Currency,
            d.Amount,
            d.AmountInReportingCurrency,
            d.SequenceNumber,
            d.ReconciliationStatus,
            d.ReconciliationDate
    FROM    dbo.AccountStatementDetail d
    WHERE   d.IdStatementHeader = @IdStatementHeader
    ORDER BY d.StatementDate, d.SequenceNumber;
END
GO
