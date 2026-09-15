-- =============================================================================
-- Renombra el módulo de Comunicados a inglés (Docs/Pendientes-Negocio-
-- Consolidado.md #17): tablas, stored procedures y el Url del MenuItem
-- admin. Reemplaza el script 2026-09-11_97_Comunicado.sql. Mismo criterio
-- que las migraciones anteriores (Employee/Payroll, Governance): DROP +
-- CREATE en vez de sp_rename (entorno de pruebas sin datos que preservar),
-- texto de UI (Title del menú, categorías, mensajes al usuario) queda en
-- español.
--
-- Mapeo: Comunicado->Announcement, ComunicadoDestinatario->
-- AnnouncementRecipient. Los permission keys (view_announcements,
-- create_announcements) y los ItemKey de MenuItems (announcements_admin,
-- myannouncements) YA estaban en inglés -- no se tocan. El valor semilla
-- del Parameter "Categoría Comunicado" (grupo de categorías) tampoco se
-- toca: es texto de UI/dato de negocio, no un identificador de código, y
-- el C# lo sigue buscando por ese mismo string exacto.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===================== DROP procedures viejos =====================
DROP PROCEDURE IF EXISTS dbo.INS_Comunicado;
DROP PROCEDURE IF EXISTS dbo.GET_ComunicadosByBuilding;
DROP PROCEDURE IF EXISTS dbo.INS_ComunicadoDestinatario;
DROP PROCEDURE IF EXISTS dbo.GET_ComunicadoDestinatariosByComunicado;
DROP PROCEDURE IF EXISTS dbo.GET_ComunicadosParaUsuario;
GO

-- ===================== DROP tablas viejas (hijas primero) =====================
DROP TABLE IF EXISTS dbo.ComunicadoDestinatario;
DROP TABLE IF EXISTS dbo.Comunicado;
GO

-- ===================== CREATE tablas nuevas =====================
CREATE TABLE dbo.Announcement
(
    IdAnnouncement   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBuilding       UNIQUEIDENTIFIER NOT NULL,
    Titulo           NVARCHAR(200)    NOT NULL,
    Cuerpo           NVARCHAR(MAX)    NOT NULL,
    IdCategoria      INT              NOT NULL,   -- Parameter.IdTabla, grupo "Categoría Comunicado"
    Alcance          INT              NOT NULL,   -- AnnouncementScope: 1=Público, 2=Reservado, 3=Privado
    RolReservado     NVARCHAR(50)     NULL,        -- sólo si Alcance=Reservado
    EnviarPorCorreo  BIT              NOT NULL DEFAULT (0),
    CreatedBy        UNIQUEIDENTIFIER NOT NULL,
    CreatedOn        DATETIME2        NOT NULL
);

CREATE INDEX IX_Announcement_Building ON dbo.Announcement (IdBuilding, CreatedOn DESC);
GO

CREATE TABLE dbo.AnnouncementRecipient
(
    IdAnnouncementRecipient UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdAnnouncement          UNIQUEIDENTIFIER NOT NULL,
    IdGroupUnit             UNIQUEIDENTIFIER NULL,
    NombreRecipient         NVARCHAR(200)    NOT NULL,
    Telefono                NVARCHAR(30)     NULL,
    Email                   NVARCHAR(200)    NULL,
    EstadoWhatsApp          INT              NOT NULL DEFAULT (0), -- WhatsAppDeliveryStatus
    EstadoCorreo            INT              NOT NULL DEFAULT (0), -- EmailDeliveryStatus
    FechaEnvio              DATETIME2        NOT NULL
);

CREATE INDEX IX_AnnouncementRecipient_Announcement ON dbo.AnnouncementRecipient (IdAnnouncement);
GO

-- ===================== Stored procedures =====================
CREATE OR ALTER PROCEDURE dbo.INS_Announcement
    @IdAnnouncement UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Titulo NVARCHAR(200),
    @Cuerpo NVARCHAR(MAX),
    @IdCategoria INT,
    @Alcance INT,
    @RolReservado NVARCHAR(50) = NULL,
    @EnviarPorCorreo BIT,
    @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Announcement
        (IdAnnouncement, IdBuilding, Titulo, Cuerpo, IdCategoria, Alcance, RolReservado, EnviarPorCorreo, CreatedBy, CreatedOn)
    VALUES
        (@IdAnnouncement, @IdBuilding, @Titulo, @Cuerpo, @IdCategoria, @Alcance, @RolReservado, @EnviarPorCorreo, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AnnouncementsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.IdAnnouncement, c.IdBuilding, c.Titulo, c.Cuerpo, c.IdCategoria, c.Alcance, c.RolReservado,
           c.EnviarPorCorreo, c.CreatedBy, c.CreatedOn,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName,
           (SELECT COUNT(*) FROM dbo.AnnouncementRecipient d WHERE d.IdAnnouncement = c.IdAnnouncement) AS TotalRecipients
    FROM dbo.Announcement c
    LEFT JOIN dbo.Users creador ON creador.IdUser = c.CreatedBy
    WHERE c.IdBuilding = @IdBuilding
    ORDER BY c.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_AnnouncementRecipient
    @IdAnnouncementRecipient UNIQUEIDENTIFIER,
    @IdAnnouncement UNIQUEIDENTIFIER,
    @IdGroupUnit UNIQUEIDENTIFIER = NULL,
    @NombreRecipient NVARCHAR(200),
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(200) = NULL,
    @EstadoWhatsApp INT,
    @EstadoCorreo INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AnnouncementRecipient
        (IdAnnouncementRecipient, IdAnnouncement, IdGroupUnit, NombreRecipient, Telefono, Email, EstadoWhatsApp, EstadoCorreo, FechaEnvio)
    VALUES
        (@IdAnnouncementRecipient, @IdAnnouncement, @IdGroupUnit, @NombreRecipient, @Telefono, @Email, @EstadoWhatsApp, @EstadoCorreo, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AnnouncementRecipientsByAnnouncement
    @IdAnnouncement UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdAnnouncementRecipient, IdAnnouncement, IdGroupUnit, NombreRecipient, Telefono, Email,
           EstadoWhatsApp, EstadoCorreo, FechaEnvio
    FROM dbo.AnnouncementRecipient
    WHERE IdAnnouncement = @IdAnnouncement
    ORDER BY NombreRecipient ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AnnouncementsParaUsuario
    @IdBuilding UNIQUEIDENTIFIER,
    @RolUsuario NVARCHAR(50),
    @IdGroupUnit UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT c.IdAnnouncement, c.IdBuilding, c.Titulo, c.Cuerpo, c.IdCategoria, c.Alcance, c.RolReservado,
           c.EnviarPorCorreo, c.CreatedBy, c.CreatedOn,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName,
           0 AS TotalRecipients
    FROM dbo.Announcement c
    LEFT JOIN dbo.Users creador ON creador.IdUser = c.CreatedBy
    LEFT JOIN dbo.AnnouncementRecipient d ON d.IdAnnouncement = c.IdAnnouncement AND c.Alcance = 3
    WHERE c.IdBuilding = @IdBuilding
      AND (
            c.Alcance = 1
            OR (c.Alcance = 2 AND c.RolReservado = @RolUsuario)
            OR (c.Alcance = 3 AND @IdGroupUnit IS NOT NULL AND d.IdGroupUnit = @IdGroupUnit)
          )
    ORDER BY c.CreatedOn DESC;
END
GO

-- ===================== MenuItems =====================
-- Sólo el ítem admin tiene Url en español ('/comunicados') -- el ItemKey ya
-- era 'announcements_admin' (inglés) y el Title 'Comunicados' es texto de
-- UI, queda igual. El ítem del residente (myannouncements) ya usaba
-- Url='MyAnnouncements' desde antes, no se toca.
UPDATE dbo.MenuItems
SET Url = '/announcements'
WHERE IdMenu = 'B6C1E3F4-2A6D-4B8E-9F3C-7D5A1E8B2C40' AND Url = '/comunicados';
GO
