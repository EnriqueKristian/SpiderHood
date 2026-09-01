-- =============================================================================
-- Fix: GET_IncidentsByBuilding / GET_IncidentsByReporter / GET_IncidentById
-- (de 2026-09-02_05_Incidents.sql) referenciaban dbo.GroupUnit.IdUnit y
-- dbo.GroupUnit.GroupName, columnas que no existen en el esquema real
-- (Msg 207, reportado al correr el script anterior).
--
-- Este fix saca el JOIN contra GroupUnit -- UnitName queda NULL por ahora
-- (la UI ya maneja ese caso mostrando "Área común"/guion). Una vez que me
-- confirmes el nombre real de la tabla/columna que tiene el número o nombre
-- de la unidad, mando un segundo fix que la vuelve a conectar.
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
           CAST(NULL AS NVARCHAR(100)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
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
           CAST(NULL AS NVARCHAR(100)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
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
           CAST(NULL AS NVARCHAR(100)) AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    WHERE i.IdIncident = @IdIncident;
END
GO
