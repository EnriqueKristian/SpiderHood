-- =============================================================================
-- Fix: varios GET_* del módulo Personal y Planillas (Fases 1-3) no
-- devolvían todas las columnas que su clase de Models mapea.
--
-- EF Core, contra una entidad HasNoKey() leída con FromSqlRaw, exige que el
-- SELECT traiga TODAS las propiedades escalares mapeadas de esa clase -- si
-- falta una sola columna, no la deja en null: tira
-- InvalidOperationException ("The required column '...' was not present in
-- the results of a 'FromSql' operation") apenas se lee la primera fila.
--
-- El error se disparó primero en GET_BoletasByAccountAndPeriodo (faltaban
-- FechaIngreso/FechaCese/SistemaPensionario/IdAccount de BoletaPago), pero
-- al auditar el resto del módulo aparecieron cuatro casos más del mismo
-- patrón: un SP que sólo se usa desde un caso de uso (ej. "traer por
-- Building") no traía las columnas que sólo llena el OTRO caso de uso del
-- mismo Models.* (ej. "traer por Personal") -- como ambos comparten la
-- misma clase C#, los dos SELECT tienen que traer el superset completo de
-- columnas, aunque alguna quede NULL para ese caso.
--
-- Idempotente (sólo CREATE OR ALTER PROCEDURE).
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- BoletaPago (Fase 2) -- Classes/Planilla.cs mapea Nombres/Apellidos/DNI/
-- Cargo/FechaIngreso/FechaCese/SistemaPensionario/IdAccount como columnas
-- normales (no [NotMapped]) para poder mostrarlas sin una consulta aparte a
-- Personal -- los dos GET de listado no las traían todas, sólo GET_BoletaById.
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_BoletasByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT b.IdBoletaPago, b.IdPersonal, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.BoletaPago b
    INNER JOIN dbo.Personal p ON p.IdPersonal = b.IdPersonal
    WHERE b.IdPersonal = @IdPersonal
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
    SELECT b.IdBoletaPago, b.IdPersonal, b.Anio, b.Mes, b.TipoRegimen, b.TotalIngresos, b.TotalDescuentos, b.NetoAPagar, b.FechaGeneracion, b.GeneradoPor,
           p.Nombres, p.Apellidos, p.DNI, p.Cargo, p.FechaIngreso, p.FechaCese, p.SistemaPensionario, p.IdAccount
    FROM dbo.BoletaPago b
    INNER JOIN dbo.Personal p ON p.IdPersonal = b.IdPersonal
    WHERE p.IdAccount = @IdAccount AND b.Anio = @Anio AND b.Mes = @Mes
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- AsignacionPersonalEdificio (Fase 1) -- Classes/Personal.cs mapea BuildingName
-- (sólo lo llena "ByPersonal", join contra Building) Y Nombres/Apellidos/Cargo
-- (sólo lo llena "ByBuilding", join contra Personal) en la MISMA clase -- cada
-- GET ahora joinea ambas tablas para traer el superset completo; la mitad que
-- no aplica a ese caso de uso queda NULL (ej. BuildingName en el listado "por
-- edificio", donde el building ya lo conoce el caller).
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_AsignacionesEdificioByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdAsignacionPersonalEdificio, a.IdPersonal, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName, p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.AsignacionPersonalEdificio a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    LEFT JOIN dbo.Personal p ON p.IdPersonal = a.IdPersonal
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
           b.Name AS BuildingName, p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.AsignacionPersonalEdificio a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    LEFT JOIN dbo.Personal p ON p.IdPersonal = a.IdPersonal
    WHERE a.IdBuilding = @IdBuilding AND a.FechaHasta IS NULL
    ORDER BY p.Apellidos, p.Nombres;
END
GO

-- -----------------------------------------------------------------------------
-- Vacaciones (Fase 2) -- GET_VacacionesByPersonal no traía Nombres/Apellidos
-- (sólo las llenaba GET_VacacionesPendientesByAccount).
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_VacacionesByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT v.IdVacaciones, v.IdPersonal, v.Anio, v.FechaInicio, v.FechaFin, v.DiasSolicitados, v.Estado, v.IdAprobador, v.FechaResolucion, v.CreatedBy, v.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.Vacaciones v
    LEFT JOIN dbo.Personal p ON p.IdPersonal = v.IdPersonal
    WHERE v.IdPersonal = @IdPersonal
    ORDER BY v.FechaInicio DESC;
END
GO

-- -----------------------------------------------------------------------------
-- PermisoLicencia (Fase 3) -- mismo caso que Vacaciones.
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.GET_PermisoLicenciaByPersonal
    @IdPersonal UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pl.IdPermisoLicencia, pl.IdPersonal, pl.FechaInicio, pl.FechaFin, pl.ConGoceDeHaber, pl.Motivo, pl.Estado, pl.IdAprobador, pl.FechaResolucion, pl.CreatedBy, pl.CreatedOn,
           p.Nombres, p.Apellidos
    FROM dbo.PermisoLicencia pl
    LEFT JOIN dbo.Personal p ON p.IdPersonal = pl.IdPersonal
    WHERE pl.IdPersonal = @IdPersonal
    ORDER BY pl.FechaInicio DESC;
END
GO
