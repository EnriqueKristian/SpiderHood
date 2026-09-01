-- =============================================================================
-- Módulo de Incidentes: reclamos/tickets de mantenimiento reportados por
-- Residentes (o Administrador), con workflow de estados y comentarios.
--
-- Tablas 100% nuevas -- no toca nada existente. Nace con columnas de auditoría
-- (CreatedBy/CreatedOn/ModifiedBy/ModifiedOn) desde el INSERT, no hace falta un
-- SP de "estampado" aparte como en Database/Scripts/2026-09-01_01 (esa técnica
-- era para retrofitear auditoría en tablas viejas sin tocar sus SPs; acá la
-- tabla es nueva, así que va directo).
--
-- El historial de aprobación/asignación/cierre NO tiene tabla propia: usa
-- WorkflowAuditLog (Module = 'Incident'), la misma tabla de
-- Database/Scripts/2026-09-01_02_WorkflowAuditLog.sql.
--
-- OJO: los JOIN de las consultas GET_Incidents* contra dbo.Users y
-- dbo.GroupUnit están armados según el diagrama de BD que compartiste
-- (Users.FirstName/LastName, GroupUnit.GroupName) -- confirmalos contra tu
-- esquema real antes de correr, mismo cuidado que con los scripts anteriores.
--
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Tablas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Incident')
BEGIN
    CREATE TABLE dbo.Incident
    (
        IdIncident   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding   UNIQUEIDENTIFIER NOT NULL,
        Title        NVARCHAR(200)    NOT NULL,
        Description  NVARCHAR(MAX)    NOT NULL,
        Type         NVARCHAR(30)     NOT NULL,   -- IncidentType (Plumbing/Electrical/...)
        Priority     NVARCHAR(20)     NOT NULL,   -- Low/Medium/High/Urgent
        Status       NVARCHAR(20)     NOT NULL,   -- Reported/InReview/InProgress/Resolved/Closed/Rejected/Reopened
        IdGroupUnit  UNIQUEIDENTIFIER NULL,        -- NULL = área común
        ReportedBy   UNIQUEIDENTIFIER NOT NULL,
        AssignedTo   UNIQUEIDENTIFIER NULL,
        ResolvedOn   DATETIME2        NULL,
        ClosedOn     DATETIME2        NULL,
        CreatedBy    NVARCHAR(256)    NOT NULL,
        CreatedOn    DATETIME2        NOT NULL,
        ModifiedBy   NVARCHAR(256)    NULL,
        ModifiedOn   DATETIME2        NULL
    );

    CREATE INDEX IX_Incident_Building_Status ON dbo.Incident (IdBuilding, Status);
    CREATE INDEX IX_Incident_ReportedBy ON dbo.Incident (ReportedBy);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IncidentComment')
BEGIN
    CREATE TABLE dbo.IncidentComment
    (
        IdComment   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdIncident  UNIQUEIDENTIFIER NOT NULL,
        AuthorId    UNIQUEIDENTIFIER NOT NULL,
        [Text]      NVARCHAR(MAX)    NOT NULL,
        IsInternal  BIT              NOT NULL DEFAULT (0),  -- true = nota interna, el Residente no la ve
        CreatedOn   DATETIME2        NOT NULL
    );

    CREATE INDEX IX_IncidentComment_Incident ON dbo.IncidentComment (IdIncident);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Incident
    @IdIncident UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX),
    @Type NVARCHAR(30),
    @Priority NVARCHAR(20),
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

-- Cambia estado (y opcionalmente a quién está asignado) -- separado de un UPDATE
-- genérico de campos porque es la única transición que de verdad ocurre en la
-- app (Título/Descripción no se editan después de reportado en este alcance).
-- Marca ResolvedOn/ClosedOn automáticamente según el nuevo estado.
CREATE OR ALTER PROCEDURE dbo.UPD_IncidentStatus
    @IdIncident UNIQUEIDENTIFIER,
    @Status NVARCHAR(20),
    @AssignedTo UNIQUEIDENTIFIER = NULL,
    @ModifiedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Incident
    SET Status = @Status,
        AssignedTo = COALESCE(@AssignedTo, AssignedTo),
        ResolvedOn = CASE WHEN @Status = 'Resolved' THEN SYSUTCDATETIME() ELSE ResolvedOn END,
        ClosedOn = CASE WHEN @Status = 'Closed' THEN SYSUTCDATETIME() ELSE ClosedOn END,
        ModifiedBy = @ModifiedBy,
        ModifiedOn = SYSUTCDATETIME()
    WHERE IdIncident = @IdIncident;
END
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
           gu.GroupName AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
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
           gu.GroupName AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
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
           gu.GroupName AS UnitName
    FROM dbo.Incident i
    LEFT JOIN dbo.Users reporter ON reporter.IdUser = i.ReportedBy
    LEFT JOIN dbo.Users assignee ON assignee.IdUser = i.AssignedTo
    LEFT JOIN dbo.GroupUnit gu ON gu.IdUnit = i.IdGroupUnit
    WHERE i.IdIncident = @IdIncident;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_IncidentComment
    @IdComment UNIQUEIDENTIFIER,
    @IdIncident UNIQUEIDENTIFIER,
    @AuthorId UNIQUEIDENTIFIER,
    @Text NVARCHAR(MAX),
    @IsInternal BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.IncidentComment (IdComment, IdIncident, AuthorId, [Text], IsInternal, CreatedOn)
    VALUES (@IdComment, @IdIncident, @AuthorId, @Text, @IsInternal, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentCommentsByIncident
    @IdIncident UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.IdComment, c.IdIncident, c.AuthorId, c.[Text], c.IsInternal, c.CreatedOn,
           author.FirstName + ' ' + author.LastName AS AuthorName
    FROM dbo.IncidentComment c
    LEFT JOIN dbo.Users author ON author.IdUser = c.AuthorId
    WHERE c.IdIncident = @IdIncident
    ORDER BY c.CreatedOn ASC;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Item de menú "Incidentes" -- vía el SP que ya usa el resto de la app
--    (dbo.INS_MenuItem), no un INSERT crudo, para no arriesgar el esquema real
--    de MenuItemDefinition. Gateo de acceso a la página es por rol directo
--    (Administrador/Junta/SysAdmin ven todo, Residente ve "Mis Incidentes"),
--    no por clave de permiso nueva -- mismo criterio que /Settings/SystemLogs.
-- -----------------------------------------------------------------------------

-- NO es idempotente este paso puntual (a diferencia del resto del script): no
-- conozco el nombre físico real de la tabla de menú desde acá para hacer un
-- IF NOT EXISTS confiable, así que llama a INS_MenuItem directo. Si corrés el
-- script dos veces, vas a terminar con el item de menú duplicado -- revisá el
-- menú de Configuración > Items de Menú antes de repetir este bloque.
EXEC dbo.INS_MenuItem
    @IdMenu = 'B2C9E1A4-6F3D-4E8A-9C1B-7D2A5F4E8B91',
    @IdParent = NULL,
    @ItemKey = 'incidents',
    @Title = 'Incidentes',
    @Icon = 'bi bi-exclamation-triangle',
    @Url = '/incidents',
    @Target = NULL,
    @DisplayOrder = 50,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
