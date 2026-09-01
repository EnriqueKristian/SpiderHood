-- =============================================================================
-- Auditoria de Workflow: quien aprueba/rechaza/publica (y cuando).
--
-- Tabla y SP 100% nuevos -- no toca nada existente. Se llena desde la app en
-- las transiciones de estado que ya existen (por ahora: Presupuesto, ver
-- BudgetGenerator.razor). Idempotente: se puede correr mas de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowAuditLog')
BEGIN
    CREATE TABLE dbo.WorkflowAuditLog
    (
        Id             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Module         NVARCHAR(50)     NOT NULL,   -- p.ej. "Budget"
        EntityId       UNIQUEIDENTIFIER NOT NULL,    -- p.ej. IdBudgetHeader
        Action         NVARCHAR(30)     NOT NULL,    -- Submitted/Approved/Rejected/Published/Closed
        PerformedBy    NVARCHAR(256)    NOT NULL,
        PerformedOn    DATETIME2        NOT NULL,
        Comment        NVARCHAR(500)    NULL,
        IdBuilding     UNIQUEIDENTIFIER NOT NULL
    );

    CREATE INDEX IX_WorkflowAuditLog_Module_EntityId
        ON dbo.WorkflowAuditLog (Module, EntityId);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_WorkflowAuditLog
    @Id UNIQUEIDENTIFIER,
    @Module NVARCHAR(50),
    @EntityId UNIQUEIDENTIFIER,
    @Action NVARCHAR(30),
    @PerformedBy NVARCHAR(256),
    @Comment NVARCHAR(500) = NULL,
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.WorkflowAuditLog (Id, Module, EntityId, Action, PerformedBy, PerformedOn, Comment, IdBuilding)
    VALUES (@Id, @Module, @EntityId, @Action, @PerformedBy, SYSUTCDATETIME(), @Comment, @IdBuilding);
END
GO
