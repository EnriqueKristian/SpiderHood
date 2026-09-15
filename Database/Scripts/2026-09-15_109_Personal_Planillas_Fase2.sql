-- =============================================================================
-- Módulo Personal y Planillas -- Fase 2 (Planillas): régimen laboral,
-- parámetros legales, vacaciones y boleta de pago. Requiere Fase 1
-- (Database/Scripts/2026-09-15_108_Personal_Planillas_Fase1.sql) ya aplicada.
--
-- Alcance (ver especificación funcional del 14 sept 2026, sección 11
-- "Decisiones confirmadas"): SpiderHood sólo GENERA la boleta -- no declara
-- PLAME ni T-Registro (Fase 3). CTS queda fuera de este alcance: la sección 9
-- de la especificación ("lo mínimo indispensable") no la lista entre los
-- bloques obligatorios de la boleta -- es un depósito aparte, no un concepto
-- del "neto a pagar" mensual. Renta de 5ta categoría se simplifica a 0
-- mientras la remuneración anualizada no supere 7 UIT (el caso normal para
-- personal de edificio) -- el cálculo progresivo completo por encima de ese
-- umbral queda pendiente (ver comentario en IPlanillaService.GenerarBoletaAsync).
--
-- Auditoría inline, sin FK real -- mismo criterio que Fase 1.
--
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 0) Personal.TieneHijos -- necesario para Asignación Familiar (Régimen
--    General, S/ 113/mes si tiene hijos, ver sección 3 de la especificación).
--    No existía en la Fase 1 porque hasta ahora Personal no distinguía esto.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Personal') AND name = 'TieneHijos')
BEGIN
    ALTER TABLE dbo.Personal ADD TieneHijos BIT NOT NULL CONSTRAINT DF_Personal_TieneHijos DEFAULT (0);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Personal
    @IdPersonal UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @DNI NVARCHAR(20),
    @Nombres NVARCHAR(150),
    @Apellidos NVARCHAR(150),
    @Cargo NVARCHAR(100),
    @FechaIngreso DATE,
    @RemuneracionBase DECIMAL(18,2),
    @SistemaPensionario NVARCHAR(20),
    @Telefono NVARCHAR(30) = NULL,
    @CreatedBy NVARCHAR(256),
    @TieneHijos BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Personal
        (IdPersonal, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese, RemuneracionBase, SistemaPensionario, Telefono, IsActive, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos)
    VALUES
        (@IdPersonal, @IdAccount, @DNI, @Nombres, @Apellidos, @Cargo, @FechaIngreso, NULL, @RemuneracionBase, @SistemaPensionario, @Telefono, 1, @CreatedBy, SYSUTCDATETIME(), NULL, NULL, @TieneHijos);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Personal
    @IdPersonal UNIQUEIDENTIFIER,
    @DNI NVARCHAR(20),
    @Nombres NVARCHAR(150),
    @Apellidos NVARCHAR(150),
    @Cargo NVARCHAR(100),
    @RemuneracionBase DECIMAL(18,2),
    @SistemaPensionario NVARCHAR(20),
    @Telefono NVARCHAR(30) = NULL,
    @IsActive BIT,
    @FechaCese DATE = NULL,
    @ModifiedBy NVARCHAR(256),
    @TieneHijos BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Personal
    SET DNI = @DNI,
        Nombres = @Nombres,
        Apellidos = @Apellidos,
        Cargo = @Cargo,
        RemuneracionBase = @RemuneracionBase,
        SistemaPensionario = @SistemaPensionario,
        Telefono = @Telefono,
        IsActive = @IsActive,
        FechaCese = @FechaCese,
        ModifiedBy = @ModifiedBy,
        ModifiedOn = SYSUTCDATETIME(),
        TieneHijos = @TieneHijos
    WHERE IdPersonal = @IdPersonal;
END
GO

-- GET_PersonalByAccount/GET_PersonalById ya traen SELECT * de columnas
-- explícitas (Fase 1) -- se recrean acá sólo para sumar TieneHijos.
CREATE OR ALTER PROCEDURE dbo.GET_PersonalByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPersonal, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos
    FROM dbo.Personal
    WHERE IdAccount = @IdAccount
    ORDER BY Apellidos, Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_PersonalById
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPersonal, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos
    FROM dbo.Personal
    WHERE IdPersonal = @IdPersonal;
END
GO

-- -----------------------------------------------------------------------------
-- 1) Tablas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ConfiguracionRegimenLaboral')
BEGIN
    CREATE TABLE dbo.ConfiguracionRegimenLaboral
    (
        IdConfiguracionRegimenLaboral UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount            UNIQUEIDENTIFIER NOT NULL,
        TipoRegimen          NVARCHAR(20)     NOT NULL,  -- Microempresa/PequenaEmpresa/RegimenGeneral
        RUC                  NVARCHAR(20)     NULL,
        RazonSocial          NVARCHAR(200)    NULL,
        FechaVigenciaDesde   DATE             NOT NULL,
        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedOn            DATETIME2        NOT NULL
    );

    CREATE INDEX IX_ConfiguracionRegimenLaboral_Account ON dbo.ConfiguracionRegimenLaboral (IdAccount, FechaVigenciaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ParametrosLegales')
BEGIN
    CREATE TABLE dbo.ParametrosLegales
    (
        IdParametrosLegales      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount                UNIQUEIDENTIFIER NOT NULL,
        Anio                     INT              NOT NULL,
        ValorUIT                 DECIMAL(18,2)    NOT NULL,
        MontoAsignacionFamiliar  DECIMAL(18,2)    NOT NULL,
        CostoSISMensual          DECIMAL(18,2)    NOT NULL,
        PorcentajeEsSalud        DECIMAL(5,2)     NOT NULL,
        -- ONP es un % fijo por ley; AFP en la práctica varía por fondo (aporte +
        -- comisión + prima de seguro) -- PorcentajeAFP es un aproximado
        -- configurable, no el cálculo exacto por AFP real (ver comentario en
        -- IPlanillaService.GenerarBoletaAsync).
        PorcentajeONP            DECIMAL(5,2)     NOT NULL,
        PorcentajeAFP            DECIMAL(5,2)     NOT NULL,
        CreatedBy                NVARCHAR(256)    NOT NULL,
        CreatedOn                DATETIME2        NOT NULL,

        CONSTRAINT UQ_ParametrosLegales_Account_Anio UNIQUE (IdAccount, Anio)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vacaciones')
BEGIN
    -- Una fila por SOLICITUD de goce (no un saldo agregado) -- el saldo
    -- (ganados/gozados/pendientes) se deriva en tiempo de lectura, ver
    -- GET_VacacionesGozadasByPersonalAnio. Reutiliza WorkflowAuditLog (Module =
    -- 'Vacaciones') para el historial de aprobación, mismo motor que
    -- Presupuesto/Cuota (sección 8 de la especificación) -- por eso Estado acá
    -- es sólo el estado ACTUAL, no el historial completo.
    CREATE TABLE dbo.Vacaciones
    (
        IdVacaciones      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal        UNIQUEIDENTIFIER NOT NULL,
        Anio              INT              NOT NULL,   -- período devengado
        FechaInicio       DATE             NOT NULL,
        FechaFin          DATE             NOT NULL,
        DiasSolicitados   INT              NOT NULL,
        Estado            NVARCHAR(20)     NOT NULL DEFAULT ('Pendiente'), -- Pendiente/Aprobada/Rechazada/Gozada
        IdAprobador       UNIQUEIDENTIFIER NULL,
        FechaResolucion   DATETIME2        NULL,
        CreatedBy         NVARCHAR(256)    NOT NULL,
        CreatedOn         DATETIME2        NOT NULL
    );

    CREATE INDEX IX_Vacaciones_Personal_Anio ON dbo.Vacaciones (IdPersonal, Anio);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BoletaPago')
BEGIN
    CREATE TABLE dbo.BoletaPago
    (
        IdBoletaPago      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal        UNIQUEIDENTIFIER NOT NULL,
        Anio              INT              NOT NULL,
        Mes               INT              NOT NULL,   -- 1..12
        -- Congelado al momento del cálculo (sección 11, decisión "nunca
        -- retroactivo") -- una boleta ya emitida no cambia si el régimen de la
        -- Cuenta cambia después.
        TipoRegimen       NVARCHAR(20)     NOT NULL,
        TotalIngresos     DECIMAL(18,2)    NOT NULL,
        TotalDescuentos   DECIMAL(18,2)    NOT NULL,
        NetoAPagar        DECIMAL(18,2)    NOT NULL,
        FechaGeneracion   DATETIME2        NOT NULL,
        GeneradoPor       NVARCHAR(256)    NOT NULL,

        CONSTRAINT UQ_BoletaPago_Personal_Periodo UNIQUE (IdPersonal, Anio, Mes)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BoletaPagoDetalle')
BEGIN
    -- Filas en vez de columnas fijas (sección 4 de la especificación) -- el
    -- motor de reglas decide qué filas generar según el régimen, sin tocar el
    -- esquema cuando cambian las reglas.
    CREATE TABLE dbo.BoletaPagoDetalle
    (
        IdBoletaPagoDetalle UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBoletaPago        UNIQUEIDENTIFIER NOT NULL,
        TipoConcepto        NVARCHAR(20)     NOT NULL,  -- Ingreso/DescuentoTrabajador/AporteEmpleador
        CodigoConcepto      NVARCHAR(20)     NOT NULL,  -- BASICO/HEXT25/HEXT35/GRATIF/ASIGFAM/ONP/AFP/ESSALUD/SIS
        Descripcion         NVARCHAR(200)    NOT NULL,
        Monto               DECIMAL(18,2)    NOT NULL,
        EsRemunerativo      BIT              NOT NULL
    );

    CREATE INDEX IX_BoletaPagoDetalle_Boleta ON dbo.BoletaPagoDetalle (IdBoletaPago);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures -- ConfiguracionRegimenLaboral
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_ConfiguracionRegimenLaboral
    @IdConfiguracionRegimenLaboral UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @TipoRegimen NVARCHAR(20),
    @RUC NVARCHAR(20) = NULL,
    @RazonSocial NVARCHAR(200) = NULL,
    @FechaVigenciaDesde DATE,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ConfiguracionRegimenLaboral (IdConfiguracionRegimenLaboral, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn)
    VALUES (@IdConfiguracionRegimenLaboral, @IdAccount, @TipoRegimen, @RUC, @RazonSocial, @FechaVigenciaDesde, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Trae la fila vigente a una fecha dada (por default, hoy) -- la más reciente
-- cuya FechaVigenciaDesde ya pasó. Así una boleta de un mes viejo puede pedir
-- el régimen "vigente a esa fecha" en vez de siempre el más nuevo.
CREATE OR ALTER PROCEDURE dbo.GET_ConfiguracionRegimenLaboralVigente
    @IdAccount UNIQUEIDENTIFIER,
    @Fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FechaEfectiva DATE = COALESCE(@Fecha, CAST(SYSUTCDATETIME() AS DATE));

    SELECT TOP 1 IdConfiguracionRegimenLaboral, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn
    FROM dbo.ConfiguracionRegimenLaboral
    WHERE IdAccount = @IdAccount AND FechaVigenciaDesde <= @FechaEfectiva
    ORDER BY FechaVigenciaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ConfiguracionRegimenLaboralHistorial
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdConfiguracionRegimenLaboral, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn
    FROM dbo.ConfiguracionRegimenLaboral
    WHERE IdAccount = @IdAccount
    ORDER BY FechaVigenciaDesde DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Stored Procedures -- ParametrosLegales
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_ParametrosLegales
    @IdParametrosLegales UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT,
    @ValorUIT DECIMAL(18,2),
    @MontoAsignacionFamiliar DECIMAL(18,2),
    @CostoSISMensual DECIMAL(18,2),
    @PorcentajeEsSalud DECIMAL(5,2),
    @PorcentajeONP DECIMAL(5,2),
    @PorcentajeAFP DECIMAL(5,2),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ParametrosLegales
        (IdParametrosLegales, IdAccount, Anio, ValorUIT, MontoAsignacionFamiliar, CostoSISMensual, PorcentajeEsSalud, PorcentajeONP, PorcentajeAFP, CreatedBy, CreatedOn)
    VALUES
        (@IdParametrosLegales, @IdAccount, @Anio, @ValorUIT, @MontoAsignacionFamiliar, @CostoSISMensual, @PorcentajeEsSalud, @PorcentajeONP, @PorcentajeAFP, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ParametrosLegales
    @IdParametrosLegales UNIQUEIDENTIFIER,
    @ValorUIT DECIMAL(18,2),
    @MontoAsignacionFamiliar DECIMAL(18,2),
    @CostoSISMensual DECIMAL(18,2),
    @PorcentajeEsSalud DECIMAL(5,2),
    @PorcentajeONP DECIMAL(5,2),
    @PorcentajeAFP DECIMAL(5,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ParametrosLegales
    SET ValorUIT = @ValorUIT,
        MontoAsignacionFamiliar = @MontoAsignacionFamiliar,
        CostoSISMensual = @CostoSISMensual,
        PorcentajeEsSalud = @PorcentajeEsSalud,
        PorcentajeONP = @PorcentajeONP,
        PorcentajeAFP = @PorcentajeAFP
    WHERE IdParametrosLegales = @IdParametrosLegales;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ParametrosLegalesByAccountAndYear
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdParametrosLegales, IdAccount, Anio, ValorUIT, MontoAsignacionFamiliar, CostoSISMensual,
           PorcentajeEsSalud, PorcentajeONP, PorcentajeAFP, CreatedBy, CreatedOn
    FROM dbo.ParametrosLegales
    WHERE IdAccount = @IdAccount AND Anio = @Anio;
END
GO

-- -----------------------------------------------------------------------------
-- 4) Stored Procedures -- Vacaciones
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Vacaciones
    @IdVacaciones UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @Anio INT,
    @FechaInicio DATE,
    @FechaFin DATE,
    @DiasSolicitados INT,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Vacaciones (IdVacaciones, IdPersonal, Anio, FechaInicio, FechaFin, DiasSolicitados, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn)
    VALUES (@IdVacaciones, @IdPersonal, @Anio, @FechaInicio, @FechaFin, @DiasSolicitados, 'Pendiente', NULL, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_VacacionesEstado
    @IdVacaciones UNIQUEIDENTIFIER,
    @Estado NVARCHAR(20),
    @IdAprobador UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Vacaciones
    SET Estado = @Estado,
        IdAprobador = @IdAprobador,
        FechaResolucion = SYSUTCDATETIME()
    WHERE IdVacaciones = @IdVacaciones;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VacacionesByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdVacaciones, IdPersonal, Anio, FechaInicio, FechaFin, DiasSolicitados, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn
    FROM dbo.Vacaciones
    WHERE IdPersonal = @IdPersonal
    ORDER BY FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VacacionesPendientesByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.IdVacaciones, v.IdPersonal, v.Anio, v.FechaInicio, v.FechaFin, v.DiasSolicitados, v.Estado, v.IdAprobador, v.FechaResolucion, v.CreatedBy, v.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.Vacaciones v
    INNER JOIN dbo.Personal p ON p.IdPersonal = v.IdPersonal
    WHERE p.IdAccount = @IdAccount AND v.Estado = 'Pendiente'
    ORDER BY v.CreatedOn;
END
GO

-- Días GOZADOS de un Personal en un año -- suma de solicitudes Aprobadas o
-- Gozadas. DiasGanados/DiasPendientes se calculan en el servicio (Ganados
-- sale del régimen vigente, no de esta tabla).
CREATE OR ALTER PROCEDURE dbo.GET_VacacionesGozadasByPersonalAnio
    @IdPersonal UNIQUEIDENTIFIER,
    @Anio INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(DiasSolicitados), 0) AS DiasGozados
    FROM dbo.Vacaciones
    WHERE IdPersonal = @IdPersonal AND Anio = @Anio AND Estado IN ('Aprobada', 'Gozada');
END
GO

-- -----------------------------------------------------------------------------
-- 5) Stored Procedures -- BoletaPago / BoletaPagoDetalle
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_BoletaPago
    @IdBoletaPago UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @Anio INT,
    @Mes INT,
    @TipoRegimen NVARCHAR(20),
    @TotalIngresos DECIMAL(18,2),
    @TotalDescuentos DECIMAL(18,2),
    @NetoAPagar DECIMAL(18,2),
    @GeneradoPor NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.BoletaPago (IdBoletaPago, IdPersonal, Anio, Mes, TipoRegimen, TotalIngresos, TotalDescuentos, NetoAPagar, FechaGeneracion, GeneradoPor)
    VALUES (@IdBoletaPago, @IdPersonal, @Anio, @Mes, @TipoRegimen, @TotalIngresos, @TotalDescuentos, @NetoAPagar, SYSUTCDATETIME(), @GeneradoPor);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdBoletaPago, IdPersonal, Anio, Mes, TipoRegimen, TotalIngresos, TotalDescuentos, NetoAPagar, FechaGeneracion, GeneradoPor
    FROM dbo.BoletaPago
    WHERE IdPersonal = @IdPersonal
    ORDER BY Anio DESC, Mes DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletaById
    @IdBoletaPago UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdBoletaPago, b.IdPersonal, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.BoletaPago b
    INNER JOIN dbo.Personal p ON p.IdPersonal = b.IdPersonal
    WHERE b.IdBoletaPago = @IdBoletaPago;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByAccountAndPeriodo
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT,
    @Mes INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdBoletaPago, b.IdPersonal, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo
    FROM dbo.BoletaPago b
    INNER JOIN dbo.Personal p ON p.IdPersonal = b.IdPersonal
    WHERE p.IdAccount = @IdAccount AND b.Anio = @Anio AND b.Mes = @Mes
    ORDER BY p.Apellidos, p.Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_BoletaPagoDetalle
    @IdBoletaPagoDetalle UNIQUEIDENTIFIER,
    @IdBoletaPago UNIQUEIDENTIFIER,
    @TipoConcepto NVARCHAR(20),
    @CodigoConcepto NVARCHAR(20),
    @Descripcion NVARCHAR(200),
    @Monto DECIMAL(18,2),
    @EsRemunerativo BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.BoletaPagoDetalle (IdBoletaPagoDetalle, IdBoletaPago, TipoConcepto, CodigoConcepto, Descripcion, Monto, EsRemunerativo)
    VALUES (@IdBoletaPagoDetalle, @IdBoletaPago, @TipoConcepto, @CodigoConcepto, @Descripcion, @Monto, @EsRemunerativo);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletaPagoDetalleByBoleta
    @IdBoletaPago UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdBoletaPagoDetalle, IdBoletaPago, TipoConcepto, CodigoConcepto, Descripcion, Monto, EsRemunerativo
    FROM dbo.BoletaPagoDetalle
    WHERE IdBoletaPago = @IdBoletaPago
    ORDER BY CASE TipoConcepto WHEN 'Ingreso' THEN 1 WHEN 'DescuentoTrabajador' THEN 2 ELSE 3 END, Descripcion;
END
GO

-- -----------------------------------------------------------------------------
-- 6) Items de menú -- hijos de "Personal" (Fase 1, IdMenu
--    C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71). Mismo criterio que Fase 1: no
--    idempotente, revisar Configuración > Items de Menú antes de repetir.
-- -----------------------------------------------------------------------------

EXEC dbo.INS_MenuItem
    @IdMenu = 'F6D4C8B5-BE7A-4D5B-AC4F-6A9DB17D5FA4',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-regimen',
    @Title = 'Régimen laboral',
    @Icon = 'bi bi-gear',
    @Url = '/personal/regimen',
    @Target = NULL,
    @DisplayOrder = 3,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

EXEC dbo.INS_MenuItem
    @IdMenu = '07E5D9C6-CF8B-4E6C-BD5A-7BAEC28E6A05',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-vacaciones',
    @Title = 'Vacaciones',
    @Icon = 'bi bi-airplane',
    @Url = '/personal/vacaciones',
    @Target = NULL,
    @DisplayOrder = 4,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

EXEC dbo.INS_MenuItem
    @IdMenu = '18F6EAD7-D09C-4F7D-CE6B-8CBFD39F7B16',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-boletas',
    @Title = 'Boletas de pago',
    @Icon = 'bi bi-receipt',
    @Url = '/personal/boletas',
    @Target = NULL,
    @DisplayOrder = 5,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
