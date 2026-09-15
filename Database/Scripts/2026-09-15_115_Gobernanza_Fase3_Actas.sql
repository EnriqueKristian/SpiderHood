-- =============================================================================
-- Módulo de Gobernanza -- Actas (Docs/Pendientes-Negocio-Consolidado.md #21,
-- "Gobernanza -- Reuniones, Votación y Actas como un solo flujo") -- Fase 3
-- (última) de la entrega Reuniones+Votación+Actas. Se construye sobre las
-- Fases 1 (2026-09-15_113) y 2 (2026-09-15_114).
--
-- El Acta es un borrador AUTOGENERADO a partir de lo ya capturado en Reunion
-- + AgendaItem + Asistencia + Votacion (fecha, modalidad, asistentes con su
-- % de alícuota, quórum verificado, agenda, resultado de cada punto con
-- todas sus rondas) -- reduce el riesgo de un acta redactada de memoria días
-- después. Se puede regenerar mientras sigue en Borrador (si se corrige algo
-- en la Reunión antes de cerrar el trámite); una vez Firmada queda
-- INMUTABLE -- IReunionService.GenerarBorradorActaAsync rechaza regenerar un
-- Acta ya firmada.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Acta')
BEGIN
    CREATE TABLE dbo.Acta
    (
        IdActa              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdReunion           UNIQUEIDENTIFIER NOT NULL,
        ContenidoGenerado   NVARCHAR(MAX)    NOT NULL,
        Estado              INT              NOT NULL,   -- EstadoActa: Borrador=1, Firmada=2
        NombrePresidente    NVARCHAR(200)    NULL,
        FirmaPresidenteEn   DATETIME2        NULL,
        NombreSecretario    NVARCHAR(200)    NULL,
        FirmaSecretarioEn   DATETIME2        NULL,
        CreatedBy           UNIQUEIDENTIFIER NOT NULL,
        CreatedOn           DATETIME2        NOT NULL,
        UpdatedOn           DATETIME2        NULL,

        CONSTRAINT FK_Acta_Reunion FOREIGN KEY (IdReunion) REFERENCES dbo.Reunion (IdReunion),
        -- Una Reunion tiene a lo sumo un Acta -- GenerarBorradorActaAsync
        -- regenera (UPDATE) la existente, nunca inserta una segunda.
        CONSTRAINT UQ_Acta_Reunion UNIQUE (IdReunion)
    );
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Acta
    @IdActa UNIQUEIDENTIFIER, @IdReunion UNIQUEIDENTIFIER, @ContenidoGenerado NVARCHAR(MAX),
    @Estado INT, @CreatedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Acta (IdActa, IdReunion, ContenidoGenerado, Estado, CreatedBy, CreatedOn)
    VALUES (@IdActa, @IdReunion, @ContenidoGenerado, @Estado, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ActaContenido
    @IdActa UNIQUEIDENTIFIER, @ContenidoGenerado NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Acta SET ContenidoGenerado = @ContenidoGenerado, UpdatedOn = SYSUTCDATETIME()
    WHERE IdActa = @IdActa;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ActaFirma
    @IdActa UNIQUEIDENTIFIER, @NombrePresidente NVARCHAR(200), @NombreSecretario NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Acta SET
        Estado = 2, -- Firmada
        NombrePresidente = @NombrePresidente,
        FirmaPresidenteEn = SYSUTCDATETIME(),
        NombreSecretario = @NombreSecretario,
        FirmaSecretarioEn = SYSUTCDATETIME(),
        UpdatedOn = SYSUTCDATETIME()
    WHERE IdActa = @IdActa;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ActaByReunion
    @IdReunion UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.Acta WHERE IdReunion = @IdReunion;
END
GO
