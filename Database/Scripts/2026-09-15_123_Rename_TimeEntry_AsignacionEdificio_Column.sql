-- =============================================================================
-- Cleanup flagged during the Budget/Cuotas rename pass: the original
-- 2026-09-15_116_Rename_Employee_Payroll_To_English.sql left
-- dbo.TimeEntry.IdAsignacionEdificio and the SPs
-- GET_AsignacionesEdificioByEmployee/GET_AsignacionesEdificioByBuilding
-- Spanish-named, even though the table they point at
-- (EmployeeBuildingAssignment) and the rest of that migration were already
-- English. Fix-forward script rather than editing the historical migration.
--
-- sp_rename (not DROP+CREATE) for the column -- unlike the other rename
-- phases, this table already has a live row in the test DB, and sp_rename
-- preserves it with no data loss.
-- =============================================================================

SET NOCOUNT ON;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.TimeEntry') AND name = 'IdAsignacionEdificio'
)
BEGIN
    EXEC sp_rename 'dbo.TimeEntry.IdAsignacionEdificio', 'IdEmployeeBuildingAssignment', 'COLUMN';
END
GO

DROP PROCEDURE IF EXISTS dbo.GET_AsignacionesEdificioByEmployee;
DROP PROCEDURE IF EXISTS dbo.GET_AsignacionesEdificioByBuilding;
GO

-- Docs/Pendientes-Negocio-Conciliacion.md pattern aside, esto es un bug pre-existente
-- (reproduce igual con el nombre en español, GET_AsignacionesEdificioByEmployee, antes
-- de este rename) encontrado al probar el rename: EmployeeBuildingAssignment es una
-- keyless entity -- FromSqlRaw exige que TODAS sus columnas mapeadas estén en el SELECT,
-- incluso las nullable (Nombres/Apellidos/Cargo/BuildingName, "sólo poblado por los GET
-- según el caso" per el comentario en Classes/Employee.cs) -- si faltan, EF tira
-- "required column ... was not present" en vez de dejarlas NULL. Se agregan como NULL
-- literal en la variante que no las llena, corrigiendo de paso.
CREATE OR ALTER PROCEDURE dbo.GET_EmployeeBuildingAssignmentsByEmployee
    @IdEmployee UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           b.Name AS BuildingName,
           CAST(NULL AS NVARCHAR(150)) AS Nombres,
           CAST(NULL AS NVARCHAR(150)) AS Apellidos,
           CAST(NULL AS NVARCHAR(100)) AS Cargo
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE a.IdEmployee = @IdEmployee
    ORDER BY a.FechaDesde DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_EmployeeBuildingAssignmentsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdEmployeeBuildingAssignment, a.IdEmployee, a.IdBuilding, a.FechaDesde, a.FechaHasta,
           a.PorcentajeDedicacion, a.CreatedBy, a.CreatedOn,
           CAST(NULL AS NVARCHAR(100)) AS BuildingName,
           p.Nombres, p.Apellidos, p.Cargo
    FROM dbo.EmployeeBuildingAssignment a
    LEFT JOIN dbo.Employee p ON p.IdEmployee = a.IdEmployee
    WHERE a.IdBuilding = @IdBuilding AND a.FechaHasta IS NULL
    ORDER BY p.Apellidos, p.Nombres;
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_TimeEntry
    @IdTimeEntry UNIQUEIDENTIFIER,
    @IdEmployee UNIQUEIDENTIFIER,
    @IdEmployeeBuildingAssignment UNIQUEIDENTIFIER,
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
        (IdTimeEntry, IdEmployee, IdEmployeeBuildingAssignment, Fecha, HorasOrdinarias, HorasExtra25, HorasExtra35, EsFeriado, Observaciones, IdUsuarioRegistro, CreatedOn)
    VALUES
        (@IdTimeEntry, @IdEmployee, @IdEmployeeBuildingAssignment, @Fecha, @HorasOrdinarias, @HorasExtra25, @HorasExtra35, @EsFeriado, @Observaciones, @IdUsuarioRegistro, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_TimeEntryByEmployee
    @IdEmployee UNIQUEIDENTIFIER,
    @FechaDesde DATE,
    @FechaHasta DATE
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.IdTimeEntry, r.IdEmployee, r.IdEmployeeBuildingAssignment, r.Fecha,
           r.HorasOrdinarias, r.HorasExtra25, r.HorasExtra35, r.EsFeriado, r.Observaciones,
           r.IdUsuarioRegistro, r.CreatedOn,
           b.Name AS BuildingName
    FROM dbo.TimeEntry r
    LEFT JOIN dbo.EmployeeBuildingAssignment a ON a.IdEmployeeBuildingAssignment = r.IdEmployeeBuildingAssignment
    LEFT JOIN dbo.Building b ON b.IdBuilding = a.IdBuilding
    WHERE r.IdEmployee = @IdEmployee
      AND r.Fecha BETWEEN @FechaDesde AND @FechaHasta
    ORDER BY r.Fecha DESC;
END
GO
