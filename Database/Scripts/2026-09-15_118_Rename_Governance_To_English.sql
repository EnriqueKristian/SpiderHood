-- =============================================================================
-- Renombra el módulo de Gobernanza (Docs/Pendientes-Negocio-Consolidado.md
-- #21) a inglés: tablas, stored procedures, permiso e ItemKey/Url de menú.
-- Reemplaza los scripts 113/114/115 (Reunion/AgendaItem/Asistencia,
-- Votacion/Voto, Acta) -- mismo criterio que
-- 2026-09-15_116_Rename_Employee_Payroll_To_English.sql: entorno de
-- pruebas sin datos reales que preservar, así que se hace DROP + CREATE en
-- vez de sp_rename (los cuerpos de los stored procedures referencian los
-- nombres de tabla como texto literal, sp_rename no los actualizaría).
--
-- Texto de UI (Título del menú, mensajes mostrados al usuario) queda en
-- español -- sólo cambian los identificadores de código: nombres de tabla,
-- columnas Id* que apuntan a una tabla renombrada, stored procedures, la
-- PermissionKey y el ItemKey/Url de MenuItems.
--
-- Mapeo: Reunion->Meeting, Asistencia->Attendance, Votacion->VotingRound,
-- Voto->Vote, Acta->MeetingMinutes. AgendaItem ya estaba en inglés, no
-- cambia de nombre (sólo su FK IdReunion->IdMeeting y su columna
-- TipoVotacion->VotingType, más su compuerta MajorityType).
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===================== DROP procedures viejos =====================
DROP PROCEDURE IF EXISTS dbo.INS_Reunion;
DROP PROCEDURE IF EXISTS dbo.UPD_Reunion;
DROP PROCEDURE IF EXISTS dbo.UPD_ReunionEstado;
DROP PROCEDURE IF EXISTS dbo.GET_ReunionesByBuilding;
DROP PROCEDURE IF EXISTS dbo.GET_ReunionById;
DROP PROCEDURE IF EXISTS dbo.INS_AgendaItem;
DROP PROCEDURE IF EXISTS dbo.UPD_AgendaItem;
DROP PROCEDURE IF EXISTS dbo.UPD_AgendaItemEstado;
DROP PROCEDURE IF EXISTS dbo.GET_AgendaItemsByReunion;
DROP PROCEDURE IF EXISTS dbo.GET_AgendaItemById;
DROP PROCEDURE IF EXISTS dbo.DEL_AgendaItem;
DROP PROCEDURE IF EXISTS dbo.INS_Asistencia;
DROP PROCEDURE IF EXISTS dbo.GET_AsistenciasByReunion;
DROP PROCEDURE IF EXISTS dbo.DEL_Asistencia;
DROP PROCEDURE IF EXISTS dbo.INS_Votacion;
DROP PROCEDURE IF EXISTS dbo.UPD_VotacionCierre;
DROP PROCEDURE IF EXISTS dbo.GET_VotacionesByAgendaItem;
DROP PROCEDURE IF EXISTS dbo.GET_VotacionById;
DROP PROCEDURE IF EXISTS dbo.INS_Voto;
DROP PROCEDURE IF EXISTS dbo.DEL_Voto;
DROP PROCEDURE IF EXISTS dbo.GET_VotosByVotacion;
DROP PROCEDURE IF EXISTS dbo.INS_Acta;
DROP PROCEDURE IF EXISTS dbo.UPD_ActaContenido;
DROP PROCEDURE IF EXISTS dbo.UPD_ActaFirma;
DROP PROCEDURE IF EXISTS dbo.GET_ActaByReunion;
GO

-- ===================== DROP tablas viejas (hijas primero) =====================
DROP TABLE IF EXISTS dbo.Voto;
DROP TABLE IF EXISTS dbo.Votacion;
DROP TABLE IF EXISTS dbo.Acta;
DROP TABLE IF EXISTS dbo.Asistencia;
DROP TABLE IF EXISTS dbo.AgendaItem;
DROP TABLE IF EXISTS dbo.Reunion;
GO

-- ===================== CREATE tablas nuevas =====================
CREATE TABLE dbo.Meeting
(
    IdMeeting            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdBuilding           UNIQUEIDENTIFIER NOT NULL,
    Tipo                 INT              NOT NULL,   -- MeetingType: Ordinaria=1, Extraordinaria=2
    Titulo               NVARCHAR(200)    NOT NULL,
    FechaConvocatoria    DATETIME2        NOT NULL,
    FechaMeeting         DATETIME2        NOT NULL,
    Modalidad            INT              NOT NULL,   -- MeetingModality: Presencial=1, Virtual=2, Hibrida=3
    LugarOVinculo        NVARCHAR(500)    NULL,
    QuorumRequerido      DECIMAL(9,6)     NOT NULL,
    QuorumAlcanzado      DECIMAL(9,6)     NULL,
    Estado               INT              NOT NULL,   -- MeetingStatus
    IdMeetingOrigen      UNIQUEIDENTIFIER NULL,
    IdCalendarItem       UNIQUEIDENTIFIER NULL,
    CreatedBy            UNIQUEIDENTIFIER NOT NULL,
    CreatedOn            DATETIME2        NOT NULL,
    UpdatedOn            DATETIME2        NULL
);

CREATE INDEX IX_Meeting_Building ON dbo.Meeting (IdBuilding, FechaMeeting);
ALTER TABLE dbo.Meeting ADD CONSTRAINT FK_Meeting_Origen
    FOREIGN KEY (IdMeetingOrigen) REFERENCES dbo.Meeting (IdMeeting);
GO

CREATE TABLE dbo.AgendaItem
(
    IdAgendaItem                 UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdMeeting                    UNIQUEIDENTIFIER NOT NULL,
    Orden                        INT              NOT NULL,
    Titulo                       NVARCHAR(200)    NOT NULL,
    Descripcion                  NVARCHAR(1000)   NULL,
    Tipo                         INT              NOT NULL,   -- AgendaItemType: Informativo=1, SujetoAVotacion=2
    VotingType                   INT              NULL,       -- AgendaVotingType: Nominal=1, Secreta=2 (solo si SujetoAVotacion)
    MajorityType                 INT              NULL,       -- MajorityType: Simple=1, Calificada=2, Legal75=3 (solo si SujetoAVotacion)
    PorcentajeMayoriaCalificada  DECIMAL(9,6)     NULL,       -- solo si MajorityType=Calificada
    PermiteRevotacion            BIT              NOT NULL DEFAULT (0),
    Estado                       INT              NOT NULL,   -- AgendaItemStatus: Pendiente=1, Informado=2, Aprobado=3, Rechazado=4
    CreatedOn                    DATETIME2        NOT NULL,

    CONSTRAINT FK_AgendaItem_Meeting FOREIGN KEY (IdMeeting) REFERENCES dbo.Meeting (IdMeeting)
);

CREATE INDEX IX_AgendaItem_Meeting ON dbo.AgendaItem (IdMeeting, Orden);
GO

CREATE TABLE dbo.Attendance
(
    IdAttendance    UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdMeeting       UNIQUEIDENTIFIER NOT NULL,
    IdGroupUnit     UNIQUEIDENTIFIER NOT NULL,
    Alicuota        DECIMAL(9,6)     NOT NULL,
    RegistradoPor   UNIQUEIDENTIFIER NOT NULL,
    FechaRegistro   DATETIME2        NOT NULL,

    CONSTRAINT FK_Attendance_Meeting FOREIGN KEY (IdMeeting) REFERENCES dbo.Meeting (IdMeeting),
    CONSTRAINT UQ_Attendance_Meeting_GroupUnit UNIQUE (IdMeeting, IdGroupUnit)
);

CREATE INDEX IX_Attendance_Meeting ON dbo.Attendance (IdMeeting);
GO

CREATE TABLE dbo.VotingRound
(
    IdVotingRound       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdAgendaItem        UNIQUEIDENTIFIER NOT NULL,
    NroRonda            INT              NOT NULL,
    FechaInicio         DATETIME2        NOT NULL,
    FechaFin            DATETIME2        NULL,
    Estado              INT              NOT NULL,   -- VotingRoundStatus: Abierta=1, Cerrada=2
    AlicuotaAFavor      DECIMAL(9,6)     NULL,
    AlicuotaEnContra    DECIMAL(9,6)     NULL,
    AlicuotaAbstencion  DECIMAL(9,6)     NULL,
    MayoriaAlcanzada    BIT              NULL,
    CreatedBy           UNIQUEIDENTIFIER NOT NULL,
    CreatedOn           DATETIME2        NOT NULL,

    CONSTRAINT FK_VotingRound_AgendaItem FOREIGN KEY (IdAgendaItem) REFERENCES dbo.AgendaItem (IdAgendaItem)
);

CREATE INDEX IX_VotingRound_AgendaItem ON dbo.VotingRound (IdAgendaItem, NroRonda);
GO

CREATE TABLE dbo.Vote
(
    IdVote          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdVotingRound   UNIQUEIDENTIFIER NOT NULL,
    IdGroupUnit     UNIQUEIDENTIFIER NOT NULL,
    Opcion          INT              NOT NULL,   -- VoteOption: AFavor=1, EnContra=2, Abstencion=3
    Alicuota        DECIMAL(9,6)     NOT NULL,
    RegistradoPor   UNIQUEIDENTIFIER NOT NULL,
    FechaVote       DATETIME2        NOT NULL,

    CONSTRAINT FK_Vote_VotingRound FOREIGN KEY (IdVotingRound) REFERENCES dbo.VotingRound (IdVotingRound),
    CONSTRAINT UQ_Vote_VotingRound_GroupUnit UNIQUE (IdVotingRound, IdGroupUnit)
);

CREATE INDEX IX_Vote_VotingRound ON dbo.Vote (IdVotingRound);
GO

CREATE TABLE dbo.MeetingMinutes
(
    IdMeetingMinutes     UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    IdMeeting            UNIQUEIDENTIFIER NOT NULL,
    ContenidoGenerado    NVARCHAR(MAX)    NOT NULL,
    Estado               INT              NOT NULL,   -- MeetingMinutesStatus: Borrador=1, Firmada=2
    NombrePresidente     NVARCHAR(200)    NULL,
    FirmaPresidenteEn    DATETIME2        NULL,
    NombreSecretario     NVARCHAR(200)    NULL,
    FirmaSecretarioEn    DATETIME2        NULL,
    CreatedBy            UNIQUEIDENTIFIER NOT NULL,
    CreatedOn            DATETIME2        NOT NULL,
    UpdatedOn            DATETIME2        NULL,

    CONSTRAINT FK_MeetingMinutes_Meeting FOREIGN KEY (IdMeeting) REFERENCES dbo.Meeting (IdMeeting),
    CONSTRAINT UQ_MeetingMinutes_Meeting UNIQUE (IdMeeting)
);
GO

-- ===================== Stored procedures =====================

-- ----- Meeting -----
CREATE OR ALTER PROCEDURE dbo.INS_Meeting
    @IdMeeting UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200),
    @FechaConvocatoria DATETIME2, @FechaMeeting DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL,
    @QuorumRequerido DECIMAL(9,6), @Estado INT, @IdMeetingOrigen UNIQUEIDENTIFIER = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Meeting
        (IdMeeting, IdBuilding, Tipo, Titulo, FechaConvocatoria, FechaMeeting, Modalidad, LugarOVinculo,
         QuorumRequerido, Estado, IdMeetingOrigen, IdCalendarItem, CreatedBy, CreatedOn)
    VALUES
        (@IdMeeting, @IdBuilding, @Tipo, @Titulo, @FechaConvocatoria, @FechaMeeting, @Modalidad, @LugarOVinculo,
         @QuorumRequerido, @Estado, @IdMeetingOrigen, @IdCalendarItem, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Meeting
    @IdMeeting UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200), @FechaConvocatoria DATETIME2,
    @FechaMeeting DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL, @QuorumRequerido DECIMAL(9,6)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Meeting SET
        Tipo = @Tipo, Titulo = @Titulo, FechaConvocatoria = @FechaConvocatoria, FechaMeeting = @FechaMeeting,
        Modalidad = @Modalidad, LugarOVinculo = @LugarOVinculo, QuorumRequerido = @QuorumRequerido,
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeeting = @IdMeeting;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_MeetingEstado
    @IdMeeting UNIQUEIDENTIFIER, @Estado INT, @QuorumAlcanzado DECIMAL(9,6) = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Meeting SET
        Estado = @Estado,
        QuorumAlcanzado = COALESCE(@QuorumAlcanzado, QuorumAlcanzado),
        IdCalendarItem = COALESCE(@IdCalendarItem, IdCalendarItem),
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeeting = @IdMeeting;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_MeetingsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Meeting r
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdBuilding = @IdBuilding
    ORDER BY r.FechaMeeting DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_MeetingById
    @IdMeeting UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.*, creador.FirstName + ' ' + creador.LastName AS CreatedByName
    FROM dbo.Meeting r
    LEFT JOIN dbo.Users creador ON creador.IdUser = r.CreatedBy
    WHERE r.IdMeeting = @IdMeeting;
END
GO

-- ----- AgendaItem -----
CREATE OR ALTER PROCEDURE dbo.INS_AgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER, @IdMeeting UNIQUEIDENTIFIER, @Orden INT, @Titulo NVARCHAR(200),
    @Descripcion NVARCHAR(1000) = NULL, @Tipo INT, @VotingType INT = NULL, @MajorityType INT = NULL,
    @PorcentajeMayoriaCalificada DECIMAL(9,6) = NULL, @PermiteRevotacion BIT = 0, @Estado INT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AgendaItem
        (IdAgendaItem, IdMeeting, Orden, Titulo, Descripcion, Tipo, VotingType, MajorityType,
         PorcentajeMayoriaCalificada, PermiteRevotacion, Estado, CreatedOn)
    VALUES
        (@IdAgendaItem, @IdMeeting, @Orden, @Titulo, @Descripcion, @Tipo, @VotingType, @MajorityType,
         @PorcentajeMayoriaCalificada, @PermiteRevotacion, @Estado, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_AgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER, @Orden INT, @Titulo NVARCHAR(200), @Descripcion NVARCHAR(1000) = NULL,
    @Tipo INT, @VotingType INT = NULL, @MajorityType INT = NULL, @PorcentajeMayoriaCalificada DECIMAL(9,6) = NULL,
    @PermiteRevotacion BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AgendaItem SET
        Orden = @Orden, Titulo = @Titulo, Descripcion = @Descripcion, Tipo = @Tipo,
        VotingType = @VotingType, MajorityType = @MajorityType,
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

CREATE OR ALTER PROCEDURE dbo.GET_AgendaItemsByMeeting
    @IdMeeting UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.AgendaItem WHERE IdMeeting = @IdMeeting ORDER BY Orden;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AgendaItemById
    @IdAgendaItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.AgendaItem WHERE IdAgendaItem = @IdAgendaItem;
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

-- ----- Attendance -----
CREATE OR ALTER PROCEDURE dbo.INS_Attendance
    @IdAttendance UNIQUEIDENTIFIER, @IdMeeting UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @Alicuota DECIMAL(9,6), @RegistradoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT EXISTS (SELECT 1 FROM dbo.Attendance WHERE IdMeeting = @IdMeeting AND IdGroupUnit = @IdGroupUnit)
        INSERT INTO dbo.Attendance (IdAttendance, IdMeeting, IdGroupUnit, Alicuota, RegistradoPor, FechaRegistro)
        VALUES (@IdAttendance, @IdMeeting, @IdGroupUnit, @Alicuota, @RegistradoPor, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AttendancesByMeeting
    @IdMeeting UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Attendance WHERE IdMeeting = @IdMeeting ORDER BY FechaRegistro;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_Attendance
    @IdMeeting UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Attendance WHERE IdMeeting = @IdMeeting AND IdGroupUnit = @IdGroupUnit;
END
GO

-- ----- VotingRound -----
CREATE OR ALTER PROCEDURE dbo.INS_VotingRound
    @IdVotingRound UNIQUEIDENTIFIER, @IdAgendaItem UNIQUEIDENTIFIER, @NroRonda INT, @Estado INT, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.VotingRound (IdVotingRound, IdAgendaItem, NroRonda, FechaInicio, Estado, CreatedBy, CreatedOn)
    VALUES (@IdVotingRound, @IdAgendaItem, @NroRonda, SYSUTCDATETIME(), @Estado, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_VotingRoundCierre
    @IdVotingRound UNIQUEIDENTIFIER, @AlicuotaAFavor DECIMAL(9,6), @AlicuotaEnContra DECIMAL(9,6),
    @AlicuotaAbstencion DECIMAL(9,6), @MayoriaAlcanzada BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.VotingRound SET
        Estado = 2, -- Cerrada
        FechaFin = SYSUTCDATETIME(),
        AlicuotaAFavor = @AlicuotaAFavor,
        AlicuotaEnContra = @AlicuotaEnContra,
        AlicuotaAbstencion = @AlicuotaAbstencion,
        MayoriaAlcanzada = @MayoriaAlcanzada
    WHERE IdVotingRound = @IdVotingRound;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotingRoundsByAgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.VotingRound WHERE IdAgendaItem = @IdAgendaItem ORDER BY NroRonda;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotingRoundById
    @IdVotingRound UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.VotingRound WHERE IdVotingRound = @IdVotingRound;
END
GO

-- ----- Vote -----
CREATE OR ALTER PROCEDURE dbo.INS_Vote
    @IdVote UNIQUEIDENTIFIER, @IdVotingRound UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @Opcion INT, @Alicuota DECIMAL(9,6), @RegistradoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Vote (IdVote, IdVotingRound, IdGroupUnit, Opcion, Alicuota, RegistradoPor, FechaVote)
    VALUES (@IdVote, @IdVotingRound, @IdGroupUnit, @Opcion, @Alicuota, @RegistradoPor, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_Vote
    @IdVotingRound UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Vote WHERE IdVotingRound = @IdVotingRound AND IdGroupUnit = @IdGroupUnit;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotesByVotingRound
    @IdVotingRound UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Vote WHERE IdVotingRound = @IdVotingRound ORDER BY FechaVote;
END
GO

-- ----- MeetingMinutes -----
CREATE OR ALTER PROCEDURE dbo.INS_MeetingMinutes
    @IdMeetingMinutes UNIQUEIDENTIFIER, @IdMeeting UNIQUEIDENTIFIER, @ContenidoGenerado NVARCHAR(MAX),
    @Estado INT, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.MeetingMinutes (IdMeetingMinutes, IdMeeting, ContenidoGenerado, Estado, CreatedBy, CreatedOn)
    VALUES (@IdMeetingMinutes, @IdMeeting, @ContenidoGenerado, @Estado, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_MeetingMinutesContenido
    @IdMeetingMinutes UNIQUEIDENTIFIER, @ContenidoGenerado NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MeetingMinutes SET ContenidoGenerado = @ContenidoGenerado, UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeetingMinutes = @IdMeetingMinutes;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_MeetingMinutesFirma
    @IdMeetingMinutes UNIQUEIDENTIFIER, @NombrePresidente NVARCHAR(200), @NombreSecretario NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.MeetingMinutes SET
        Estado = 2, -- Firmada
        NombrePresidente = @NombrePresidente,
        FirmaPresidenteEn = SYSUTCDATETIME(),
        NombreSecretario = @NombreSecretario,
        FirmaSecretarioEn = SYSUTCDATETIME(),
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeetingMinutes = @IdMeetingMinutes;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_MeetingMinutesByMeeting
    @IdMeeting UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.MeetingMinutes WHERE IdMeeting = @IdMeeting;
END
GO

-- ===================== Permiso =====================
-- UPDATE en el lugar (no DROP+INSERT) -- preserva PermissionId y las filas
-- de RolePermissions ya otorgadas para este permiso.
UPDATE dbo.Permissions
SET PermissionKey = 'manage_meetings'
WHERE PermissionKey = 'manage_reuniones';
GO

-- ===================== MenuItems =====================
-- La fila ya existe en cualquier BD de pruebas que haya corrido el script
-- 113 original -- se actualiza el ItemKey/Url a inglés, el Title ('Reuniones')
-- queda igual (texto de UI en español). Guarda IF NOT EXISTS por si se corre
-- este script contra una BD nueva que nunca sembró el menú.
IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'A1B2C3D4-6E5F-4A7B-8C9D-0E1F2A3B4C5D')
EXEC dbo.INS_MenuItem
    @IdMenu = 'A1B2C3D4-6E5F-4A7B-8C9D-0E1F2A3B4C5D',
    @IdParent = NULL,
    @ItemKey = 'governance_meetings',
    @Title = 'Reuniones',
    @Icon = 'bi bi-people-fill',
    @Url = '/governance/meetings',
    @Target = NULL,
    @DisplayOrder = 33,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

UPDATE dbo.MenuItems
SET ItemKey = 'governance_meetings', Url = '/governance/meetings'
WHERE IdMenu = 'A1B2C3D4-6E5F-4A7B-8C9D-0E1F2A3B4C5D';
GO
