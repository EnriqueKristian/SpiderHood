-- =============================================================================
-- Módulo de Gobernanza -- Votación (Docs/Pendientes-Negocio-Consolidado.md
-- #21, "Gobernanza -- Reuniones, Votación y Actas como un solo flujo") --
-- Fase 2 de la entrega Reuniones+Votación+Actas (Fase 1 =
-- 2026-09-15_113_Gobernanza_Fase1_Reuniones.sql). Sólo se puede votar sobre
-- un AgendaItem Tipo=SujetoAVotacion de una Reunion en curso, y sólo votan
-- las unidades que ya están registradas como Asistencia de esa Reunion (el
-- voto usa la MISMA alícuota ya capturada al registrar la asistencia).
--
-- Revotación -- decisión cerrada 2026-09-11, flexible por AgendaItem
-- (columna PermiteRevotacion, ya agregada en la Fase 1): cada intento sobre
-- un mismo punto es una fila nueva de Votacion con NroRonda incremental, no
-- se pisa la anterior -- así el Acta (Fase 3) puede mostrar todos los
-- intentos, no sólo el último.
--
-- Mayoría -- simplificación deliberada (documentada, no un olvido): el
-- Reglamento Interno de cada edificio define los números exactos y el
-- reglamento definitivo del D.L. 1568 todavía no se publica, así que:
--   - Simple: AlicuotaAFavor > AlicuotaEnContra (ambas ya expresadas como %
--     del edificio total, igual que Asistencia.Alicuota).
--   - Calificada: AlicuotaAFavor sobre el total de votos EMITIDOS (a favor +
--     en contra + abstención) alcanza AgendaItem.PorcentajeMayoriaCalificada
--     -- es decir, un "2/3 de los presentes que votaron", no 2/3 del
--     edificio completo.
--   - Legal75 (Art. 14.1 D.L. 1568, desafectar bienes comunes): AlicuotaAFavor
--     >= 75, ahí sí sobre el total del edificio (la ley lo fija así).
-- Este cálculo vive en IReunionService.CerrarVotacionAsync -- las columnas
-- Alicuota* de Votacion son el resultado ya sumado, no se recalculan acá.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Votacion')
BEGIN
    CREATE TABLE dbo.Votacion
    (
        IdVotacion          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAgendaItem        UNIQUEIDENTIFIER NOT NULL,
        NroRonda            INT              NOT NULL,
        FechaInicio         DATETIME2        NOT NULL,
        FechaFin            DATETIME2        NULL,
        Estado              INT              NOT NULL,   -- EstadoVotacion: Abierta=1, Cerrada=2
        AlicuotaAFavor      DECIMAL(9,6)     NULL,        -- se completa al Cerrar
        AlicuotaEnContra    DECIMAL(9,6)     NULL,
        AlicuotaAbstencion  DECIMAL(9,6)     NULL,
        MayoriaAlcanzada    BIT              NULL,
        CreatedBy           UNIQUEIDENTIFIER NOT NULL,
        CreatedOn           DATETIME2        NOT NULL,

        CONSTRAINT FK_Votacion_AgendaItem FOREIGN KEY (IdAgendaItem) REFERENCES dbo.AgendaItem (IdAgendaItem)
    );

    CREATE INDEX IX_Votacion_AgendaItem ON dbo.Votacion (IdAgendaItem, NroRonda);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Voto')
BEGIN
    CREATE TABLE dbo.Voto
    (
        IdVoto          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdVotacion      UNIQUEIDENTIFIER NOT NULL,
        IdGroupUnit     UNIQUEIDENTIFIER NOT NULL,
        Opcion          INT              NOT NULL,   -- OpcionVoto: AFavor=1, EnContra=2, Abstencion=3
        Alicuota        DECIMAL(9,6)     NOT NULL,   -- snapshot de Asistencia.Alicuota al votar
        RegistradoPor   UNIQUEIDENTIFIER NOT NULL,
        FechaVoto       DATETIME2        NOT NULL,

        CONSTRAINT FK_Voto_Votacion FOREIGN KEY (IdVotacion) REFERENCES dbo.Votacion (IdVotacion),
        CONSTRAINT UQ_Voto_Votacion_GroupUnit UNIQUE (IdVotacion, IdGroupUnit)
    );

    CREATE INDEX IX_Voto_Votacion ON dbo.Voto (IdVotacion);
END
GO

-- La Fase 1 (2026-09-15_113) no agregó un GET por Id individual de
-- AgendaItem (sólo GET_AgendaItemsByReunion, la vista de detalle siempre
-- traía la agenda completa) -- IniciarVotacionAsync sí necesita leer un solo
-- punto (su Tipo/TipoMayoria/PermiteRevotacion) sin depender de que el
-- caller ya lo tenga cargado en memoria.
CREATE OR ALTER PROCEDURE dbo.GET_AgendaItemById
    @IdAgendaItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.AgendaItem WHERE IdAgendaItem = @IdAgendaItem;
END
GO

-- ===================== Votacion =====================
CREATE OR ALTER PROCEDURE dbo.INS_Votacion
    @IdVotacion UNIQUEIDENTIFIER, @IdAgendaItem UNIQUEIDENTIFIER, @NroRonda INT, @Estado INT, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Votacion (IdVotacion, IdAgendaItem, NroRonda, FechaInicio, Estado, CreatedBy, CreatedOn)
    VALUES (@IdVotacion, @IdAgendaItem, @NroRonda, SYSUTCDATETIME(), @Estado, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_VotacionCierre
    @IdVotacion UNIQUEIDENTIFIER, @AlicuotaAFavor DECIMAL(9,6), @AlicuotaEnContra DECIMAL(9,6),
    @AlicuotaAbstencion DECIMAL(9,6), @MayoriaAlcanzada BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Votacion SET
        Estado = 2, -- Cerrada
        FechaFin = SYSUTCDATETIME(),
        AlicuotaAFavor = @AlicuotaAFavor,
        AlicuotaEnContra = @AlicuotaEnContra,
        AlicuotaAbstencion = @AlicuotaAbstencion,
        MayoriaAlcanzada = @MayoriaAlcanzada
    WHERE IdVotacion = @IdVotacion;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotacionesByAgendaItem
    @IdAgendaItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Votacion WHERE IdAgendaItem = @IdAgendaItem ORDER BY NroRonda;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotacionById
    @IdVotacion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Votacion WHERE IdVotacion = @IdVotacion;
END
GO

-- ===================== Voto =====================
-- Upsert manual desde el service (DEL_Voto + INS_Voto) en vez de MERGE --
-- mismo criterio ya usado en el resto del codebase (ej. UPD_Reserva* son SPs
-- chicos y explícitos, no MERGE) -- permite corregir un voto cargado mal
-- mientras la Votación sigue Abierta.
CREATE OR ALTER PROCEDURE dbo.INS_Voto
    @IdVoto UNIQUEIDENTIFIER, @IdVotacion UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER,
    @Opcion INT, @Alicuota DECIMAL(9,6), @RegistradoPor UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Voto (IdVoto, IdVotacion, IdGroupUnit, Opcion, Alicuota, RegistradoPor, FechaVoto)
    VALUES (@IdVoto, @IdVotacion, @IdGroupUnit, @Opcion, @Alicuota, @RegistradoPor, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_Voto
    @IdVotacion UNIQUEIDENTIFIER, @IdGroupUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Voto WHERE IdVotacion = @IdVotacion AND IdGroupUnit = @IdGroupUnit;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VotosByVotacion
    @IdVotacion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Voto WHERE IdVotacion = @IdVotacion ORDER BY FechaVoto;
END
GO
