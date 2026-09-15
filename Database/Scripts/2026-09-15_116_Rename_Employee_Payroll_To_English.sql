-- =============================================================================
-- Renombra a inglés el módulo "Personal y Planillas" (nomenclatura del
-- equipo: tablas, stored procedures, permisos y rutas de página van en
-- inglés -- el texto que ve el usuario final queda en español). Reemplaza
-- por completo lo creado en 2026-09-15_108/109/110/111 con nombres nuevos.
--
-- Personal -> Employee, Turno -> Shift, AsignacionPersonalTurno ->
-- EmployeeShiftAssignment, AsignacionPersonalEdificio ->
-- EmployeeBuildingAssignment, RegistroHoras -> TimeEntry,
-- ConfiguracionFeriados -> HolidayConfiguration, ConfiguracionRegimenLaboral
-- -> LaborRegimeConfiguration, ParametrosLegales -> LegalParameters,
-- Vacaciones -> Vacation, PermisoLicencia -> LeaveRequest, BoletaPago ->
-- Payslip, BoletaPagoDetalle -> PayslipDetail.
--
-- Ambiente de desarrollo/pruebas sin datos reales que preservar -- se hace
-- DROP de las tablas/SPs viejas y CREATE de las nuevas, no una migración de
-- datos. Los 7 MenuItems ya sembrados por el script original se actualizan
-- in place (UPDATE) al final para que una base que ya corrió 108-111 quede
-- consistente sin duplicar filas.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ===================== DROP procedimientos viejos =====================
DROP PROCEDURE IF EXISTS dbo.DEL_ConfiguracionFeriados;
DROP PROCEDURE IF EXISTS dbo.DEL_RegistroHoras;
DROP PROCEDURE IF EXISTS dbo.GET_AsignacionesEdificioByBuilding;
DROP PROCEDURE IF EXISTS dbo.GET_AsignacionesEdificioByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_AsignacionesTurnoByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_BoletaById;
DROP PROCEDURE IF EXISTS dbo.GET_BoletaPagoDetalleByBoleta;
DROP PROCEDURE IF EXISTS dbo.GET_BoletasByAccountAndPeriodo;
DROP PROCEDURE IF EXISTS dbo.GET_BoletasByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_ConfiguracionRegimenLaboralHistorial;
DROP PROCEDURE IF EXISTS dbo.GET_ConfiguracionRegimenLaboralVigente;
DROP PROCEDURE IF EXISTS dbo.GET_FeriadosByAccountAndYear;
DROP PROCEDURE IF EXISTS dbo.GET_ParametrosLegalesByAccountAndYear;
DROP PROCEDURE IF EXISTS dbo.GET_PermisoLicenciaByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_PermisoLicenciaPendientesByAccount;
DROP PROCEDURE IF EXISTS dbo.GET_PermisoLicenciaSinGoceDiasByPersonalMes;
DROP PROCEDURE IF EXISTS dbo.GET_PersonalByAccount;
DROP PROCEDURE IF EXISTS dbo.GET_PersonalById;
DROP PROCEDURE IF EXISTS dbo.GET_RegistroHorasByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_TurnosByAccount;
DROP PROCEDURE IF EXISTS dbo.GET_VacacionesByPersonal;
DROP PROCEDURE IF EXISTS dbo.GET_VacacionesGozadasByPersonalAnio;
DROP PROCEDURE IF EXISTS dbo.GET_VacacionesPendientesByAccount;
DROP PROCEDURE IF EXISTS dbo.INS_AsignacionPersonalEdificio;
DROP PROCEDURE IF EXISTS dbo.INS_AsignacionPersonalTurno;
DROP PROCEDURE IF EXISTS dbo.INS_BoletaPago;
DROP PROCEDURE IF EXISTS dbo.INS_BoletaPagoDetalle;
DROP PROCEDURE IF EXISTS dbo.INS_ConfiguracionFeriados;
DROP PROCEDURE IF EXISTS dbo.INS_ConfiguracionRegimenLaboral;
DROP PROCEDURE IF EXISTS dbo.INS_ParametrosLegales;
DROP PROCEDURE IF EXISTS dbo.INS_PermisoLicencia;
DROP PROCEDURE IF EXISTS dbo.INS_Personal;
DROP PROCEDURE IF EXISTS dbo.INS_RegistroHoras;
DROP PROCEDURE IF EXISTS dbo.INS_Turno;
DROP PROCEDURE IF EXISTS dbo.INS_Vacaciones;
DROP PROCEDURE IF EXISTS dbo.UPD_AsignacionPersonalEdificio_Cerrar;
DROP PROCEDURE IF EXISTS dbo.UPD_ConfiguracionFeriados;
DROP PROCEDURE IF EXISTS dbo.UPD_ParametrosLegales;
DROP PROCEDURE IF EXISTS dbo.UPD_PermisoLicenciaEstado;
DROP PROCEDURE IF EXISTS dbo.UPD_Personal;
DROP PROCEDURE IF EXISTS dbo.UPD_RegistroHoras;
DROP PROCEDURE IF EXISTS dbo.UPD_Turno;
DROP PROCEDURE IF EXISTS dbo.UPD_VacacionesEstado;
GO

-- ===================== DROP tablas viejas (hijas primero) =====================
DROP TABLE IF EXISTS dbo.BoletaPagoDetalle;
DROP TABLE IF EXISTS dbo.BoletaPago;
DROP TABLE IF EXISTS dbo.PermisoLicencia;
DROP TABLE IF EXISTS dbo.Vacaciones;
DROP TABLE IF EXISTS dbo.ParametrosLegales;
DROP TABLE IF EXISTS dbo.ConfiguracionRegimenLaboral;
DROP TABLE IF EXISTS dbo.ConfiguracionFeriados;
DROP TABLE IF EXISTS dbo.RegistroHoras;
DROP TABLE IF EXISTS dbo.AsignacionPersonalEdificio;
DROP TABLE IF EXISTS dbo.AsignacionPersonalTurno;
DROP TABLE IF EXISTS dbo.Turno;
DROP TABLE IF EXISTS dbo.Personal;
GO

-- =============================================================================
-- Módulo Employee y Planillas -- Fase 1 (núcleo operativo): Employee, Shift,
-- rotación entre edificios y registro de horas/feriados. NO incluye cálculo
-- de planillas (LaborRegimeConfiguration, LegalParameters, Vacation,
-- Payslip) -- eso es Fase 2, ver especificación funcional del 14 sept 2026.
--
-- Empleador = la Cuenta (administradora), no el edificio -- Employee/Shift/
-- HolidayConfiguration cuelgan de IdAccount, igual que Building.IdAccount
-- (Docs/Design-Account-Facturacion.md). "Rotación" es sólo una segunda
-- asignación (EmployeeBuildingAssignment) con su propio rango de fechas, no
-- un contrato nuevo por edificio.
--
-- Tablas 100% nuevas -- no toca nada existente. Auditoría inline
-- (CreatedBy/CreatedOn/ModifiedBy/ModifiedOn) desde el INSERT, mismo patrón
-- que Database/Scripts/2026-09-02_05_Incidents.sql -- no hace falta el SP de
-- "estampado" aparte de 2026-09-01_01 (esa técnica era para retrofitear
-- auditoría en tablas viejas sin tocar sus SPs).
--
-- IdAccount/IdBuilding/IdEmployee/IdShift no llevan FK real a propósito --
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Employee')
BEGIN
    CREATE TABLE dbo.Employee
    (
        IdEmployee          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
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

    CREATE INDEX IX_Employee_Account ON dbo.Employee (IdAccount);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Shift')
BEGIN
    CREATE TABLE dbo.Shift
    (
        IdShift      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount    UNIQUEIDENTIFIER NOT NULL,
        Nombre       NVARCHAR(100)    NOT NULL,   -- "Diurno 7am-3pm"
        HoraInicio   TIME(0)          NOT NULL,
        HoraFin      TIME(0)          NOT NULL,
        DiasSemana   NVARCHAR(20)     NOT NULL,   -- CSV de días 1(lunes)..7(domingo), ej "1,2,3,4,5"
        IsActive     BIT              NOT NULL DEFAULT (1),
        CreatedBy    NVARCHAR(256)    NOT NULL,
        CreatedOn    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_Shift_Account ON dbo.Shift (IdAccount);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeShiftAssignment')
BEGIN
    CREATE TABLE dbo.EmployeeShiftAssignment
    (
        IdEmployeeShiftAssignment UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee                UNIQUEIDENTIFIER NOT NULL,
        IdShift                   UNIQUEIDENTIFIER NOT NULL,
        FechaDesde                DATE             NOT NULL,
        FechaHasta                DATE             NULL,       -- NULL = vigente
        CreatedBy                 NVARCHAR(256)    NOT NULL,
        CreatedOn                 DATETIME2        NOT NULL
    );

    CREATE INDEX IX_EmployeeShiftAssignment_Employee ON dbo.EmployeeShiftAssignment (IdEmployee, FechaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EmployeeBuildingAssignment')
BEGIN
    CREATE TABLE dbo.EmployeeBuildingAssignment
    (
        IdEmployeeBuildingAssignment UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee                   UNIQUEIDENTIFIER NOT NULL,
        IdBuilding                   UNIQUEIDENTIFIER NOT NULL,
        FechaDesde                   DATE             NOT NULL,
        FechaHasta                   DATE             NULL,        -- NULL = vigente
        PorcentajeDedicacion         DECIMAL(5,2)     NOT NULL DEFAULT (100),
        CreatedBy                    NVARCHAR(256)    NOT NULL,
        CreatedOn                    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_EmployeeBuildingAssignment_Employee ON dbo.EmployeeBuildingAssignment (IdEmployee, FechaDesde);
    CREATE INDEX IX_EmployeeBuildingAssignment_Building ON dbo.EmployeeBuildingAssignment (IdBuilding, FechaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TimeEntry')
BEGIN
    CREATE TABLE dbo.TimeEntry
    (
        IdTimeEntry       UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee            UNIQUEIDENTIFIER NOT NULL,
        IdAsignacionEdificio  UNIQUEIDENTIFIER NOT NULL,  -- de qué edificio/rotación es este día -> EmployeeBuildingAssignment
        Fecha                 DATE             NOT NULL,
        HorasOrdinarias       DECIMAL(5,2)     NOT NULL DEFAULT (0),
        HorasExtra25          DECIMAL(5,2)     NOT NULL DEFAULT (0),
        HorasExtra35          DECIMAL(5,2)     NOT NULL DEFAULT (0),
        EsFeriado             BIT              NOT NULL DEFAULT (0),
        Observaciones         NVARCHAR(300)    NULL,
        IdUsuarioRegistro     UNIQUEIDENTIFIER NOT NULL,  -- MVP: sólo lo llena el administrador, nunca el propio Employee
        CreatedOn             DATETIME2        NOT NULL,

        CONSTRAINT UQ_TimeEntry_Employee_Fecha UNIQUE (IdEmployee, Fecha)
    );

    CREATE INDEX IX_TimeEntry_Asignacion ON dbo.TimeEntry (IdAsignacionEdificio, Fecha);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'HolidayConfiguration')
BEGIN
    CREATE TABLE dbo.HolidayConfiguration
    (
        IdHolidayConfiguration UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount               UNIQUEIDENTIFIER NULL,   -- NULL = feriado nacional (visible a todas las Cuentas)
        Fecha                   DATE             NOT NULL,
        Nombre                  NVARCHAR(150)    NOT NULL,
        Tipo                    NVARCHAR(30)     NOT NULL DEFAULT ('Nacional'), -- Nacional/NoLaborableCompensable/Cuenta
        Anio                    INT              NOT NULL,
        CreatedBy               NVARCHAR(256)    NOT NULL,
        CreatedOn                DATETIME2        NOT NULL
    );

    CREATE INDEX IX_HolidayConfiguration_Account_Anio ON dbo.HolidayConfiguration (IdAccount, Anio);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Semilla: feriados nacionales 2026 (IdAccount = NULL) -- editable después
--    desde /personal/feriados, no una lista fija en código (sección 7 de la
--    especificación: el Ejecutivo agrega no-laborables compensables por
--    decreto supremo cada año).
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM dbo.HolidayConfiguration WHERE IdAccount IS NULL AND Anio = 2026)
BEGIN
    INSERT INTO dbo.HolidayConfiguration (IdHolidayConfiguration, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn)
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
-- 3) Stored Procedures -- Employee
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Employee
    @IdEmployee UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.Employee
        (IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese, RemuneracionBase, SistemaPensionario, Telefono, IsActive, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn)
    VALUES
        (@IdEmployee, @IdAccount, @DNI, @Nombres, @Apellidos, @Cargo, @FechaIngreso, NULL, @RemuneracionBase, @SistemaPensionario, @Telefono, 1, @CreatedBy, SYSUTCDATETIME(), NULL, NULL);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Employee
    @IdEmployee UNIQUEIDENTIFIER,
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
    UPDATE dbo.Employee
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
    WHERE IdEmployee = @IdEmployee;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_EmployeeByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.Employee
    WHERE IdAccount = @IdAccount
    ORDER BY Apellidos, Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_EmployeeById
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.Employee
    WHERE IdEmployee = @IdEmployee;
END
GO

-- -----------------------------------------------------------------------------
-- 4) Stored Procedures -- Shift
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Shift
    @IdShift UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @Nombre NVARCHAR(100),
    @HoraInicio TIME(0),
    @HoraFin TIME(0),
    @DiasSemana NVARCHAR(20),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Shift (IdShift, IdAccount, Nombre, HoraInicio, HoraFin, DiasSemana, IsActive, CreatedBy, CreatedOn)
    VALUES (@IdShift, @IdAccount, @Nombre, @HoraInicio, @HoraFin, @DiasSemana, 1, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Shift
    @IdShift UNIQUEIDENTIFIER,
    @Nombre NVARCHAR(100),
    @HoraInicio TIME(0),
    @HoraFin TIME(0),
    @DiasSemana NVARCHAR(20),
    @IsActive BIT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Shift
    SET Nombre = @Nombre,
        HoraInicio = @HoraInicio,
        HoraFin = @HoraFin,
        DiasSemana = @DiasSemana,
        IsActive = @IsActive
    WHERE IdShift = @IdShift;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ShiftsByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdShift, IdAccount, Nombre, HoraInicio, HoraFin, DiasSemana, IsActive, CreatedBy, CreatedOn
    FROM dbo.Shift
    WHERE IdAccount = @IdAccount
    ORDER BY Nombre;
END
GO

-- -----------------------------------------------------------------------------
-- 5) Stored Procedures -- EmployeeShiftAssignment
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_EmployeeShiftAssignment
    @IdEmployeeShiftAssignment UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
    @IdShift UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    -- Cierra cualquier asignación vigente anterior de este Employee (FechaHasta
    -- NULL) el día antes de que arranque la nueva -- evita que dos turnos
    -- queden "vigentes" a la vez sin que nadie haya cerrado el anterior a mano.
    UPDATE dbo.EmployeeShiftAssignment
    SET FechaHasta = DATEADD(DAY, -1, @FechaDesde)
    WHERE IdEmployee = @IdEmployee AND FechaHasta IS NULL;

    INSERT INTO dbo.EmployeeShiftAssignment (IdEmployeeShiftAssignment, IdEmployee, IdShift, FechaDesde, FechaHasta, CreatedBy, CreatedOn)
    VALUES (@IdEmployeeShiftAssignment, @IdEmployee, @IdShift, @FechaDesde, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesShiftByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeShiftAssignment, a.IdEmployee, a.IdShift, a.FechaDesde, a.FechaHasta, a.CreatedBy, a.CreatedOn,
           t.Nombre AS ShiftNombre, t.HoraInicio, t.HoraFin, t.DiasSemana
    FROM dbo.EmployeeShiftAssignment a
    LEFT JOIN dbo.Shift t ON t.IdShift = a.IdShift
    WHERE a.IdEmployee = @IdEmployee
    ORDER BY a.FechaDesde DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 6) Stored Procedures -- EmployeeBuildingAssignment (rotación)
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_EmployeeBuildingAssignment
    @IdEmployeeBuildingAssignment UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @PorcentajeDedicacion DECIMAL(5,2),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    -- A diferencia de Shift, acá NO se cierra ninguna asignación previa: un
    -- Employee puede rotar entre VARIOS edificios a la vez (ej. lunes/miércoles/
    -- viernes en A, martes/jueves en B) -- ver sección 5 de la especificación.
    -- Cerrar la anterior sería asumir todo-o-nada, que es justo lo que este
    -- modelo evita.
    INSERT INTO dbo.EmployeeBuildingAssignment (IdEmployeeBuildingAssignment, IdEmployee, IdBuilding, FechaDesde, FechaHasta, PorcentajeDedicacion, CreatedBy, CreatedOn)
    VALUES (@IdEmployeeBuildingAssignment, @IdEmployee, @IdBuilding, @FechaDesde, NULL, @PorcentajeDedicacion, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Cierra una asignación (fin de la rotación a ese edificio) -- separado del
-- INSERT porque es la única edición real que ocurre en el MVP (el % de
-- dedicación de una asignación ya cerrada no se retoca).
CREATE OR ALTER PROCEDURE dbo.UPD_EmployeeBuildingAssignment_Cerrar
    @IdEmployeeBuildingAssignment UNIQUEIDENTIFIER,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.EmployeeBuildingAssignment
    SET FechaHasta = @FechaHasta
    WHERE IdEmployeeBuildingAssignment = @IdEmployeeBuildingAssignment;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE a.IdEmployee = @IdEmployee
    ORDER BY a.FechaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Employee p ON p.IdEmployee = a.IdEmployee
    WHERE a.IdBuilding = @IdBuilding AND a.FechaHasta IS NULL
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- 7) Stored Procedures -- TimeEntry
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_TimeEntry
    @IdTimeEntry UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.TimeEntry
        (IdTimeEntry, IdEmployee, IdAsignacionEdificio, Fecha, HorasOrdinarias, HorasExtra25, HorasExtra35, EsFeriado, Observaciones, IdUsuarioRegistro, CreatedOn)
    VALUES
        (@IdTimeEntry, @IdEmployee, @IdAsignacionEdificio, @Fecha, @HorasOrdinarias, @HorasExtra25, @HorasExtra35, @EsFeriado, @Observaciones, @IdUsuarioRegistro, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_TimeEntry
    @IdTimeEntry UNIQUEIDENTIFIER,
    @HorasOrdinarias DECIMAL(5,2),
    @HorasExtra25 DECIMAL(5,2),
    @HorasExtra35 DECIMAL(5,2),
    @EsFeriado BIT,
    @Observaciones NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.TimeEntry
    SET HorasOrdinarias = @HorasOrdinarias,
        HorasExtra25 = @HorasExtra25,
        HorasExtra35 = @HorasExtra35,
        EsFeriado = @EsFeriado,
        Observaciones = @Observaciones
    WHERE IdTimeEntry = @IdTimeEntry;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_TimeEntry
    @IdTimeEntry UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.TimeEntry WHERE IdTimeEntry = @IdTimeEntry;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_TimeEntryByEmployee
    @IdEmployee UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.IdTimeEntry, r.IdEmployee, r.IdAsignacionEdificio, r.Fecha,
           r.HorasOrdinarias, r.HorasExtra25, r.HorasExtra35, r.EsFeriado, r.Observaciones,
           r.IdUsuarioRegistro, r.CreatedOn,
           b.Name AS BuildingName
    FROM dbo.TimeEntry r
    LEFT JOIN dbo.EmployeeBuildingAssignment a ON a.IdEmployeeBuildingAssignment = r.IdAsignacionEdificio
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE r.IdEmployee = @IdEmployee
      AND r.Fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY r.Fecha DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 8) Stored Procedures -- HolidayConfiguration
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_HolidayConfiguration
    @IdHolidayConfiguration UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER = NULL,
    @Fecha DATE,
    @Nombre NVARCHAR(150),
    @Tipo NVARCHAR(30),
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.HolidayConfiguration (IdHolidayConfiguration, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn)
    VALUES (@IdHolidayConfiguration, @IdAccount, @Fecha, @Nombre, @Tipo, YEAR(@Fecha), @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_HolidayConfiguration
    @IdHolidayConfiguration UNIQUEIDENTIFIER,
    @Fecha DATE,
    @Nombre NVARCHAR(150),
    @Tipo NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.HolidayConfiguration
    SET Fecha = @Fecha,
        Nombre = @Nombre,
        Tipo = @Tipo,
        Anio = YEAR(@Fecha)
    WHERE IdHolidayConfiguration = @IdHolidayConfiguration;
END
GO

CREATE OR ALTER PROCEDURE dbo.DEL_HolidayConfiguration
    @IdHolidayConfiguration UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.HolidayConfiguration WHERE IdHolidayConfiguration = @IdHolidayConfiguration;
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
    SELECT IdHolidayConfiguration, IdAccount, Fecha, Nombre, Tipo, Anio, CreatedBy, CreatedOn
    FROM dbo.HolidayConfiguration
    WHERE Anio = @Anio
      AND (IdAccount IS NULL OR IdAccount = @IdAccount)
    ORDER BY Fecha;
END
GO

-- -----------------------------------------------------------------------------
-- 9) Items de menú "Employee" -- vía dbo.INS_MenuItem, mismo criterio que
--    Database/Scripts/2026-09-02_05_Incidents.sql. Gateo de acceso es por rol
--    directo (Administrador/SysAdmin) en cada .razor, no por clave de permiso
--    nueva -- mismo criterio que Incidentes/Comunicados/Reservas.
--
-- NO es idempotente este paso puntual (mismo motivo que Incidents.sql: no hay
-- forma confiable de IF NOT EXISTS contra MenuItems desde acá). Si corrés el
-- script dos veces, revisá Configuración > Items de Menú antes de repetir.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71')
EXEC dbo.INS_MenuItem
    @IdMenu = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @IdParent = NULL,
    @ItemKey = 'employees',
    @Title = 'Personal',
    @Icon = 'bi bi-people',
    @Url = '/employees',
    @Target = NULL,
    @DisplayOrder = 55,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'D4B2A6F3-9C5E-4B3F-8A2D-4E7C9F5B3D82')
EXEC dbo.INS_MenuItem
    @IdMenu = 'D4B2A6F3-9C5E-4B3F-8A2D-4E7C9F5B3D82',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-shifts',
    @Title = 'Turnos',
    @Icon = 'bi bi-clock-history',
    @Url = '/employees/shifts',
    @Target = NULL,
    @DisplayOrder = 1,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'E5C3B7A4-AD6F-4C4A-9B3E-5F8DA06C4E93')
EXEC dbo.INS_MenuItem
    @IdMenu = 'E5C3B7A4-AD6F-4C4A-9B3E-5F8DA06C4E93',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-holidays',
    @Title = 'Feriados',
    @Icon = 'bi bi-calendar-event',
    @Url = '/employees/holidays',
    @Target = NULL,
    @DisplayOrder = 2,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
-- =============================================================================
-- Módulo Employee y Planillas -- Fase 2 (Planillas): régimen laboral,
-- parámetros legales, vacaciones y boleta de pago. Requiere Fase 1
-- (Database/Scripts/2026-09-15_108_Employee_Planillas_Fase1.sql) ya aplicada.
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
-- 0) Employee.TieneHijos -- necesario para Asignación Familiar (Régimen
--    General, S/ 113/mes si tiene hijos, ver sección 3 de la especificación).
--    No existía en la Fase 1 porque hasta ahora Employee no distinguía esto.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Employee') AND name = 'TieneHijos')
BEGIN
    ALTER TABLE dbo.Employee ADD TieneHijos BIT NOT NULL CONSTRAINT DF_Employee_TieneHijos DEFAULT (0);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_Employee
    @IdEmployee UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.Employee
        (IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese, RemuneracionBase, SistemaPensionario, Telefono, IsActive, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos)
    VALUES
        (@IdEmployee, @IdAccount, @DNI, @Nombres, @Apellidos, @Cargo, @FechaIngreso, NULL, @RemuneracionBase, @SistemaPensionario, @Telefono, 1, @CreatedBy, SYSUTCDATETIME(), NULL, NULL, @TieneHijos);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Employee
    @IdEmployee UNIQUEIDENTIFIER,
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
    UPDATE dbo.Employee
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
    WHERE IdEmployee = @IdEmployee;
END
GO

-- GET_EmployeeByAccount/GET_EmployeeById ya traen SELECT * de columnas
-- explícitas (Fase 1) -- se recrean acá sólo para sumar TieneHijos.
CREATE OR ALTER PROCEDURE dbo.GET_EmployeeByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos
    FROM dbo.Employee
    WHERE IdAccount = @IdAccount
    ORDER BY Apellidos, Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_EmployeeById
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdEmployee, IdAccount, DNI, Nombres, Apellidos, Cargo, FechaIngreso, FechaCese,
           RemuneracionBase, SistemaPensionario, Telefono, IsActive,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn, TieneHijos
    FROM dbo.Employee
    WHERE IdEmployee = @IdEmployee;
END
GO

-- -----------------------------------------------------------------------------
-- 1) Tablas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LaborRegimeConfiguration')
BEGIN
    CREATE TABLE dbo.LaborRegimeConfiguration
    (
        IdLaborRegimeConfiguration UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdAccount            UNIQUEIDENTIFIER NOT NULL,
        TipoRegimen          NVARCHAR(20)     NOT NULL,  -- Microempresa/PequenaEmpresa/RegimenGeneral
        RUC                  NVARCHAR(20)     NULL,
        RazonSocial          NVARCHAR(200)    NULL,
        FechaVigenciaDesde   DATE             NOT NULL,
        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedOn            DATETIME2        NOT NULL
    );

    CREATE INDEX IX_LaborRegimeConfiguration_Account ON dbo.LaborRegimeConfiguration (IdAccount, FechaVigenciaDesde);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LegalParameters')
BEGIN
    CREATE TABLE dbo.LegalParameters
    (
        IdLegalParameters      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
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

        CONSTRAINT UQ_LegalParameters_Account_Anio UNIQUE (IdAccount, Anio)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vacation')
BEGIN
    -- Una fila por SOLICITUD de goce (no un saldo agregado) -- el saldo
    -- (ganados/gozados/pendientes) se deriva en tiempo de lectura, ver
    -- GET_VacationGozadasByEmployeeAnio. Reutiliza WorkflowAuditLog (Module =
    -- 'Vacation') para el historial de aprobación, mismo motor que
    -- Presupuesto/Cuota (sección 8 de la especificación) -- por eso Estado acá
    -- es sólo el estado ACTUAL, no el historial completo.
    CREATE TABLE dbo.Vacation
    (
        IdVacation      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee        UNIQUEIDENTIFIER NOT NULL,
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

    CREATE INDEX IX_Vacation_Employee_Anio ON dbo.Vacation (IdEmployee, Anio);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Payslip')
BEGIN
    CREATE TABLE dbo.Payslip
    (
        IdPayslip      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee        UNIQUEIDENTIFIER NOT NULL,
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

        CONSTRAINT UQ_Payslip_Employee_Periodo UNIQUE (IdEmployee, Anio, Mes)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PayslipDetail')
BEGIN
    -- Filas en vez de columnas fijas (sección 4 de la especificación) -- el
    -- motor de reglas decide qué filas generar según el régimen, sin tocar el
    -- esquema cuando cambian las reglas.
    CREATE TABLE dbo.PayslipDetail
    (
        IdPayslipDetail UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdPayslip        UNIQUEIDENTIFIER NOT NULL,
        TipoConcepto        NVARCHAR(20)     NOT NULL,  -- Ingreso/DescuentoTrabajador/AporteEmpleador
        CodigoConcepto      NVARCHAR(20)     NOT NULL,  -- BASICO/HEXT25/HEXT35/GRATIF/ASIGFAM/ONP/AFP/ESSALUD/SIS
        Descripcion         NVARCHAR(200)    NOT NULL,
        Monto               DECIMAL(18,2)    NOT NULL,
        EsRemunerativo      BIT              NOT NULL
    );

    CREATE INDEX IX_PayslipDetail_Boleta ON dbo.PayslipDetail (IdPayslip);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures -- LaborRegimeConfiguration
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_LaborRegimeConfiguration
    @IdLaborRegimeConfiguration UNIQUEIDENTIFIER,
    @IdAccount UNIQUEIDENTIFIER,
    @TipoRegimen NVARCHAR(20),
    @RUC NVARCHAR(20) = NULL,
    @RazonSocial NVARCHAR(200) = NULL,
    @FechaVigenciaDesde DATE,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.LaborRegimeConfiguration (IdLaborRegimeConfiguration, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn)
    VALUES (@IdLaborRegimeConfiguration, @IdAccount, @TipoRegimen, @RUC, @RazonSocial, @FechaVigenciaDesde, @CreatedBy, SYSUTCDATETIME());
END
GO

-- Trae la fila vigente a una fecha dada (por default, hoy) -- la más reciente
-- cuya FechaVigenciaDesde ya pasó. Así una boleta de un mes viejo puede pedir
-- el régimen "vigente a esa fecha" en vez de siempre el más nuevo.
CREATE OR ALTER PROCEDURE dbo.GET_LaborRegimeConfigurationVigente
    @IdAccount UNIQUEIDENTIFIER,
    @Fecha DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FechaEfectiva DATE = COALESCE(@Fecha, CAST(SYSUTCDATETIME() AS DATE));

    SELECT TOP 1 IdLaborRegimeConfiguration, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn
    FROM dbo.LaborRegimeConfiguration
    WHERE IdAccount = @IdAccount AND FechaVigenciaDesde <= @FechaEfectiva
    ORDER BY FechaVigenciaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_LaborRegimeConfigurationHistorial
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdLaborRegimeConfiguration, IdAccount, TipoRegimen, RUC, RazonSocial, FechaVigenciaDesde, CreatedBy, CreatedOn
    FROM dbo.LaborRegimeConfiguration
    WHERE IdAccount = @IdAccount
    ORDER BY FechaVigenciaDesde DESC;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Stored Procedures -- LegalParameters
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_LegalParameters
    @IdLegalParameters UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.LegalParameters
        (IdLegalParameters, IdAccount, Anio, ValorUIT, MontoAsignacionFamiliar, CostoSISMensual, PorcentajeEsSalud, PorcentajeONP, PorcentajeAFP, CreatedBy, CreatedOn)
    VALUES
        (@IdLegalParameters, @IdAccount, @Anio, @ValorUIT, @MontoAsignacionFamiliar, @CostoSISMensual, @PorcentajeEsSalud, @PorcentajeONP, @PorcentajeAFP, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_LegalParameters
    @IdLegalParameters UNIQUEIDENTIFIER,
    @ValorUIT DECIMAL(18,2),
    @MontoAsignacionFamiliar DECIMAL(18,2),
    @CostoSISMensual DECIMAL(18,2),
    @PorcentajeEsSalud DECIMAL(5,2),
    @PorcentajeONP DECIMAL(5,2),
    @PorcentajeAFP DECIMAL(5,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LegalParameters
    SET ValorUIT = @ValorUIT,
        MontoAsignacionFamiliar = @MontoAsignacionFamiliar,
        CostoSISMensual = @CostoSISMensual,
        PorcentajeEsSalud = @PorcentajeEsSalud,
        PorcentajeONP = @PorcentajeONP,
        PorcentajeAFP = @PorcentajeAFP
    WHERE IdLegalParameters = @IdLegalParameters;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_LegalParametersByAccountAndYear
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdLegalParameters, IdAccount, Anio, ValorUIT, MontoAsignacionFamiliar, CostoSISMensual,
           PorcentajeEsSalud, PorcentajeONP, PorcentajeAFP, CreatedBy, CreatedOn
    FROM dbo.LegalParameters
    WHERE IdAccount = @IdAccount AND Anio = @Anio;
END
GO

-- -----------------------------------------------------------------------------
-- 4) Stored Procedures -- Vacation
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Vacation
    @IdVacation UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
    @Anio INT,
    @FechaInicio DATE,
    @FechaFin DATE,
    @DiasSolicitados INT,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Vacation (IdVacation, IdEmployee, Anio, FechaInicio, FechaFin, DiasSolicitados, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn)
    VALUES (@IdVacation, @IdEmployee, @Anio, @FechaInicio, @FechaFin, @DiasSolicitados, 'Pendiente', NULL, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_VacationEstado
    @IdVacation UNIQUEIDENTIFIER,
    @Estado NVARCHAR(20),
    @IdAprobador UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Vacation
    SET Estado = @Estado,
        IdAprobador = @IdAprobador,
        FechaResolucion = SYSUTCDATETIME()
    WHERE IdVacation = @IdVacation;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VacationByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdVacation, IdEmployee, Anio, FechaInicio, FechaFin, DiasSolicitados, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn
    FROM dbo.Vacation
    WHERE IdEmployee = @IdEmployee
    ORDER BY FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_VacationPendientesByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.IdVacation, v.IdEmployee, v.Anio, v.FechaInicio, v.FechaFin, v.DiasSolicitados, v.Estado, v.IdAprobador, v.FechaResolucion, v.CreatedBy, v.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.Vacation v
    INNER JOIN dbo.Employee p ON p.IdEmployee = v.IdEmployee
    WHERE p.IdAccount = @IdAccount AND v.Estado = 'Pendiente'
    ORDER BY v.CreatedOn;
END
GO

-- Días GOZADOS de un Employee en un año -- suma de solicitudes Aprobadas o
-- Gozadas. DiasGanados/DiasPendientes se calculan en el servicio (Ganados
-- sale del régimen vigente, no de esta tabla).
CREATE OR ALTER PROCEDURE dbo.GET_VacationGozadasByEmployeeAnio
    @IdEmployee UNIQUEIDENTIFIER,
    @Anio INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(DiasSolicitados), 0) AS DiasGozados
    FROM dbo.Vacation
    WHERE IdEmployee = @IdEmployee AND Anio = @Anio AND Estado IN ('Aprobada', 'Gozada');
END
GO

-- -----------------------------------------------------------------------------
-- 5) Stored Procedures -- Payslip / PayslipDetail
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_Payslip
    @IdPayslip UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
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
    INSERT INTO dbo.Payslip (IdPayslip, IdEmployee, Anio, Mes, TipoRegimen, TotalIngresos, TotalDescuentos, NetoAPagar, FechaGeneracion, GeneradoPor)
    VALUES (@IdPayslip, @IdEmployee, @Anio, @Mes, @TipoRegimen, @TotalIngresos, @TotalDescuentos, @NetoAPagar, SYSUTCDATETIME(), @GeneradoPor);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPayslip, IdEmployee, Anio, Mes, TipoRegimen, TotalIngresos, TotalDescuentos, NetoAPagar, FechaGeneracion, GeneradoPor
    FROM dbo.Payslip
    WHERE IdEmployee = @IdEmployee
    ORDER BY Anio DESC, Mes DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletaById
    @IdPayslip UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdPayslip, b.IdEmployee, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.Payslip b
    INNER JOIN dbo.Employee p ON p.IdEmployee = b.IdEmployee
    WHERE b.IdPayslip = @IdPayslip;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByAccountAndPeriodo
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT,
    @Mes INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdPayslip, b.IdEmployee, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo
    FROM dbo.Payslip b
    INNER JOIN dbo.Employee p ON p.IdEmployee = b.IdEmployee
    WHERE p.IdAccount = @IdAccount AND b.Anio = @Anio AND b.Mes = @Mes
    ORDER BY p.Apellidos, p.Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_PayslipDetail
    @IdPayslipDetail UNIQUEIDENTIFIER,
    @IdPayslip UNIQUEIDENTIFIER,
    @TipoConcepto NVARCHAR(20),
    @CodigoConcepto NVARCHAR(20),
    @Descripcion NVARCHAR(200),
    @Monto DECIMAL(18,2),
    @EsRemunerativo BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.PayslipDetail (IdPayslipDetail, IdPayslip, TipoConcepto, CodigoConcepto, Descripcion, Monto, EsRemunerativo)
    VALUES (@IdPayslipDetail, @IdPayslip, @TipoConcepto, @CodigoConcepto, @Descripcion, @Monto, @EsRemunerativo);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_PayslipDetailByBoleta
    @IdPayslip UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdPayslipDetail, IdPayslip, TipoConcepto, CodigoConcepto, Descripcion, Monto, EsRemunerativo
    FROM dbo.PayslipDetail
    WHERE IdPayslip = @IdPayslip
    ORDER BY CASE TipoConcepto WHEN 'Ingreso' THEN 1 WHEN 'DescuentoTrabajador' THEN 2 ELSE 3 END, Descripcion;
END
GO

-- -----------------------------------------------------------------------------
-- 6) Items de menú -- hijos de "Employee" (Fase 1, IdMenu
--    C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71). Mismo criterio que Fase 1: no
--    idempotente, revisar Configuración > Items de Menú antes de repetir.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = 'F6D4C8B5-BE7A-4D5B-AC4F-6A9DB17D5FA4')
EXEC dbo.INS_MenuItem
    @IdMenu = 'F6D4C8B5-BE7A-4D5B-AC4F-6A9DB17D5FA4',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-labor-regime',
    @Title = 'Régimen laboral',
    @Icon = 'bi bi-gear',
    @Url = '/employees/labor-regime',
    @Target = NULL,
    @DisplayOrder = 3,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = '07E5D9C6-CF8B-4E6C-BD5A-7BAEC28E6A05')
EXEC dbo.INS_MenuItem
    @IdMenu = '07E5D9C6-CF8B-4E6C-BD5A-7BAEC28E6A05',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-vacations',
    @Title = 'Vacaciones',
    @Icon = 'bi bi-airplane',
    @Url = '/employees/vacations',
    @Target = NULL,
    @DisplayOrder = 4,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = '18F6EAD7-D09C-4F7D-CE6B-8CBFD39F7B16')
EXEC dbo.INS_MenuItem
    @IdMenu = '18F6EAD7-D09C-4F7D-CE6B-8CBFD39F7B16',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-payslips',
    @Title = 'Boletas de pago',
    @Icon = 'bi bi-receipt',
    @Url = '/employees/payslips',
    @Target = NULL,
    @DisplayOrder = 5,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
-- =============================================================================
-- Módulo Employee y Planillas -- Fase 3 (parcial): Permisos y licencias.
-- Requiere Fase 1 y Fase 2 ya aplicadas. Del resto de la Fase 3 (export
-- PLAME/T-Registro, pago del sueldo, marcado biométrico) queda deliberadamente
-- fuera: PLAME/T-Registro necesita la especificación oficial vigente de SUNAT
-- para no generar un archivo incorrecto; pago del sueldo necesita credenciales
-- reales de un gateway (Yape Negocios/Plin/banco); marcado biométrico es un
-- producto aparte (app móvil o hardware).
--
-- Una solicitud de permiso/licencia (con o sin goce de haber), aprobada por el
-- mismo motor de WorkflowAuditLog que ya usan Vacation/Presupuesto/Cuota --
-- mismo patrón exacto que Vacation (Fase 2). La diferencia práctica con
-- Vacation: un permiso SIN goce de haber SÍ afecta la boleta (se descuenta
-- proporcional a los días, ver GET_LeaveRequestSinGoceDiasByEmployeeMes,
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'LeaveRequest')
BEGIN
    CREATE TABLE dbo.LeaveRequest
    (
        IdLeaveRequest UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdEmployee        UNIQUEIDENTIFIER NOT NULL,
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

    CREATE INDEX IX_LeaveRequest_Employee ON dbo.LeaveRequest (IdEmployee, FechaInicio);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_LeaveRequest
    @IdLeaveRequest UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
    @FechaInicio DATE,
    @FechaFin DATE,
    @ConGoceDeHaber BIT,
    @Motivo NVARCHAR(300) = NULL,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.LeaveRequest (IdLeaveRequest, IdEmployee, FechaInicio, FechaFin, ConGoceDeHaber, Motivo, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn)
    VALUES (@IdLeaveRequest, @IdEmployee, @FechaInicio, @FechaFin, @ConGoceDeHaber, @Motivo, 'Pendiente', NULL, NULL, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_LeaveRequestEstado
    @IdLeaveRequest UNIQUEIDENTIFIER,
    @Estado NVARCHAR(20),
    @IdAprobador UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.LeaveRequest
    SET Estado = @Estado,
        IdAprobador = @IdAprobador,
        FechaResolucion = SYSUTCDATETIME()
    WHERE IdLeaveRequest = @IdLeaveRequest;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_LeaveRequestByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdLeaveRequest, IdEmployee, FechaInicio, FechaFin, ConGoceDeHaber, Motivo, Estado, IdAprobador, FechaResolucion, CreatedBy, CreatedOn
    FROM dbo.LeaveRequest
    WHERE IdEmployee = @IdEmployee
    ORDER BY FechaInicio DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_LeaveRequestPendientesByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pl.IdLeaveRequest, pl.IdEmployee, pl.FechaInicio, pl.FechaFin, pl.ConGoceDeHaber, pl.Motivo, pl.Estado, pl.IdAprobador, pl.FechaResolucion, pl.CreatedBy, pl.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.LeaveRequest pl
    INNER JOIN dbo.Employee p ON p.IdEmployee = pl.IdEmployee
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
CREATE OR ALTER PROCEDURE dbo.GET_LeaveRequestSinGoceDiasByEmployeeMes
    @IdEmployee UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ISNULL(SUM(
        DATEDIFF(DAY, GREATEST(FechaInicio, @FechaDesde), LEAST(FechaFin, @FechaHasta)) + 1
    ), 0) AS DiasSinGoce
    FROM dbo.LeaveRequest
    WHERE IdEmployee = @IdEmployee
      AND ConGoceDeHaber = 0
      AND Estado = 'Aprobado'
      AND FechaInicio <= @FechaHasta
      AND FechaFin >= @FechaDesde;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Item de menú -- hijo de "Employee" (Fase 1, IdMenu
--    C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71). No idempotente, mismo criterio
--    que Fases 1 y 2.
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = '29A7FBE8-E1AD-408E-DF7C-9DC0E4A08C27')
EXEC dbo.INS_MenuItem
    @IdMenu = '29A7FBE8-E1AD-408E-DF7C-9DC0E4A08C27',
    @IdParent = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71',
    @ItemKey = 'employees-leave-requests',
    @Title = 'Permisos y licencias',
    @Icon = 'bi bi-calendar-x',
    @Url = '/employees/leave-requests',
    @Target = NULL,
    @DisplayOrder = 6,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
-- =============================================================================
-- Fix: varios GET_* del módulo Employee y Planillas (Fases 1-3) no
-- devolvían todas las columnas que su clase de Models mapea.
--
-- EF Core, contra una entidad HasNoKey() leída con FromSqlRaw, exige que el
-- SELECT traiga TODAS las propiedades escalares mapeadas de esa clase -- si
-- falta una sola columna, no la deja en null: tira
-- InvalidOperationException ("The required column '...' was not present in
-- the results of a 'FromSql' operation") apenas se lee la primera fila.
--
-- El error se disparó primero en GET_BoletasByAccountAndPeriodo (faltaban
-- FechaIngreso/FechaCese/SistemaPensionario/IdAccount de Payslip), pero
-- al auditar el resto del módulo aparecieron cuatro casos más del mismo
-- patrón: un SP que sólo se usa desde un caso de uso (ej. "traer por
-- Building") no traía las columnas que sólo llena el OTRO caso de uso del
-- mismo Models.* (ej. "traer por Employee") -- como ambos comparten la
-- misma clase C#, los dos SELECT tienen que traer el superset completo de
-- columnas, aunque alguna quede NULL para ese caso.
--
-- Idempotente (sólo CREATE OR ALTER PROCEDURE).
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- Payslip (Fase 2) -- Classes/Planilla.cs mapea Nombres/Apellidos/DNI/
-- Cargo/FechaIngreso/FechaCese/SistemaPensionario/IdAccount como columnas
-- normales (no [NotMapped]) para poder mostrarlas sin una consulta aparte a
-- Employee -- los dos GET de listado no las traían todas, sólo GET_BoletaById.
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdPayslip, b.IdEmployee, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.Payslip b
    INNER JOIN dbo.Employee p ON p.IdEmployee = b.IdEmployee
    WHERE b.IdEmployee = @IdEmployee
    ORDER BY b.Anio DESC, b.Mes DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByAccountAndPeriodo
    @IdAccount UNIQUEIDENTIFIER,
    @Anio INT,
    @Mes INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdPayslip, b.IdEmployee, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.Payslip b
    INNER JOIN dbo.Employee p ON p.IdEmployee = b.IdEmployee
    WHERE p.IdAccount = @IdAccount AND b.Anio = @Anio AND b.Mes = @Mes
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- EmployeeBuildingAssignment (Fase 1) -- Classes/Employee.cs mapea BuildingName
-- (sólo lo llena "ByEmployee", join contra Building) Y Nombres/Apellidos/Cargo
-- (sólo lo llena "ByBuilding", join contra Employee) en la MISMA clase -- cada
-- GET ahora joinea ambas tablas para traer el superset completo; la mitad que
-- no aplica a ese caso de uso queda NULL (ej. BuildingName en el listado "por
-- edificio", donde el building ya lo conoce el caller).
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName, p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    LEFT JOIN dbo.Employee p ON p.IdEmployee = a.IdEmployee
    WHERE a.IdEmployee = @IdEmployee
    ORDER BY a.FechaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName, p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    LEFT JOIN dbo.Employee p ON p.IdEmployee = a.IdEmployee
    WHERE a.IdBuilding = @IdBuilding AND a.FechaHasta IS NULL
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- Vacation (Fase 2) -- GET_VacationByEmployee no traía Nombres/Apellidos
-- (sólo las llenaba GET_VacationPendientesByAccount).
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_VacationByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.IdVacation, v.IdEmployee, v.Anio, v.FechaInicio, v.FechaFin, v.DiasSolicitados, v.Estado, v.IdAprobador, v.FechaResolucion, v.CreatedBy, v.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.Vacation v
    LEFT JOIN dbo.Employee p ON p.IdEmployee = v.IdEmployee
    WHERE v.IdEmployee = @IdEmployee
    ORDER BY v.FechaInicio DESC;
END
GO

-- -----------------------------------------------------------------------------
-- LeaveRequest (Fase 3) -- mismo caso que Vacation.
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_LeaveRequestByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pl.IdLeaveRequest, pl.IdEmployee, pl.FechaInicio, pl.FechaFin, pl.ConGoceDeHaber, pl.Motivo, pl.Estado, pl.IdAprobador, pl.FechaResolucion, pl.CreatedBy, pl.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.LeaveRequest pl
    LEFT JOIN dbo.Employee p ON p.IdEmployee = pl.IdEmployee
    WHERE pl.IdEmployee = @IdEmployee
    ORDER BY pl.FechaInicio DESC;
END
GO

-- ===================== Actualizar MenuItems ya sembrados =====================
-- Los INS_MenuItem de arriba están guardados por IF NOT EXISTS (mismo IdMenu) --
-- una base que ya corrió 108-111 no los vuelve a insertar, así que hay que
-- actualizar el ItemKey/Url in place para que quede consistente con las
-- rutas nuevas de las páginas Razor renombradas.
UPDATE dbo.MenuItems SET ItemKey = 'employees', Url = '/employees' WHERE IdMenu = 'C3A1F5E2-8B4D-4A2E-9F1C-3D6B8E4A2C71';
UPDATE dbo.MenuItems SET ItemKey = 'employees-shifts', Url = '/employees/shifts' WHERE IdMenu = 'D4B2A6F3-9C5E-4B3F-8A2D-4E7C9F5B3D82';
UPDATE dbo.MenuItems SET ItemKey = 'employees-holidays', Url = '/employees/holidays' WHERE IdMenu = 'E5C3B7A4-AD6F-4C4A-9B3E-5F8DA06C4E93';
UPDATE dbo.MenuItems SET ItemKey = 'employees-labor-regime', Url = '/employees/labor-regime' WHERE IdMenu = 'F6D4C8B5-BE7A-4D5B-AC4F-6A9DB17D5FA4';
UPDATE dbo.MenuItems SET ItemKey = 'employees-vacations', Url = '/employees/vacations' WHERE IdMenu = '07E5D9C6-CF8B-4E6C-BD5A-7BAEC28E6A05';
UPDATE dbo.MenuItems SET ItemKey = 'employees-payslips', Url = '/employees/payslips' WHERE IdMenu = '18F6EAD7-D09C-4F7D-CE6B-8CBFD39F7B16';
UPDATE dbo.MenuItems SET ItemKey = 'employees-leave-requests', Url = '/employees/leave-requests' WHERE IdMenu = '29A7FBE8-E1AD-408E-DF7C-9DC0E4A08C27';
GO
