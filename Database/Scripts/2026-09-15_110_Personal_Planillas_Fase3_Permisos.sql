-- =============================================================================
-- Módulo Personal y Planillas -- Fase 3 (parcial): Permisos y licencias.
-- Requiere Fase 1 y Fase 2 ya aplicadas. Del resto de la Fase 3 (export
-- PLAME/T-Registro, pago del sueldo, marcado biométrico) queda deliberadamente
-- fuera: PLAME/T-Registro necesita la especificación oficial vigente de SUNAT
-- para no generar un archivo incorrecto; pago del sueldo necesita credenciales
-- reales de un gateway (Yape Negocios/Plin/banco); marcado biométrico es un
-- producto aparte (app móvil o hardware).
--
-- Una solicitud de permiso/licencia (con o sin goce de haber), aprobada por el
-- mismo motor de WorkflowAuditLog que ya usan Vacaciones/Presupuesto/Cuota --
-- mismo patrón exacto que Vacaciones (Fase 2). La diferencia práctica con
-- Vacaciones: un permiso SIN goce de haber SÍ afecta la boleta (se descuenta
-- proporcional a los días, ver GET_PermisoLicenciaSinGoceDiasByPersonalMes,
-- usado por IPlanillaService.GenerarBoletaAsync) -- un permiso CON goce no
-- descuenta nada, igual que unas vacaciones.
--
-- Auditoría inline, sin FK real -- mismo criterio que Fases 1 y 2.
--
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Tabla
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PermisoLicencia')
BEGIN
    CREATE TABLE dbo.PermisoLicencia
    (
        IdPermisoLicencia UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal        UNIQUEIDENTIFIER NOT NULL,
        FechaInicio       DATE             NOT NULL,
        FechaFin          DATE             NOT NULL,
        ConGoceDeHaber    BIT              NOT NULL DEFAULT (1),
        Motivo            NVARCHAR(300)    NULL,
        Estado            NVARCHAR(20)     NOT NULL DEFAULT ('Pendiente'), -- Pendiente/Aprobado/Rechazado
        IdAprobador       UNIQUEIDENTIFIER NULL,
        FechaResolucion   DATETIME2        NULL,
        CreatedBy         NVARCHAR(256)    NOT NULL,
        CreatedOn         DATETIME2        NOT NULL
    );

    CREATE INDEX IX_PermisoLicencia_Personal ON dbo.PermisoLicencia (IdPersonal, FechaInicio);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_PermisoLicencia
    @IdPermisoLicencia UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @FechaInicio DATE,
    @FechaFin DATE,
    @ConGoceDeHaber BIT,
    @Motivo NVARCHAR(300) = NULL,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.PermisoLicencia (IdPermisoLicencia, IdPersonal, FechaInicio, FechaFin, ConGoceDeHaber, Motivo, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn)
    VALUES (@IdPermisoLicencia, @IdPersonal, @FechaInicio, @FechaFin, @ConGoceDeHaber, @Motivo, 'Pendiente', NULL, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_PermisoLicenciaEstado
    @IdPermisoLicencia UNIQUEIDENTIFIER,
    @Estado NVARCHAR(20),
    @IdAprobador UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.PermisoLicencia
    SET Estado = @Estado,
        IdAprobador = @IdAprobador,
        FechaResolucion = SYSUTCDATETIME()
    WHERE IdPermisoLicencia = @IdPermisoLicencia;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_PermisoLicenciaByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPermisoLicencia, IdPersonal, FechaInicio, FechaFin, ConGoceDeHaber, Motivo, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn
    FROM dbo.PermisoLicencia
    WHERE IdPersonal = @IdPersonal
    ORDER BY FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_PermisoLicenciaPendientesByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pl.IdPermisoLicencia, pl.IdPersonal, pl.FechaInicio, pl.FechaFin, pl.ConGoceDeHaber, pl.Motivo, pl.Estado, pl.IdAprobador, pl.FechaResolucion, pl.CreatedBy, pl.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.PermisoLicencia pl
    INNER JOIN dbo.Personal p ON p.IdPersonal = pl.IdPersonal
    WHERE p.IdAccount = @IdAccount AND pl.Estado = 'Pendiente'
    ORDER BY pl.CreatedOn;
END
GO

-- Días de permiso SIN goce de haber, Aprobados, que caen dentro del rango
-- [@FechaDesde, @FechaHasta] (normalmente un mes calendario) -- usado por
-- IPlanillaService.GenerarBoletaAsync para descontar proporcionalmente.
-- GREATEST/LEAST (SQL Server 2022+) recortan el solape al rango pedido -- un
-- permiso que empieza en agosto y termina en septiembre sólo cuenta sus días
-- de septiembre en la boleta de septiembre.
CREATE OR ALTER PROCEDURE dbo.GET_PermisoLicenciaSinGoceDiasByPersonalMes
    @IdPersonal UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(
        DATEDIFF(DAY, GREATEST(FechaInicio, @FechaDesde), LEAST(FechaFin, @FechaHasta)) + 1
    ), 0) AS DiasSinGoce
    FROM dbo.PermisoLicencia
    WHERE IdPersonal = @IdPersonal
      AND ConGoceDeHaber = 0
      AND Estado = 'Aprobado'
      AND FechaInicio <= @FechaHasta
      AND FechaFin >= @FechaDesde;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Item de menú -- hijo de "Personal" (Fase 1, IdMenu
--    C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71). No idempotente, mismo criterio
--    que Fases 1 y 2.
-- -----------------------------------------------------------------------------

EXEC dbo.INS_MenuItem
    @IdMenu = '29A7FBE8-E1AD-408E-DF7C-9DC0E4A08C27',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-permisos',
    @Title = 'Permisos y licencias',
    @Icon = 'bi bi-calendar-x',
    @Url = '/personal/permisos',
    @Target = NULL,
    @DisplayOrder = 6,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
