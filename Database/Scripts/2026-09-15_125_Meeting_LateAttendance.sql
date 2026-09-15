-- =============================================================================
-- Feedback del usuario: una vez que la reunión pasa a En Curso, no había forma
-- de registrar a alguien que llega tarde -- RegistrarAttendanceAsync exigía
-- Estado=Convocada. Se habilita el registro tardío, a criterio de quien dirige
-- la reunión, dentro de una ventana de tiempo configurable desde que arrancó
-- realmente (no desde la hora programada, FechaMeeting, que puede diferir).
--
-- Decisiones confirmadas con el usuario:
--   1) El % de Quórum Alcanzado se recalcula cada vez que se agrega un asistente
--      tardío (no queda fijado al momento de Iniciar).
--   2) El recién llegado puede votar en cualquier ronda que siga Abierta en ese
--      momento (RegistrarVoteAsync ya vuelve a traer Attendance en cada llamada,
--      así que esto no necesita cambios de por sí).
--   3) Ventana de tiempo fija y configurable por reunión (default 20 minutos),
--      no "mientras esté En Curso" sin límite.
--
-- Columnas nuevas -- ALTER TABLE, no DROP+CREATE, para no perder las reuniones
-- ya cargadas en la BD de pruebas.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Meeting') AND name = 'FechaInicioReal')
BEGIN
    ALTER TABLE dbo.Meeting ADD FechaInicioReal DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Meeting') AND name = 'MinutosLimiteAsistenciaTardia')
BEGIN
    ALTER TABLE dbo.Meeting ADD MinutosLimiteAsistenciaTardia INT NOT NULL DEFAULT (20);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Meeting
    @IdMeeting UNIQUEIDENTIFIER, @IdBuilding UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200),
    @FechaConvocatoria DATETIME2, @FechaMeeting DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL,
    @QuorumRequerido DECIMAL(9,6), @Estado INT, @IdMeetingOrigen UNIQUEIDENTIFIER = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL, @CreatedBy UNIQUEIDENTIFIER,
    @MinutosLimiteAsistenciaTardia INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Meeting
        (IdMeeting, IdBuilding, Tipo, Titulo, FechaConvocatoria, FechaMeeting, Modalidad, LugarOVinculo,
         QuorumRequerido, Estado, IdMeetingOrigen, IdCalendarItem, CreatedBy, CreatedOn, MinutosLimiteAsistenciaTardia)
    VALUES
        (@IdMeeting, @IdBuilding, @Tipo, @Titulo, @FechaConvocatoria, @FechaMeeting, @Modalidad, @LugarOVinculo,
         @QuorumRequerido, @Estado, @IdMeetingOrigen, @IdCalendarItem, @CreatedBy, SYSUTCDATETIME(), @MinutosLimiteAsistenciaTardia);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Meeting
    @IdMeeting UNIQUEIDENTIFIER, @Tipo INT, @Titulo NVARCHAR(200), @FechaConvocatoria DATETIME2,
    @FechaMeeting DATETIME2, @Modalidad INT, @LugarOVinculo NVARCHAR(500) = NULL, @QuorumRequerido DECIMAL(9,6),
    @MinutosLimiteAsistenciaTardia INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Meeting SET
        Tipo = @Tipo, Titulo = @Titulo, FechaConvocatoria = @FechaConvocatoria, FechaMeeting = @FechaMeeting,
        Modalidad = @Modalidad, LugarOVinculo = @LugarOVinculo, QuorumRequerido = @QuorumRequerido,
        MinutosLimiteAsistenciaTardia = @MinutosLimiteAsistenciaTardia,
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeeting = @IdMeeting;
END
GO

-- FechaInicioReal se pasa COALESCE-eada: sólo la fija IniciarMeetingAsync la primera
-- vez que la reunión pasa a EnCurso (pasando la fecha real); cualquier otra transición
-- de estado (Finalizar, etc.) manda NULL acá y no la toca -- lo mismo que ya hacía
-- QuorumAlcanzado.
CREATE OR ALTER PROCEDURE dbo.UPD_MeetingEstado
    @IdMeeting UNIQUEIDENTIFIER, @Estado INT, @QuorumAlcanzado DECIMAL(9,6) = NULL,
    @IdCalendarItem UNIQUEIDENTIFIER = NULL, @FechaInicioReal DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Meeting SET
        Estado = @Estado,
        QuorumAlcanzado = COALESCE(@QuorumAlcanzado, QuorumAlcanzado),
        IdCalendarItem = COALESCE(@IdCalendarItem, IdCalendarItem),
        FechaInicioReal = COALESCE(FechaInicioReal, @FechaInicioReal),
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdMeeting = @IdMeeting;
END
GO
