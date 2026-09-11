-- =============================================================================
-- Módulo de Comunicados (Docs/Pendientes-Negocio-Consolidado.md #17) -- diseño
-- cerrado con el usuario el 2026-09-11. Tablas 100% nuevas, no tocan nada
-- existente. El menú "Comunicados"/MyAnnouncements y el permiso
-- view_announcements ya existían sin nada detrás (ver
-- 2026-09-10_85_Reorganizar_Menu.sql) -- este script los deja funcionando.
--
-- Comunicado = cabecera (qué se publicó, con qué alcance, quién lo publicó).
-- ComunicadoDestinatario = a quién se le mandó y qué pasó con el envío
-- (WhatsApp/correo) -- mismo patrón cabecera+detalle que BudgetHeader/
-- Installment o ReceiptFile.
--
-- Alcance (int, ver Classes/Communication/Comunicado.cs AlcanceComunicado):
--   1 = Público (todo el edificio), 2 = Reservado (un rol), 3 = Privado
--   (una o varias unidades puntuales). Sin "Público Global" de SysAdmin a
--   todos los edificios (fuera de alcance de v1) ni mensajería vecino-a-
--   vecino (se descartó explícitamente, sería un chat).
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Comunicado')
BEGIN
    CREATE TABLE dbo.Comunicado
    (
        IdComunicado     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding       UNIQUEIDENTIFIER NOT NULL,
        Titulo           NVARCHAR(200)    NOT NULL,
        Cuerpo           NVARCHAR(MAX)    NOT NULL,
        IdCategoria      INT              NOT NULL,   -- Parameter.IdTabla, grupo "Categoría de Comunicado"
        Alcance          INT              NOT NULL,   -- 1=Público, 2=Reservado, 3=Privado
        RolReservado     NVARCHAR(50)     NULL,        -- sólo si Alcance=Reservado (Administrador/Junta/Residente)
        EnviarPorCorreo  BIT              NOT NULL DEFAULT (0),
        CreatedBy        UNIQUEIDENTIFIER NOT NULL,
        CreatedOn        DATETIME2        NOT NULL
    );

    CREATE INDEX IX_Comunicado_Building ON dbo.Comunicado (IdBuilding, CreatedOn DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComunicadoDestinatario')
BEGIN
    CREATE TABLE dbo.ComunicadoDestinatario
    (
        IdComunicadoDestinatario UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdComunicado             UNIQUEIDENTIFIER NOT NULL,
        IdGroupUnit              UNIQUEIDENTIFIER NULL,   -- unidad del destinatario, si aplica (Privado/Público)
        NombreDestinatario       NVARCHAR(200)    NOT NULL,
        Telefono                 NVARCHAR(30)     NULL,
        Email                    NVARCHAR(200)    NULL,
        EstadoWhatsApp           INT              NOT NULL DEFAULT (0), -- 0=NoAplica,1=Simulado,2=Enviado,3=Fallido
        EstadoCorreo             INT              NOT NULL DEFAULT (0), -- 0=NoAplica,1=Enviado,2=Fallido
        FechaEnvio               DATETIME2        NOT NULL
    );

    CREATE INDEX IX_ComunicadoDestinatario_Comunicado ON dbo.ComunicadoDestinatario (IdComunicado);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Comunicado
    @IdComunicado UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.Comunicado
        (IdComunicado, IdBuilding, Titulo, Cuerpo, IdCategoria, Alcance, RolReservado, EnviarPorCorreo, CreatedBy, CreatedOn)
    VALUES
        (@IdComunicado, @IdBuilding, @Titulo, @Cuerpo, @IdCategoria, @Alcance, @RolReservado, @EnviarPorCorreo, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ComunicadosByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT c.IdComunicado, c.IdBuilding, c.Titulo, c.Cuerpo, c.IdCategoria, c.Alcance, c.RolReservado,
           c.EnviarPorCorreo, c.CreatedBy, c.CreatedOn,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName,
           (SELECT COUNT(*) FROM dbo.ComunicadoDestinatario d WHERE d.IdComunicado = c.IdComunicado) AS TotalDestinatarios
    FROM dbo.Comunicado c
    LEFT JOIN dbo.Users creador ON creador.IdUser = c.CreatedBy
    WHERE c.IdBuilding = @IdBuilding
    ORDER BY c.CreatedOn DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_ComunicadoDestinatario
    @IdComunicadoDestinatario UNIQUEIDENTIFIER,
    @IdComunicado UNIQUEIDENTIFIER,
    @IdGroupUnit UNIQUEIDENTIFIER = NULL,
    @NombreDestinatario NVARCHAR(200),
    @Telefono NVARCHAR(30) = NULL,
    @Email NVARCHAR(200) = NULL,
    @EstadoWhatsApp INT,
    @EstadoCorreo INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ComunicadoDestinatario
        (IdComunicadoDestinatario, IdComunicado, IdGroupUnit, NombreDestinatario, Telefono, Email, EstadoWhatsApp, EstadoCorreo, FechaEnvio)
    VALUES
        (@IdComunicadoDestinatario, @IdComunicado, @IdGroupUnit, @NombreDestinatario, @Telefono, @Email, @EstadoWhatsApp, @EstadoCorreo, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ComunicadoDestinatariosByComunicado
    @IdComunicado UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdComunicadoDestinatario, IdComunicado, IdGroupUnit, NombreDestinatario, Telefono, Email,
           EstadoWhatsApp, EstadoCorreo, FechaEnvio
    FROM dbo.ComunicadoDestinatario
    WHERE IdComunicado = @IdComunicado
    ORDER BY NombreDestinatario ASC;
END
GO

-- Comunicados visibles para un residente puntual: Públicos de su edificio,
-- Reservados si su rol matchea, o Privados donde su unidad aparece como
-- destinatario. RolUsuario llega como parámetro (no se resuelve acá adentro)
-- porque el rol activo del usuario ya lo resuelve la sesión (UserSession),
-- no hace falta repetir esa lógica en SQL.
CREATE OR ALTER PROCEDURE dbo.GET_ComunicadosParaUsuario
    @IdBuilding UNIQUEIDENTIFIER,
    @RolUsuario NVARCHAR(50),
    @IdGroupUnit UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT c.IdComunicado, c.IdBuilding, c.Titulo, c.Cuerpo, c.IdCategoria, c.Alcance, c.RolReservado,
           c.EnviarPorCorreo, c.CreatedBy, c.CreatedOn,
           creador.FirstName + ' ' + creador.LastName AS CreatedByName,
           0 AS TotalDestinatarios
    FROM dbo.Comunicado c
    LEFT JOIN dbo.Users creador ON creador.IdUser = c.CreatedBy
    LEFT JOIN dbo.ComunicadoDestinatario d ON d.IdComunicado = c.IdComunicado AND c.Alcance = 3
    WHERE c.IdBuilding = @IdBuilding
      AND (
            c.Alcance = 1
            OR (c.Alcance = 2 AND c.RolReservado = @RolUsuario)
            OR (c.Alcance = 3 AND @IdGroupUnit IS NOT NULL AND d.IdGroupUnit = @IdGroupUnit)
          )
    ORDER BY c.CreatedOn DESC;
END
GO

-- Permiso para publicar Comunicados (Administrador/Junta) -- view_announcements
-- ya se documentó como existente (Docs/Pendientes-Negocio-Consolidado.md #17)
-- pero se re-sembra acá también por las dudas (idempotente); asignar a roles
-- se hace después desde /Settings/Roles, no acá (mismo criterio que
-- 2026-09-11_92_Seed_ApproveExpensesPermission.sql).
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'view_announcements')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'view_announcements', 'Ver Comunicados', 'Ver los comunicados publicados en el edificio.', 'incidents');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'create_announcements')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'create_announcements', 'Publicar Comunicados', 'Publicar un comunicado (Público/Reservado/Privado) al edificio.', 'incidents');
GO

-- Categorías de Comunicado -- define qué plantilla de WhatsApp usa cada una
-- (Docs/Pendientes-Negocio-Consolidado.md #17). Mismo patrón que
-- 2026-09-02_14_Seed_IncidentTypeAndPriorityParameters.sql -- NO idempotente,
-- no volver a correr si ya se ejecutó una vez.
DECLARE @IdBuilding UNIQUEIDENTIFIER = (SELECT TOP 1 IdBuilding FROM dbo.Building);

EXEC dbo.INS_Parameter @Description = N'Categoría de Comunicado', @ShortDescription = N'Categoría Comunicado',
    @Value = 0, @Sort = 0, @IdParent = 0, @Estado = 1, @IdBuilding = @IdBuilding;

DECLARE @IdCategoriaComunicado INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter
    WHERE IdParent = 0 AND ShortDescription = N'Categoría Comunicado' AND IdBuilding = @IdBuilding ORDER BY IdTabla DESC);

EXEC dbo.INS_Parameter @Description = N'Mantenimiento Programado', @ShortDescription = N'Mantenimiento Programado',
    @Value = 1, @Sort = 1, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;

EXEC dbo.INS_Parameter @Description = N'Corte de Servicio', @ShortDescription = N'Corte de Servicio',
    @Value = 2, @Sort = 2, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;

EXEC dbo.INS_Parameter @Description = N'Convocatoria de Reunión', @ShortDescription = N'Convocatoria de Reunión',
    @Value = 3, @Sort = 3, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;

EXEC dbo.INS_Parameter @Description = N'Aviso General', @ShortDescription = N'Aviso General',
    @Value = 4, @Sort = 4, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;
GO

-- Menú: "Comunicados" (admin, publicar) para Administrador/Junta -- el ítem
-- "Comunicados"/MyAnnouncements ya existe para el residente (view_announcements,
-- ver 2026-09-10_85_Reorganizar_Menu.sql) y no se toca acá. Ítem standalone
-- (IdParent NULL), igual que se hizo originalmente con "Incidentes" en
-- 2026-09-02_05_Incidents.sql -- el gateo real de acceso es por permiso
-- dentro de la página (create_announcements vía PermissionService), no por
-- una tabla de menú-a-permiso (no confirmado su nombre real desde acá).
-- NO idempotente este paso puntual, mismo motivo que el resto de INS_MenuItem
-- de este repo: no reintentar sin revisar el menú antes.
EXEC dbo.INS_MenuItem
    @IdMenu = 'B6C1E3F4-2A6D-4B8E-9F3C-7D5A1E8B2C40',
    @IdParent = NULL,
    @ItemKey = 'announcements_admin',
    @Title = 'Comunicados',
    @Icon = 'bi bi-megaphone',
    @Url = '/comunicados',
    @Target = NULL,
    @DisplayOrder = 30,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
