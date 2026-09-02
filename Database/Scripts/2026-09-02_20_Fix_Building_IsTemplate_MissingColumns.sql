-- =============================================================================
-- Fix sobre 2026-09-02_19_Building_IsTemplate.sql: agregar la columna IsTemplate
-- no alcanza -- EF exige que TODA columna mapeada en Models.Building esté
-- presente en el resultado de CUALQUIER proc que se lea con FromSqlRaw<Building>,
-- así que los 3 procs de lectura que ya existían (no tocados por el script
-- anterior) rompían con:
--   System.InvalidOperationException: The required column 'IsTemplate' was not
--   present in the results of a 'FromSql' operation.
-- en cuanto un usuario intentaba reconstruir su sesión (GET_AllBuildings).
--
-- Se agrega IsTemplate al SELECT de cada uno, respetando el resto tal cual
-- estaba (JOIN/WHERE/ORDER BY sin cambios) -- texto real confirmado por el
-- usuario (sp_helptext), no adivinado.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_AllBuildings]
@IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  b.IdBuilding,
            b.[Name],
            b.[Location],
            b.TotalArea,
            b.Number,
            b.[Type],
            b.Floors,
            b.Basements,
            b.Apartments,
            b.Parkings,
            b.Deposits,
            b.Others,
            b.IsActive,
            b.IsTemplate
    FROM    Building b
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_AllBuildingsPublic]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        IdBuilding,
        Name,
        Location,
        Number,
        Type,
        Floors,
        Basements,
        Apartments,
        Parkings,
        Deposits,
        Others,
        TotalArea,
        IsActive,
        IsTemplate
    FROM dbo.Building
    WHERE IsActive = 1
    ORDER BY Name;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_BuildingById]
@IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdBuilding,
            Name,
            Location,
            TotalArea,
            Number,
            Type,
            Floors,
            Basements,
            Apartments,
            Parkings,
            Deposits,
            Others,
            IsActive,
            IsTemplate
    FROM    Building
    WHERE   IdBuilding = @IdBuilding
END
GO
