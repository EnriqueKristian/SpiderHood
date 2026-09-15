-- =============================================================================
-- Fix: GET_IncidentById / GET_IncidentsByBuilding / GET_IncidentsByReporter
-- resuelven TypeName/PriorityName buscando el grupo de Parámetros con
-- `WHERE IdParent = 0` -- pero en la tabla real todo grupo de nivel superior
-- tiene IdParent NULL (no 0), así que esa subconsulta nunca encuentra el
-- IdTabla del grupo ("Tipo Incidente"/"Prioridad Incidente") y el LEFT JOIN
-- contra Parameter nunca matchea nada. TypeName/PriorityName quedan NULL
-- SIEMPRE, para cualquier incidente, en cualquier ambiente -- la pantalla de
-- detalle (y la lista) caían al fallback ("Tipo: -", "Prioridad: 3" -- el
-- valor numérico crudo en vez del nombre).
--
-- El código C# (ParameterService/IncidentList.ResolveGroupChildren) filtra
-- por el mismo `IdParent == 0`, pero ahí SÍ funciona -- FromSqlRaw mapea un
-- IdParent NULL de la BD a 0 (default de un int no-nullable en C#), así que
-- por accidente de la coerción nunca se notó que el criterio real en SQL
-- necesita IS NULL, no = 0.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentById
    @IdIncident UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Prioridad Incidente');

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
END;
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Prioridad Incidente');

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
END;
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentsByReporter
    @ReportedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdTipoIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Tipo Incidente');
    DECLARE @IdPrioridadIncidente INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Prioridad Incidente');

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
END;
GO
