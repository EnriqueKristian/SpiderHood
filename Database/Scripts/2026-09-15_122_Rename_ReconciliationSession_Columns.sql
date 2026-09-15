-- =============================================================================
-- Continua el rename sistemático de Spanish->English (ver
-- 2026-09-15_118/119/121_Rename_*_To_English.sql) -- esta vez sobre
-- dbo.ReconciliationSession (Classes/Budget/Conciliacion.cs -> Reconciliation.cs,
-- clase Conciliacion -> ReconciliationSession), que ya tenía tabla y SPs con
-- nombre en inglés pero columnas en español.
--
-- Test-only environment, sin datos reales que preservar -- DROP+CREATE, mismo
-- criterio que las fases anteriores. Verificado: 0 filas en la tabla antes de
-- correr este script.
-- =============================================================================

SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.INS_ReconciliationSession', 'P') IS NOT NULL DROP PROCEDURE dbo.INS_ReconciliationSession;
IF OBJECT_ID('dbo.GET_LastReconciliationSession', 'P') IS NOT NULL DROP PROCEDURE dbo.GET_LastReconciliationSession;
IF OBJECT_ID('dbo.ReconciliationSession', 'U') IS NOT NULL DROP TABLE dbo.ReconciliationSession;
GO

CREATE TABLE dbo.ReconciliationSession
(
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBankAccount           UNIQUEIDENTIFIER NOT NULL,
    IdBuilding              UNIQUEIDENTIFIER NOT NULL,
    StartDate               DATETIME2        NOT NULL,
    EndDate                 DATETIME2        NOT NULL,
    ProcessedTransactions   INT              NOT NULL,
    ReconciledTransactions  INT              NOT NULL,
    Difference              DECIMAL(18,2)    NOT NULL,
    Completed               BIT              NOT NULL,
    [Date]                  DATETIME2        NOT NULL,
    PerformedBy             NVARCHAR(256)    NOT NULL,
    Notes                   NVARCHAR(500)    NULL
);

CREATE INDEX IX_ReconciliationSession_BankAccount_Date
    ON dbo.ReconciliationSession (IdBankAccount, [Date] DESC);
GO

CREATE OR ALTER PROCEDURE dbo.INS_ReconciliationSession
    @Id UNIQUEIDENTIFIER,
    @IdBankAccount UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @StartDate DATETIME2,
    @EndDate DATETIME2,
    @ProcessedTransactions INT,
    @ReconciledTransactions INT,
    @Difference DECIMAL(18,2),
    @Completed BIT,
    @Date DATETIME2,
    @PerformedBy NVARCHAR(256),
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReconciliationSession
        (Id, IdBankAccount, IdBuilding, StartDate, EndDate, ProcessedTransactions,
         ReconciledTransactions, Difference, Completed, [Date], PerformedBy, Notes)
    VALUES
        (@Id, @IdBankAccount, @IdBuilding, @StartDate, @EndDate, @ProcessedTransactions,
         @ReconciledTransactions, @Difference, @Completed, @Date, @PerformedBy, @Notes);
END
GO

-- La más reciente por [Date] (cuándo se hizo click en "Finalizar Conciliación"),
-- no por EndDate (fin del rango de fechas filtrado) -- son cosas distintas.
CREATE OR ALTER PROCEDURE dbo.GET_LastReconciliationSession
    @IdBankAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 *
    FROM dbo.ReconciliationSession
    WHERE IdBankAccount = @IdBankAccount
    ORDER BY [Date] DESC;
END
GO
