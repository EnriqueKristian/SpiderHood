-- =============================================================================
-- Fix sobre 2026-09-02_15: al reescribir GET_IncidentsByBuilding/ByReporter/
-- ById para agregar Type/Priority como Parameter, se pisó por error el JOIN
-- a GroupUnit con la versión VIEJA y rota (gu.IdUnit, gu.GroupName) en vez
-- de la que ya habíamos corregido en 2026-09-02_06/07 de esta misma sesión
-- (gu.IdGroupUnit, gu.GroupNumber). Esto tronaba con "Invalid column name
-- 'IdUnit'" apenas se ejecutaba el SP -- CREATE OR ALTER no valida nombres
-- de columna al crearse (resolución diferida de SQL Server), así que el
-- error recién aparece al usarlo.
--
-- Este script solo repone el JOIN correcto -- nada de Type/Priority/Parameter
-- cambia acá, eso ya quedó bien en 2026-09-02_15.
-- =============================================================================

SET NOCOUNT ON;
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
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
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
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
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
           CAST(gu.GroupNumber AS NVARCHAR(20)) AS UnitName,
           typeParam.ShortDescription AS TypeName,
           priorityParam.ShortDescription AS PriorityName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdGroupUnit = i.IdGroupUnit
    LEFT JOIN dbo.Parameter typeParam ON typeParam.IdParent = @IdTipoIncidente AND typeParam.Value = i.Type
    LEFT JOIN dbo.Parameter priorityParam ON priorityParam.IdParent = @IdPrioridadIncidente AND priorityParam.Value = i.Priority
    WHERE i.IdIncident = @IdIncident;
END
GO
