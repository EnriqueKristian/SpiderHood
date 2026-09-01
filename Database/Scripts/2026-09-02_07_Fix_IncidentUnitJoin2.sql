-- =============================================================================
-- Reconecta UnitName en las consultas de Incidentes contra el esquema real de
-- dbo.GroupUnit (confirmado por el usuario): PK IdGroupUnit, y el número de
-- unidad vive en GroupNumber (int), no en un IdUnit/GroupName que no existen
-- (ver 2026-09-02_06_Fix_IncidentUnitJoin.sql, que había sacado el JOIN).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
    WHERE i.IdBuilding = @IdBuilding
    ORDER BY i.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByReporter
    @ReportedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
    WHERE i.ReportedBy = @ReportedBy
    ORDER BY i.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentById
    @IdIncident UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.IdIncident, i.IdBuilding, i.Title, i.Description, i.Type, i.Priority, i.Status,
           i.IdGroupUnit, i.ReportedBy, i.AssignedTo, i.ResolvedOn, i.ClosedOn,
           i.CreatedBy, i.CreatedOn, i.ModifiedBy, i.ModifiedOn,
           reporter.FirstName + ' ' + reporter.LastName AS ReportedByName,
           assignee.FirstName + ' ' + assignee.LastName AS AssignedToName,
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
    WHERE i.IdIncident = @IdIncident;
END
GO
