/*
    Catálogo de Workflows (/workflow) — documentación y seguimiento del estado de
    implementación de los flujos de negocio de la app (ej: aprobación de
    Presupuesto). No maneja lógica de negocio en vivo: BudgetStatus y el resto de
    los estados reales siguen viviendo en cada módulo. Esto es solo una tabla de
    referencia/planificación para el equipo.

    Tablas nuevas (no existían): WorkflowHeader, WorkflowStep.

    Ejecutar contra la misma base que usa la app (ver connection string
    "SpiderHoodContext" en appsettings.json).
*/

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowHeader')
BEGIN
    CREATE TABLE dbo.WorkflowHeader
    (
        IdWorkflow  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Name        NVARCHAR(150)    NOT NULL,
        Description NVARCHAR(500)    NULL,
        Status      INT              NOT NULL DEFAULT (0), -- WorkflowImplementationStatus: 0 Pendiente, 1 EnDesarrollo, 2 Implementado, 3 Descartado
        CreatedOn   DATETIME2        NOT NULL DEFAULT (GETDATE()),
        UpdatedOn   DATETIME2        NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WorkflowStep')
BEGIN
    CREATE TABLE dbo.WorkflowStep
    (
        IdWorkflowStep UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdWorkflow     UNIQUEIDENTIFIER NOT NULL,
        StepOrder      INT              NOT NULL,
        Name           NVARCHAR(150)    NOT NULL,
        Description    NVARCHAR(500)    NULL,
        Responsible    NVARCHAR(100)    NULL,
        IsImplemented  BIT              NOT NULL DEFAULT (0),
        CONSTRAINT FK_WorkflowStep_WorkflowHeader FOREIGN KEY (IdWorkflow)
            REFERENCES dbo.WorkflowHeader (IdWorkflow) ON DELETE CASCADE
    );
END
GO

CREATE OR ALTER PROCEDURE INS_Workflow
    @IdWorkflow  UNIQUEIDENTIFIER,
    @Name        NVARCHAR(150),
    @Description NVARCHAR(500),
    @Status      INT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO WorkflowHeader (IdWorkflow, Name, Description, Status, CreatedOn)
    VALUES (@IdWorkflow, @Name, @Description, @Status, GETDATE());
END
GO

CREATE OR ALTER PROCEDURE UPD_Workflow
    @IdWorkflow  UNIQUEIDENTIFIER,
    @Name        NVARCHAR(150),
    @Description NVARCHAR(500),
    @Status      INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE WorkflowHeader
    SET Name        = @Name,
        Description = @Description,
        Status      = @Status,
        UpdatedOn   = GETDATE()
    WHERE IdWorkflow = @IdWorkflow;
END
GO

CREATE OR ALTER PROCEDURE DEL_Workflow
    @IdWorkflow UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Los pasos se eliminan solos por el FK ON DELETE CASCADE.
    DELETE FROM WorkflowHeader
    WHERE IdWorkflow = @IdWorkflow;
END
GO

CREATE OR ALTER PROCEDURE GET_Workflows
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdWorkflow, Name, Description, Status, CreatedOn, UpdatedOn
    FROM WorkflowHeader
    ORDER BY Name;
END
GO

CREATE OR ALTER PROCEDURE INS_WorkflowStep
    @IdWorkflowStep UNIQUEIDENTIFIER,
    @IdWorkflow     UNIQUEIDENTIFIER,
    @StepOrder      INT,
    @Name           NVARCHAR(150),
    @Description    NVARCHAR(500),
    @Responsible    NVARCHAR(100),
    @IsImplemented  BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO WorkflowStep (IdWorkflowStep, IdWorkflow, StepOrder, Name, Description, Responsible, IsImplemented)
    VALUES (@IdWorkflowStep, @IdWorkflow, @StepOrder, @Name, @Description, @Responsible, @IsImplemented);
END
GO

CREATE OR ALTER PROCEDURE UPD_WorkflowStep
    @IdWorkflowStep UNIQUEIDENTIFIER,
    @StepOrder      INT,
    @Name           NVARCHAR(150),
    @Description    NVARCHAR(500),
    @Responsible    NVARCHAR(100),
    @IsImplemented  BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE WorkflowStep
    SET StepOrder     = @StepOrder,
        Name           = @Name,
        Description    = @Description,
        Responsible    = @Responsible,
        IsImplemented  = @IsImplemented
    WHERE IdWorkflowStep = @IdWorkflowStep;
END
GO

CREATE OR ALTER PROCEDURE DEL_WorkflowStep
    @IdWorkflowStep UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM WorkflowStep
    WHERE IdWorkflowStep = @IdWorkflowStep;
END
GO

CREATE OR ALTER PROCEDURE GET_WorkflowStepsByWorkflow
    @IdWorkflow UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT IdWorkflowStep, IdWorkflow, StepOrder, Name, Description, Responsible, IsImplemented
    FROM WorkflowStep
    WHERE IdWorkflow = @IdWorkflow
    ORDER BY StepOrder;
END
GO
