-- =============================================================================
-- Logo/foto del Edificio (Docs/Pendientes-Negocio-Consolidado.md #30, punto e)
-- -- diseño acordado con el usuario (Opción A del mockup): el logo del
-- Edificio es el protagonista del encabezado del recibo (mismo lugar donde
-- ya iban las fotos de edificio en las plantillas Excel viejas); si el
-- Edificio no tiene logo propio, se usa el logo de la Account (empresa
-- administradora) como respaldo -- la lógica de ese fallback vive en
-- BuildingService.GetReceiptBrandingAsync, no acá.
--
-- Mismo mecanismo que el logo de Account (2026-09-22_138_Account_Logo.sql):
-- reusa IFileStorageService, columna con la ruta RELATIVA (nunca una URL
-- pública), UPD_Building_Logo separado de UPD_Building (subir un logo es
-- una acción aparte del formulario de datos generales del edificio).
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'LogoPath')
    ALTER TABLE dbo.Building ADD LogoPath NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'LogoContentType')
    ALTER TABLE dbo.Building ADD LogoContentType NVARCHAR(100) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Building_Logo
    @IdBuilding UNIQUEIDENTIFIER,
    @LogoPath NVARCHAR(500) = NULL,
    @LogoContentType NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Building
    SET LogoPath = @LogoPath,
        LogoContentType = @LogoContentType
    WHERE IdBuilding = @IdBuilding;
END
GO

-- Building es EF keyless -- toda columna mapeada en Models.Building debe
-- estar en el SELECT de CUALQUIER proc que lo hidrate (GetAllBuildingByOwnerAsync,
-- GetAllBuildingsPublicAsync, GetBuildingsByAccountAsync, GetBuildingByIdAsync
-- -- ver BDLayout.Get.cs; mismo criterio ya aplicado en
-- 2026-09-22_134_Building_Unit_Owner_Persist_RedesignFields.sql).
CREATE OR ALTER PROCEDURE dbo.GET_AllBuildings
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
            b.IsTemplate,
            b.IdAccount,
            b.ConstructionYear,
            b.Phone,
            b.Email,
            b.Elevators,
            b.AdminName,
            b.AdminPhone,
            b.EmergencyPhone,
            b.OfficeHours,
            b.HasPool,
            b.HasGym,
            b.HasBBQ,
            b.HasEventRoom,
            b.HasPetArea,
            b.HasGreenAreas,
            b.Has247Security,
            b.HasPorter,
            b.HasCameras,
            b.LogoPath,
            b.LogoContentType
    FROM    Building b
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AllBuildingsPublic
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
        IsTemplate,
        IdAccount,
        ConstructionYear,
        Phone,
        Email,
        Elevators,
        AdminName,
        AdminPhone,
        EmergencyPhone,
        OfficeHours,
        HasPool,
        HasGym,
        HasBBQ,
        HasEventRoom,
        HasPetArea,
        HasGreenAreas,
        Has247Security,
        HasPorter,
        HasCameras,
        LogoPath,
        LogoContentType
    FROM dbo.Building
    WHERE IsActive = 1
    ORDER BY Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BuildingsByAccount
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdBuilding, Number, Name, Location, Type, Floors, Basements,
           Apartments, Parkings, Deposits, Others, TotalArea, IsActive, IsTemplate, IdAccount,
           ConstructionYear, Phone, Email, Elevators, AdminName, AdminPhone, EmergencyPhone, OfficeHours,
           HasPool, HasGym, HasBBQ, HasEventRoom, HasPetArea, HasGreenAreas, Has247Security, HasPorter, HasCameras,
           LogoPath, LogoContentType
    FROM dbo.Building
    WHERE IdAccount = @IdAccount;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_BuildingById
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
            IsTemplate,
            IdAccount,
            ConstructionYear,
            Phone,
            Email,
            Elevators,
            AdminName,
            AdminPhone,
            EmergencyPhone,
            OfficeHours,
            HasPool,
            HasGym,
            HasBBQ,
            HasEventRoom,
            HasPetArea,
            HasGreenAreas,
            Has247Security,
            HasPorter,
            HasCameras,
            LogoPath,
            LogoContentType
    FROM    dbo.Building
    WHERE   IdBuilding = @IdBuilding;
END
GO
