-- =============================================================================
-- Módulo Personal y Planillas -- Fase 1 (núcleo operativo): Personal, Turno,
-- rotación entre edificios y registro de horas/feriados. NO incluye cálculo
-- de planillas (ConfiguracionRegimenLaboral, ParametrosLegales, Vacaciones,
-- BoletaPago) -- eso es Fase 2, ver especificación funcional del 14 sept 2026.
--
-- Empleador = la Cuenta (administradora), no el edificio -- Personal/Turno/
-- ConfiguracionFeriados cuelgan de IdAccount, igual que Building.IdAccount
-- (Docs/Design-Account-Facturacion.md). "Rotación" es sólo una segunda
-- asignación (AsignacionPersonalEdificio) con su propio rango de fechas, no
-- un contrato nuevo por edificio.
--
-- Tablas 100% nuevas -- no toca nada existente. Auditoría inline
-- (CreatedBy/CreatedOn/ModifiedBy/ModifiedOn) desde el INSERT, mismo patrón
-- que Database/Scripts/2026-09-02_05_Incidents.sql -- no hace falta el SP de
-- "estampado" aparte de 2026-09-01_01 (esa técnica era para retrofitear
-- auditoría en tablas viejas sin tocar sus SPs).
--
-- IdAccount/IdBuilding/IdPersonal/IdTurno no llevan FK real a propósito --
-- mismo criterio que Building.IdAccount (nullable, fail-open) y el resto del
-- esquema reciente: evita que un INSERT en el orden equivocado tire error de
-- constraint mientras el módulo está recién naciendo.
--
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Tablas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Personal')
BEGIN
    CREATE TABLE dbo.Personal
    (
        IdPersonal          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount            UNIQUEIDENTIFIER NOT NULL,
        DNI                  NVARCHAR(20)     NOT NULL,
        Nombres              NVARCHAR(150)    NOT NULL,
        Apellidos            NVARCHAR(150)    NOT NULL,
        Cargo                NVARCHAR(100)    NOT NULL,   -- Conserje/Seguridad/Limpieza/Mantenimiento/...
        FechaIngreso         DATE             NOT NULL,
        FechaCese            DATE             NULL,
        RemuneracionBase     DECIMAL(18,2)    NOT NULL,
        SistemaPensionario   NVARCHAR(20)     NOT NULL DEFAULT ('ONP'), -- ONP/AFP
        Telefono             NVARCHAR(30)     NULL,
        IsActive             BIT              NOT NULL DEFAULT (1),
        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedOn            DATETIME2        NOT NULL,
        ModifiedBy           NVARCHAR(256)    NULL,
        ModifiedOn           DATETIME2        NULL
    );

    CREATE INDEX IX_Personal_Account ON dbo.Personal (IdAccount);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Turno')
BEGIN
    CREATE TABLE dbo.Turno
    (
        IdTurno      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount    UNIQUEIDENTIFIER NOT NULL,
        Nombre       NVARCHAR(100)    NOT NULL,   -- "Diurno 7am-3pm"
        HoraInicio   TIME(0)          NOT NULL,
        HoraFin      TIME(0)          NOT NULL,
        DiasSemana   NVARCHAR(20)     NOT NULL,   -- CSV de días 1(lunes)..7(domingo), ej "1,2,3,4,5"
        IsActive     BIT              NOT NULL DEFAULT (1),
        CreatedBy    NVARCHAR(256)    NOT NULL,
        CreatedOn    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_Turno_Account ON dbo.Turno (IdAccount);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AsignacionPersonalTurno')
BEGIN
    CREATE TABLE dbo.AsignacionPersonalTurno
    (
        IdAsignacionPersonalTurno UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal                UNIQUEIDENTIFIER NOT NULL,
        IdTurno                   UNIQUEIDENTIFIER NOT NULL,
        FechaDesde                DATE             NOT NULL,
        FechaHasta                DATE             NULL,       -- NULL = vigente
        CreatedBy                 NVARCHAR(256)    NOT NULL,
        CreatedOn                 DATETIME2        NOT NULL
    );

    CREATE INDEX IX_AsignacionPersonalTurno_Personal ON dbo.AsignacionPersonalTurno (IdPersonal, FechaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AsignacionPersonalEdificio')
BEGIN
    CREATE TABLE dbo.AsignacionPersonalEdificio
    (
        IdAsignacionPersonalEdificio UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal                   UNIQUEIDENTIFIER NOT NULL,
        IdBuilding                   UNIQUEIDENTIFIER NOT NULL,
        FechaDesde                   DATE             NOT NULL,
        FechaHasta                   DATE             NULL,        -- NULL = vigente
        PorcentajeDedicacion         DECIMAL(5,2)     NOT NULL DEFAULT (100),
        CreatedBy                    NVARCHAR(256)    NOT NULL,
        CreatedOn                    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_AsignacionPersonalEdificio_Personal ON dbo.AsignacionPersonalEdificio (IdPersonal, FechaDesde);
    CREATE INDEX IX_AsignacionPersonalEdificio_Building ON dbo.AsignacionPersonalEdificio (IdBuilding, FechaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'RegistroHoras')
BEGIN
    CREATE TABLE dbo.RegistroHoras
    (
        IdRegistroHoras       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPersonal            UNIQUEIDENTIFIER NOT NULL,
        IdAsignacionEdificio  UNIQUEIDENTIFIER NOT NULL,  -- de qué edificio/rotación es este día -> AsignacionPersonalEdificio
        Fecha                 DATE             NOT NULL,
        HorasOrdinarias       DECIMAL(5,2)     NOT NULL DEFAULT (0),
        HorasExtra25          DECIMAL(5,2)     NOT NULL DEFAULT (0),
        HorasExtra35          DECIMAL(5,2)     NOT NULL DEFAULT (0),
        EsFeriado             BIT              NOT NULL DEFAULT (0),
        Observaciones         NVARCHAR(300)    NULL,
        IdUsuarioRegistro     UNIQUEIDENTIFIER NOT NULL,  -- MVP: sólo lo llena el administrador, nunca el propio Personal
        CreatedOn             DATETIME2        NOT NULL,

        CONSTRAINT UQ_RegistroHoras_Personal_Fecha UNIQUE (IdPersonal, Fecha)
    );

    CREATE INDEX IX_RegistroHoras_Asignacion ON dbo.RegistroHoras (IdAsignacionEdificio, Fecha);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ConfiguracionFeriados')
BEGIN
    CREATE TABLE dbo.ConfiguracionFeriados
    (
        IdConfiguracionFeriados UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount               UNIQUEIDENTIFIER NULL,   -- NULL = feriado nacional (visible a todas las Cuentas)
        Fecha                   DATE             NOT NULL,
        Nombre                  NVARCHAR(150)    NOT NULL,
        Tipo                    NVARCHAR(30)     NOT NULL DEFAULT ('Nacional'), -- Nacional/NoLaborableCompensable/Cuenta
        Anio                    INT              NOT NULL,
        CreatedBy               NVARCHAR(256)    NOT NULL,
        CreatedOn                DATETIME2        NOT NULL
    );

    CREATE INDEX IX_ConfiguracionFeriados_Account_Anio ON dbo.ConfiguracionFeriados (IdAccount, Anio);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Semilla: feriados nacionales 2026 (IdAccount = NULL) -- editable después
--    desde /personal/feriados, no una lista fija en código (sección 7 de la
--    especificación: el Ejecutivo agrega no-laborables compensables por
--    decreto supremo cada año).
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM dbo.ConfiguracionFeriados WHERE IdAccount IS NULL AND Anio = 2026)
BEGIN
    INSERT INTO dbo.ConfiguracionFeriados (IdConfiguracionFeriados, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn)
    VALUES
        (NEWID(), NULL, '2026-01-01', 'Año Nuevo',                                'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-04-02', 'Jueves Santo',                             'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-04-03', 'Viernes Santo',                            'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-05-01', 'Día del Trabajo',                          'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-06-07', 'Batalla de Arica y Día de la Bandera',     'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-06-29', 'San Pedro y San Pablo',                    'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-07-23', 'Día de la Fuerza Aérea del Perú',          'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-07-28', 'Fiestas Patrias',                          'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-07-29', 'Fiestas Patrias',                          'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-08-06', 'Batalla de Junín',                        'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-08-30', 'Santa Rosa de Lima',                       'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-10-08', 'Combate de Angamos',                       'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-11-01', 'Día de Todos los Santos',                  'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-12-08', 'Inmaculada Concepción',                    'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-12-09', 'Batalla de Ayacucho',                      'Nacional', 2026, 'system', SYSUTCDATETIME()),
        (NEWID(), NULL, '2026-12-25', 'Navidad',                                  'Nacional', 2026, 'system', SYSUTCDATETIME());
END
GO

-- -----------------------------------------------------------------------------
-- 3) Stored Procedures -- Personal
-- -----------------------------------------------------------------------------

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
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Personal
        (IdPersonal, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese, RemuneracionBase, SistemaPensionario, Telefono, IsActive, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn)
    VALUES
        (@IdPersonal, @IdAccount, @DNI, @Nombres, @Apellidos, @Cargo, @FechaIngreso, NULL, @RemuneracionBase, @SistemaPensionario, @Telefono, 1, @CreatedBy, SYSUTCDATETIME(), NULL, NULL);
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
    @ModifiedBy NVARCHAR(256)
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
        ModifiedOn = SYSUTCDATETIME()
    WHERE IdPersonal = @IdPersonal;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_PersonalByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPersonal, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
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
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.Personal
    WHERE IdPersonal = @IdPersonal;
END
GO

-- -----------------------------------------------------------------------------
-- 4) Stored Procedures -- Turno
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Turno
    @IdTurno UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @Nombre NVARCHAR(100),
    @HoraInicio TIME(0),
    @HoraFin TIME(0),
    @DiasSemana NVARCHAR(20),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Turno (IdTurno, IdAccount, Nombre, HoraInicio, HoraFin, DiasSemana, IsActive, CreatedBy, CreatedOn)
    VALUES (@IdTurno, @IdAccount, @Nombre, @HoraInicio, @HoraFin, @DiasSemana, 1, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Turno
    @IdTurno UNIQUEIDENTIFIER,
    @Nombre NVARCHAR(100),
    @HoraInicio TIME(0),
    @HoraFin TIME(0),
    @DiasSemana NVARCHAR(20),
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Turno
    SET Nombre = @Nombre,
        HoraInicio = @HoraInicio,
        HoraFin = @HoraFin,
        DiasSemana = @DiasSemana,
        IsActive = @IsActive
    WHERE IdTurno = @IdTurno;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_TurnosByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdTurno, IdAccount, Nombre, HoraInicio, HoraFin, DiasSemana, IsActive, CreatedBy, CreatedOn
    FROM dbo.Turno
    WHERE IdAccount = @IdAccount
    ORDER BY Nombre;
END
GO

-- -----------------------------------------------------------------------------
-- 5) Stored Procedures -- AsignacionPersonalTurno
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_AsignacionPersonalTurno
    @IdAsignacionPersonalTurno UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @IdTurno UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    -- Cierra cualquier asignación vigente anterior de este Personal (FechaHasta
    -- NULL) el día antes de que arranque la nueva -- evita que dos turnos
    -- queden "vigentes" a la vez sin que nadie haya cerrado el anterior a mano.
    UPDATE dbo.AsignacionPersonalTurno
    SET FechaHasta = DATEADD(DAY, -1, @FechaDesde)
    WHERE IdPersonal = @IdPersonal AND FechaHasta IS NULL;

    INSERT INTO dbo.AsignacionPersonalTurno (IdAsignacionPersonalTurno, IdPersonal, IdTurno, FechaDesde, FechaHasta, CreatedBy, CreatedOn)
    VALUES (@IdAsignacionPersonalTurno, @IdPersonal, @IdTurno, @FechaDesde, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesTurnoByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdAsignacionPersonalTurno, a.IdPersonal, a.IdTurno, a.FechaDesde, a.FechaHasta, a.CreatedBy, a.CreatedOn,
           t.Nombre AS TurnoNombre, t.HoraInicio, t.HoraFin, t.DiasSemana
    FROM dbo.AsignacionPersonalTurno a
    LEFT JOIN dbo.Turno t ON t.IdTurno = a.IdTurno
    WHERE a.IdPersonal = @IdPersonal
    ORDER BY a.FechaDesde DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 6) Stored Procedures -- AsignacionPersonalEdificio (rotación)
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_AsignacionPersonalEdificio
    @IdAsignacionPersonalEdificio UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @PorcentajeDedicacion DECIMAL(5,2),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    -- A diferencia de Turno, acá NO se cierra ninguna asignación previa: un
    -- Personal puede rotar entre VARIOS edificios a la vez (ej. lunes/miércoles/
    -- viernes en A, martes/jueves en B) -- ver sección 5 de la especificación.
    -- Cerrar la anterior sería asumir todo-o-nada, que es justo lo que este
    -- modelo evita.
    INSERT INTO dbo.AsignacionPersonalEdificio (IdAsignacionPersonalEdificio, IdPersonal, IdBuilding, FechaDesde, FechaHasta, PorcentajeDedicacion, CreatedBy, CreatedOn)
    VALUES (@IdAsignacionPersonalEdificio, @IdPersonal, @IdBuilding, @FechaDesde, NULL, @PorcentajeDedicacion, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Cierra una asignación (fin de la rotación a ese edificio) -- separado del
-- INSERT porque es la única edición real que ocurre en el MVP (el % de
-- dedicación de una asignación ya cerrada no se retoca).
CREATE OR ALTER PROCEDURE dbo.UPD_AsignacionPersonalEdificio_Cerrar
    @IdAsignacionPersonalEdificio UNIQUEIDENTIFIER,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AsignacionPersonalEdificio
    SET FechaHasta = @FechaHasta
    WHERE IdAsignacionPersonalEdificio = @IdAsignacionPersonalEdificio;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdAsignacionPersonalEdificio, a.IdPersonal, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName
    FROM dbo.AsignacionPersonalEdificio a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE a.IdPersonal = @IdPersonal
    ORDER BY a.FechaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdAsignacionPersonalEdificio, a.IdPersonal, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.AsignacionPersonalEdificio a
    LEFT JOIN dbo.Personal p ON p.IdPersonal = a.IdPersonal
    WHERE a.IdBuilding = @IdBuilding AND a.FechaHasta IS NULL
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- 7) Stored Procedures -- RegistroHoras
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_RegistroHoras
    @IdRegistroHoras UNIQUEIDENTIFIER,
    @IdPersonal UNIQUEIDENTIFIER,
    @IdAsignacionEdificio UNIQUEIDENTIFIER,
    @Fecha DATE,
    @HorasOrdinarias DECIMAL(5,2),
    @HorasExtra25 DECIMAL(5,2),
    @HorasExtra35 DECIMAL(5,2),
    @EsFeriado BIT,
    @Observaciones NVARCHAR(300) = NULL,
    @IdUsuarioRegistro UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.RegistroHoras
        (IdRegistroHoras, IdPersonal, IdAsignacionEdificio, Fecha, HorasOrdinarias, HorasExtra25, HorasExtra35, EsFeriado, Observaciones, IdUsuarioRegistro, CreatedOn)
    VALUES
        (@IdRegistroHoras, @IdPersonal, @IdAsignacionEdificio, @Fecha, @HorasOrdinarias, @HorasExtra25, @HorasExtra35, @EsFeriado, @Observaciones, @IdUsuarioRegistro, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_RegistroHoras
    @IdRegistroHoras UNIQUEIDENTIFIER,
    @HorasOrdinarias DECIMAL(5,2),
    @HorasExtra25 DECIMAL(5,2),
    @HorasExtra35 DECIMAL(5,2),
    @EsFeriado BIT,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.RegistroHoras
    SET HorasOrdinarias = @HorasOrdinarias,
        HorasExtra25 = @HorasExtra25,
        HorasExtra35 = @HorasExtra35,
        EsFeriado = @EsFeriado,
        Observaciones = @Observaciones
    WHERE IdRegistroHoras = @IdRegistroHoras;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_RegistroHoras
    @IdRegistroHoras UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.RegistroHoras WHERE IdRegistroHoras = @IdRegistroHoras;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_RegistroHorasByPersonal
    @IdPersonal UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.IdRegistroHoras, r.IdPersonal, r.IdAsignacionEdificio, r.Fecha,
           r.HorasOrdinarias, r.HorasExtra25, r.HorasExtra35, r.EsFeriado, r.Observaciones,
           r.IdUsuarioRegistro, r.CreatedOn,
           b.Name AS BuildingName
    FROM dbo.RegistroHoras r
    LEFT JOIN dbo.AsignacionPersonalEdificio a ON a.IdAsignacionPersonalEdificio = r.IdAsignacionEdificio
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE r.IdPersonal = @IdPersonal
      AND r.Fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY r.Fecha DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 8) Stored Procedures -- ConfiguracionFeriados
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_ConfiguracionFeriados
    @IdConfiguracionFeriados UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER = NULL,
    @Fecha DATE,
    @Nombre NVARCHAR(150),
    @Tipo NVARCHAR(30),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ConfiguracionFeriados (IdConfiguracionFeriados, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn)
    VALUES (@IdConfiguracionFeriados, @IdAccount, @Fecha, @Nombre, @Tipo, YEAR(@Fecha), @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ConfiguracionFeriados
    @IdConfiguracionFeriados UNIQUEIDENTIFIER,
    @Fecha DATE,
    @Nombre NVARCHAR(150),
    @Tipo NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ConfiguracionFeriados
    SET Fecha = @Fecha,
        Nombre = @Nombre,
        Tipo = @Tipo,
        Anio = YEAR(@Fecha)
    WHERE IdConfiguracionFeriados = @IdConfiguracionFeriados;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_ConfiguracionFeriados
    @IdConfiguracionFeriados UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.ConfiguracionFeriados WHERE IdConfiguracionFeriados = @IdConfiguracionFeriados;
END
GO

-- @IdAccount = NULL trae SOLO los nacionales; con un valor real trae los
-- nacionales + los propios de esa Cuenta para el año pedido.
CREATE OR ALTER PROCEDURE dbo.GET_FeriadosByAccountAndYear
    @IdAccount UNIQUEIDENTIFIER = NULL,
    @Anio INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdConfiguracionFeriados, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn
    FROM dbo.ConfiguracionFeriados
    WHERE Anio = @Anio
      AND (IdAccount IS NULL OR IdAccount = @IdAccount)
    ORDER BY Fecha;
END
GO

-- -----------------------------------------------------------------------------
-- 9) Items de menú "Personal" -- vía dbo.INS_MenuItem, mismo criterio que
--    Database/Scripts/2026-09-02_05_Incidents.sql. Gateo de acceso es por rol
--    directo (Administrador/SysAdmin) en cada .razor, no por clave de permiso
--    nueva -- mismo criterio que Incidentes/Comunicados/Reservas.
--
-- NO es idempotente este paso puntual (mismo motivo que Incidents.sql: no hay
-- forma confiable de IF NOT EXISTS contra MenuItems desde acá). Si corrés el
-- script dos veces, revisá Configuración > Items de Menú antes de repetir.
-- -----------------------------------------------------------------------------

EXEC dbo.INS_MenuItem
    @IdMenu = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @IdParent = NULL,
    @ItemKey = 'personal',
    @Title = 'Personal',
    @Icon = 'bi bi-people',
    @Url = '/personal',
    @Target = NULL,
    @DisplayOrder = 55,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

EXEC dbo.INS_MenuItem
    @IdMenu = 'D4B2A6F3-9C5E-4B3F-8A2D-4E7C9F5B3D82',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-turnos',
    @Title = 'Turnos',
    @Icon = 'bi bi-clock-history',
    @Url = '/personal/turnos',
    @Target = NULL,
    @DisplayOrder = 1,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

EXEC dbo.INS_MenuItem
    @IdMenu = 'E5C3B7A4-AD6F-4C4A-9B3E-5F8DA06C4E93',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'personal-feriados',
    @Title = 'Feriados',
    @Icon = 'bi bi-calendar-event',
    @Url = '/personal/feriados',
    @Target = NULL,
    @DisplayOrder = 2,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
