-- =============================================================================
-- Cambia Incident.Type e Incident.Priority de NVARCHAR (texto del enum viejo:
-- "Plumbing", "Urgent", etc.) a INT (Parameter.Value dentro de los grupos
-- "Tipo Incidente"/"Prioridad Incidente" -- ver
-- 2026-09-02_14_Seed_IncidentTypeAndPriorityParameters.sql, que tiene que
-- haberse corrido ANTES que este script).
--
-- Corte limpio, sin migración de datos: se confirmó que no hay Incidentes
-- reales que conservar todavía. Si ya tenés alguno cargado en tu QA, se va a
-- perder su Type/Priority (quedan en 0, sin texto -- no rompe, pero conviene
-- borrarlos y recrearlos después de correr esto).
--
-- Idempotente en la parte de columnas (chequea el tipo de dato antes de
-- tocarlas). Los CREATE OR ALTER PROCEDURE son idempotentes por naturaleza.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Columnas: Type/Priority de NVARCHAR a INT
-- -----------------------------------------------------------------------------

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.Incident') AND c.name = 'Type' AND t.name <> 'int'
)
BEGIN
    ALTER TABLE dbo.Incident DROP COLUMN Type;
    ALTER TABLE dbo.Incident ADD Type INT NOT NULL DEFAULT (8); -- 8 = "Otro" en la seed
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.Incident') AND c.name = 'Priority' AND t.name <> 'int'
)
BEGIN
    ALTER TABLE dbo.Incident DROP COLUMN Priority;
    ALTER TABLE dbo.Incident ADD Priority INT NOT NULL DEFAULT (2); -- 2 = "Media" en la seed
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures: agregan LEFT JOIN a Parameter para traer
--    TypeName/PriorityName, y reciben @Type/@Priority como INT.
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Incident
    @IdIncident UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @Type INT,
    @Priority INT,
    @Status NVARCHAR(20),
    @IdGroupUnit UNIQUEIDENTIFIER = NULL,
    @ReportedBy UNIQUEIDENTIFIER,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Incident
        (IdIncident, IdBuilding, Title, Description, Type, Priority, Status, IdGroupUnit, ReportedBy, AssignedTo, ResolvedOn, ClosedOn, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn)
    VALUES
        (@IdIncident, @IdBuilding, @Title, @Description, @Type, @Priority, @Status, @IdGroupUnit, @ReportedBy, NULL, NULL, NULL, @CreatedBy, SYSUTCDATETIME(), NULL, NULL);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Prioridad Incidente');

    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           gu.GroupName AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
    LEFT JOIN dbo.Parameter typeParam ON typeParam.IdParent = @IdTipoIncidente AND typeParam.Value = i.Type
    LEFT JOIN dbo.Parameter priorityParam ON priorityParam.IdParent = @IdPrioridadIncidente AND priorityParam.Value = i.Priority
    WHERE i.IdBuilding = @IdBuilding
    ORDER BY i.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByReporter
    @ReportedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Prioridad Incidente');

    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           gu.GroupName AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
    LEFT JOIN dbo.Parameter typeParam ON typeParam.IdParent = @IdTipoIncidente AND typeParam.Value = i.Type
    LEFT JOIN dbo.Parameter priorityParam ON priorityParam.IdParent = @IdPrioridadIncidente AND priorityParam.Value = i.Priority
    WHERE i.ReportedBy = @ReportedBy
    ORDER BY i.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentById
    @IdIncident UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent = 0 AND ShortDescription = N'Prioridad Incidente');

    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           gu.GroupName AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
    LEFT JOIN dbo.Parameter typeParam ON typeParam.IdParent = @IdTipoIncidente AND typeParam.Value = i.Type
    LEFT JOIN dbo.Parameter priorityParam ON priorityParam.IdParent = @IdPrioridadIncidente AND priorityParam.Value = i.Priority
    WHERE i.IdIncident = @IdIncident;
END
GO
