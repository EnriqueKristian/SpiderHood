-- =============================================================================
-- Módulo de Gobernanza -- Reuniones de Propietarios (Docs/Pendientes-Negocio-
-- Consolidado.md #21, sección "Gobernanza -- Reuniones, Votación y Actas
-- como un solo flujo") -- diseño cerrado 2026-09-11, alcance de la primera
-- entrega decidido 2026-09-15: Reuniones + Votación + Actas juntas primero,
-- Encuestas (sin peso legal) queda para una segunda pasada por ser la pieza
-- de menor esfuerzo y no compartir flujo con las otras tres.
--
-- Esta es la FASE 1 de esa entrega: Convocatoria + Agenda + Asistencia +
-- Quórum + Segunda Convocatoria. Votación (Fase 2) y Actas (Fase 3) se
-- construyen sobre esta base en scripts posteriores -- por eso los puntos de
-- agenda "Sujeto a Votación" quedan en Estado=Pendiente en esta fase (sin
-- mecánica de voto todavía), y no hay tabla Acta aún.
--
-- Alícuota -- decisión cerrada 2026-09-11: NO es un campo nuevo, se deriva
-- en tiempo de ejecución (lado C#, ver IReunionService) como
-- OwnerUnitView.TotalArea / Building.TotalArea -- ambos datos ya existen,
-- no hace falta ninguna columna ni tabla para esto.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reunion')
BEGIN
    CREATE TABLE dbo.Reunion
    (
        IdReunion           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding           UNIQUEIDENTIFIER NOT NULL,
        Tipo                 INT              NOT NULL,   -- TipoReunion: Ordinaria=1, Extraordinaria=2
        Titulo               NVARCHAR(200)    NOT NULL,
        FechaConvocatoria    DATETIME2        NOT NULL,
        FechaReunion         DATETIME2        NOT NULL,
        Modalidad            INT              NOT NULL,   -- ModalidadReunion: Presencial=1, Virtual=2, Hibrida=3
        LugarOVinculo        NVARCHAR(500)    NULL,
        QuorumRequerido      DECIMAL(9,6)     NOT NULL,   -- % de alícuota necesario (ej. 50.000000)
        QuorumAlcanzado      DECIMAL(9,6)     NULL,       -- se completa al Iniciar
        Estado               INT              NOT NULL,   -- EstadoReunion
        -- Si esta Reunión es una Segunda Convocatoria, apunta a la Reunión
        -- original que no alcanzó quórum -- ver CrearSegundaConvocatoriaAsync.
        IdReunionOrigen      UNIQUEIDENTIFIER NULL,
        -- Mismo patrón que Reserva.IdCalendarItem (Docs/Pendientes-Negocio-
        -- Consolidado.md #21): se inserta directo por BDLayout al convocar
        -- (sin pasar por ICalendarService.CreateAsync, que manda correo
        -- masivo), y se actualiza/borra según el ciclo de vida de la Reunión.
        IdCalendarItem       UNIQUEIDENTIFIER NULL,
        CreatedBy            UNIQUEIDENTIFIER NOT NULL,
        CreatedOn            DATETIME2        NOT NULL,
        UpdatedOn            DATETIME2        NULL
    );

    CREATE INDEX IX_Reunion_Building ON dbo.Reunion (IdBuilding, FechaReunion);
    ALTER TABLE dbo.Reunion ADD CONSTRAINT FK_Reunion_Origen
        FOREIGN KEY (IdReunionOrigen) REFERENCES dbo.Reunion (IdReunion);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AgendaItem')
BEGIN
    CREATE TABLE dbo.AgendaItem
    (
        IdAgendaItem                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdReunion                    UNIQUEIDENTIFIER NOT NULL,
        Orden                        INT              NOT NULL,
        Titulo                       NVARCHAR(200)    NOT NULL,
        Descripcion                  NVARCHAR(1000)   NULL,
        Tipo                         INT              NOT NULL,   -- TipoAgendaItem: Informativo=1, SujetoAVotacion=2
        TipoVotacion                 INT              NULL,       -- TipoVotacionAgenda: Nominal=1, Secreta=2 (solo si SujetoAVotacion)
        TipoMayoria                  INT              NULL,       -- TipoMayoria: Simple=1, Calificada=2, Legal75=3 (solo si SujetoAVotacion)
        PorcentajeMayoriaCalificada  DECIMAL(9,6)     NULL,       -- solo si TipoMayoria=Calificada
        PermiteRevotacion            BIT              NOT NULL DEFAULT (0),
        Estado                       INT              NOT NULL,   -- EstadoAgendaItem: Pendiente=1, Informado=2, Aprobado=3, Rechazado=4
        CreatedOn                    DATETIME2        NOT NULL,

        CONSTRAINT FK_AgendaItem_Reunion FOREIGN KEY (IdReunion) REFERENCES dbo.Reunion (IdReunion)
    );

    CREATE INDEX IX_AgendaItem_Reunion ON dbo.AgendaItem (IdReunion, Orden);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Asistencia')
BEGIN
    -- Una fila = una unidad (Grupo Unidad) que asistió. La ausencia de fila
    -- para un Grupo Unidad significa "no asistió" -- no se precarga un
    -- roster completo, sólo se persisten los asistentes (más simple, y el
    -- Acta lista exactamente "asistentes con su % de participación", no
    -- ausentes).
    CREATE TABLE dbo.Asistencia
    (
        IdAsistencia    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdReunion       UNIQUEIDENTIFIER NOT NULL,
        IdGroupUnit     UNIQUEIDENTIFIER NOT NULL,
        Alicuota        DECIMAL(9,6)     NOT NULL,   -- % snapshot al registrar (OwnerUnitView.TotalArea / Building.TotalArea)
        RegistradoPor   UNIQUEIDENTIFIER NOT NULL,
        FechaRegistro   DATETIME2        NOT NULL,

        CONSTRAINT FK_Asistencia_Reunion FOREIGN KEY (IdReunion) REFERENCES dbo.Reunion (IdReunion),
        CONSTRAINT UQ_Asistencia_Reunion_GroupUnit UNIQUE (IdReunion, IdGroupUnit)
    );

    CREATE INDEX IX_Asistencia_Reunion ON dbo.Asistencia (IdReunion);
END
GO

-- ===================== Reunion =====================
CREATE OR ALTER PROCEDURE dbo.INS_Reunion
    @IdReunion UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200),
    @FechaConvocatoria DATETIME2, @FechaReunion DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL,
    @QuorumRequerido DECIMAL(9,6), @Estado INT, @IdReunionOrigen UNIQUEIDENTIFIER = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Reunion
        (IdReunion, IdBuilding, Tipo, Titulo, FechaConvocatoria, FechaReunion, Modalidad, LugarOVinculo,
         QuorumRequerido, Estado, IdReunionOrigen, IdCalendarItem, CreatedBy, CreatedOn)
    VALUES
        (@IdReunion, @IdBuilding, @Tipo, @Titulo, @FechaConvocatoria, @FechaReunion, @Modalidad, @LugarOVinculo,
         @QuorumRequerido, @Estado, @IdReunionOrigen, @IdCalendarItem, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Edición de la convocatoria -- sólo tiene sentido mientras Estado=Convocada
-- (el service valida eso antes de llamar). No toca Estado/QuorumAlcanzado/
-- IdReunionOrigen/CreatedBy.
CREATE OR ALTER PROCEDURE dbo.UPD_Reunion
    @IdReunion UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200), @FechaConvocatoria DATETIME2,
    @FechaReunion DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL, @QuorumRequerido DECIMAL(9,6)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reunion SET
        Tipo = @Tipo, Titulo = @Titulo, FechaConvocatoria = @FechaConvocatoria, FechaReunion = @FechaReunion,
        Modalidad = @Modalidad, LugarOVinculo = @LugarOVinculo, QuorumRequerido = @QuorumRequerido,
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdReunion = @IdReunion;
END
GO

-- Transición de estado genérica -- cubre Iniciar/QuorumNoAlcanzado/Finalizar/
-- Cancelar. Los parámetros que no aplican a una transición puntual quedan
-- NULL y no pisan el valor ya guardado (COALESCE), mismo patrón que
-- UPD_ReservaEstado.
CREATE OR ALTER PROCEDURE dbo.UPD_ReunionEstado
    @IdReunion UNIQUEIDENTIFIER, @Estado INT, @QuorumAlcanzado DECIMAL(9,6) = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Reunion SET
        Estado = @Estado,
        QuorumAlcanzado = COALESCE(@QuorumAlcanzado, QuorumAlcanzado),
        IdCalendarItem = COALESCE(@IdCalendarItem, IdCalendarItem),
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdReunion = @IdReunion;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReunionesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reunion r
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding
    ORDER BY r.FechaReunion DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReunionById
    @IdReunion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Reunion r
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdReunion = @IdReunion;
END
GO

-- ===================== AgendaItem =====================
CREATE OR ALTER PROCEDURE dbo.INS_AgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER, @IdReunion UNIQUEIDENTIFIER, @Orden INT, @Titulo NVARCHAR(200),
    @Descripcion NVARCHAR(1000) = NULL, @Tipo INT, @TipoVotacion INT = NULL, @TipoMayoria INT = NULL,
    @PorcentajeMayoriaCalificada DECIMAL(9,6) = NULL, @PermiteRevotacion BIT = 0, @Estado INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AgendaItem
        (IdAgendaItem, IdReunion, Orden, Titulo, Descripcion, Tipo, TipoVotacion, TipoMayoria,
         PorcentajeMayoriaCalificada, PermiteRevotacion, Estado, CreatedOn)
    VALUES
        (@IdAgendaItem, @IdReunion, @Orden, @Titulo, @Descripcion, @Tipo, @TipoVotacion, @TipoMayoria,
         @PorcentajeMayoriaCalificada, @PermiteRevotacion, @Estado, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_AgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER, @Orden INT, @Titulo NVARCHAR(200), @Descripcion NVARCHAR(1000) = NULL,
    @Tipo INT, @TipoVotacion INT = NULL, @TipoMayoria INT = NULL, @PorcentajeMayoriaCalificada DECIMAL(9,6) = NULL,
    @PermiteRevotacion BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AgendaItem SET
        Orden = @Orden, Titulo = @Titulo, Descripcion = @Descripcion, Tipo = @Tipo,
        TipoVotacion = @TipoVotacion, TipoMayoria = @TipoMayoria,
        PorcentajeMayoriaCalificada = @PorcentajeMayoriaCalificada, PermiteRevotacion = @PermiteRevotacion
    WHERE IdAgendaItem = @IdAgendaItem;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_AgendaItemEstado
    @IdAgendaItem UNIQUEIDENTIFIER, @Estado INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AgendaItem SET Estado = @Estado WHERE IdAgendaItem = @IdAgendaItem;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AgendaItemsByReunion
    @IdReunion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.AgendaItem WHERE IdReunion = @IdReunion ORDER BY Orden;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_AgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.AgendaItem WHERE IdAgendaItem = @IdAgendaItem;
END
GO

-- ===================== Asistencia =====================
CREATE OR ALTER PROCEDURE dbo.INS_Asistencia
    @IdAsistencia UNIQUEIDENTIFIER, @IdReunion UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @Alicuota DECIMAL(9,6), @RegistradoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Asistencia WHERE IdReunion = @IdReunion AND IdGroupUnit = @IdGroupUnit)
        INSERT INTO dbo.Asistencia (IdAsistencia, IdReunion, IdGroupUnit, Alicuota, RegistradoPor, FechaRegistro)
        VALUES (@IdAsistencia, @IdReunion, @IdGroupUnit, @Alicuota, @RegistradoPor, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsistenciasByReunion
    @IdReunion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Asistencia WHERE IdReunion = @IdReunion ORDER BY FechaRegistro;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_Asistencia
    @IdReunion UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Asistencia WHERE IdReunion = @IdReunion AND IdGroupUnit = @IdGroupUnit;
END
GO

-- ===================== Permisos =====================
-- Un solo permiso para toda la gestión (convocar, editar agenda, registrar
-- asistencia, iniciar/finalizar/cancelar) -- a diferencia de Reservas, acá
-- no hay una separación natural aprobar/gestionar (no hay "solicitud" que
-- alguien más apruebe; quien convoca ya tiene la autoridad). Quién lo tiene
-- asignado (Administrador y/o Junta) lo decide el dueño de la cuenta desde
-- Roles y Permisos, igual que el resto de permisos de este codebase.
IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'manage_reuniones')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'manage_reuniones', 'Gestionar Reuniones', 'Convocar reuniones, administrar su agenda, registrar asistencia e iniciar/finalizar/cancelar (Administrador/Junta).', 'gobernanza');
GO

-- Menú: item raíz "Reuniones" -- visible para cualquiera con acceso al
-- edificio (el gateo de "puede convocar/gestionar" es por permiso dentro de
-- la página, mismo criterio que Reservas/Comunicados). Futuras fases
-- (Votación en vivo, Actas) viven dentro de la misma pantalla de detalle,
-- no suman ítems de menú nuevos.
IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'A1B2C3D4-6E5F-4A7B-8C9D-0E1F2A3B4C5D')
EXEC dbo.INS_MenuItem
    @IdMenu = 'A1B2C3D4-6E5F-4A7B-8C9D-0E1F2A3B4C5D',
    @IdParent = NULL,
    @ItemKey = 'gobernanza_reuniones',
    @Title = 'Reuniones',
    @Icon = 'bi bi-people-fill',
    @Url = '/gobernanza/reuniones',
    @Target = NULL,
    @DisplayOrder = 33,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
