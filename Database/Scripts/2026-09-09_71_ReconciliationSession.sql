-- =============================================================================
-- Docs/Pendientes-Negocio-Conciliacion.md #3 -- "Última Conciliación" mostraba
-- datos INVENTADOS y fijos (BankAccountService.ObtenerUltimaConciliacionAsync
-- era un stub que devolvía siempre el mismo registro de ejemplo, sin tocar la
-- BD). El registro por TRANSACCIÓN ya es real (WorkflowAuditLog, module
-- "Reconciliation"), pero no tiene forma de agrupar qué transacciones se
-- procesaron juntas en un mismo "Finalizar Conciliación" -- hace falta una
-- tabla propia para la sesión completa (fecha, cuántas transacciones,
-- diferencia).
--
-- Tabla y SPs 100% nuevos -- no toca nada existente. Idempotente: se puede
-- correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReconciliationSession')
BEGIN
    CREATE TABLE dbo.ReconciliationSession
    (
        Id                          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBankAccount               UNIQUEIDENTIFIER NOT NULL,
        IdBuilding                  UNIQUEIDENTIFIER NOT NULL,
        FechaInicio                 DATETIME2        NOT NULL,
        FechaFin                    DATETIME2        NOT NULL,
        TransaccionesProcesadas     INT              NOT NULL,
        TransaccionesConciliadas    INT              NOT NULL,
        Diferencia                  DECIMAL(18,2)    NOT NULL,
        Completada                  BIT              NOT NULL,
        Fecha                       DATETIME2        NOT NULL,
        Usuario                     NVARCHAR(256)    NOT NULL,
        Notas                       NVARCHAR(500)    NULL
    );

    CREATE INDEX IX_ReconciliationSession_BankAccount_Fecha
        ON dbo.ReconciliationSession (IdBankAccount, Fecha DESC);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_ReconciliationSession
    @Id UNIQUEIDENTIFIER,
    @IdBankAccount UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FechaInicio DATETIME2,
    @FechaFin DATETIME2,
    @TransaccionesProcesadas INT,
    @TransaccionesConciliadas INT,
    @Diferencia DECIMAL(18,2),
    @Completada BIT,
    @Fecha DATETIME2,
    @Usuario NVARCHAR(256),
    @Notas NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReconciliationSession
        (Id, IdBankAccount, IdBuilding, FechaInicio, FechaFin, TransaccionesProcesadas,
         TransaccionesConciliadas, Diferencia, Completada, Fecha, Usuario, Notas)
    VALUES
        (@Id, @IdBankAccount, @IdBuilding, @FechaInicio, @FechaFin, @TransaccionesProcesadas,
         @TransaccionesConciliadas, @Diferencia, @Completada, @Fecha, @Usuario, @Notas);
END
GO

-- La más reciente por Fecha (cuándo se hizo click en "Finalizar Conciliación"),
-- no por FechaFin (fin del rango de fechas filtrado) -- son cosas distintas.
CREATE OR ALTER PROCEDURE dbo.GET_LastReconciliationSession
    @IdBankAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 *
    FROM dbo.ReconciliationSession
    WHERE IdBankAccount = @IdBankAccount
    ORDER BY Fecha DESC;
END
GO
