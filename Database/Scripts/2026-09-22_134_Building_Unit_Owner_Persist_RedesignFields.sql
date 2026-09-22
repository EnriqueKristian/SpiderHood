-- =============================================================================
-- Termina de implementar los campos que se agregaron como [NotMapped] en
-- Building.cs/Unit.cs/Owner.cs (rediseño de Edificio/Unidades/Propietarios) --
-- ver Docs/Pendientes-Negocio-Consolidado.md #30. [NotMapped] = el formulario
-- los mostraba pero NUNCA se guardaban: se perdían al recargar la página. Acá
-- se agregan las columnas y se reescriben los stored procedures de alta/
-- edición/lectura para que persistan de verdad.
--
-- Decisiones tomadas con el usuario (2026-09-22), y 2 correcciones sobre lo
-- acordado al confirmar contra el código real ya escrito:
--
-- 1. Building: los 17 campos nuevos (ConstructionYear, Phone, Email,
--    Elevators, AdminName, AdminPhone, EmergencyPhone, OfficeHours + 9
--    amenities) se acordó en un principio mandar AdminName/AdminPhone/
--    EmergencyPhone/OfficeHours/Phone/Email a Account -- pero
--    BuildingPage.razor (Tab 1 "Información General" y Tab 4 "Contacto") YA
--    tiene el formulario completo tratándolos como datos DE CADA EDIFICIO
--    (ej. "Teléfono del Edificio" distinto de "Teléfono Administrador",
--    "Teléfono Emergencias 24 horas" por edificio) -- no como el dato de
--    facturación de Account (RazonSocial/RucDni/Telefono, que sigue
--    intacto). Se corrige: TODOS quedan en Building, ninguno se mueve a
--    Account.
-- 2. RealEstateUnit: se había asumido que PlateNumber/HasElectricCharging
--    (Estacionamiento) y HasWater/HasSecurity (Depósito) duplicaban a
--    VehicleType/IsCovered y HasVentilation/HasElectricity -- pero
--    ModalUnit.razor ya los usa como checkboxes SEPARADOS en la misma
--    sección ("Características" de Estacionamiento/Depósito trae 3-4
--    checkboxes distintos, uno por atributo). Se corrige: son campos
--    genuinamente nuevos y complementarios, se persisten todos. El único
--    duplicado real confirmado es ConstructedArea (nunca se usa en
--    ModalUnit.razor -- sólo BuiltArea, ya existente) -- ese sí se
--    descarta, no se agrega columna.
-- 3. Owner: Occupation/Employer -- confirmados en ModalOwner.razor, se
--    persisten. UnitNumber (Owner.cs) se descarta (dato redundante, ya
--    denormalizado en OwnerUnitView vía join real).
--
-- Todo NULL-able (fail-open, mismo criterio que scripts anteriores de esta
-- misma familia -- 2026-09-04_49_Unit_ExtraFields.sql,
-- 2026-09-05_52_Owner_ExtraFields.sql): registros existentes quedan sin
-- estos datos hasta que alguien los edite.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------
-- 1. Building -- 17 columnas nuevas
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'ConstructionYear')
    ALTER TABLE dbo.Building ADD ConstructionYear INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'Phone')
    ALTER TABLE dbo.Building ADD Phone NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'Email')
    ALTER TABLE dbo.Building ADD Email NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'Elevators')
    ALTER TABLE dbo.Building ADD Elevators INT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'AdminName')
    ALTER TABLE dbo.Building ADD AdminName NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'AdminPhone')
    ALTER TABLE dbo.Building ADD AdminPhone NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'EmergencyPhone')
    ALTER TABLE dbo.Building ADD EmergencyPhone NVARCHAR(30) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'OfficeHours')
    ALTER TABLE dbo.Building ADD OfficeHours NVARCHAR(200) NULL;
GO

-- BIT NOT NULL DEFAULT(0), no NULL -- Building.HasXxx son bool no-nullable en
-- C# (Blazor's InputCheckbox<T> en BuildingPage.razor sólo soporta bool, no
-- bool?), así que la columna no puede quedar NULL o EF revienta al hidratar.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasPool')
    ALTER TABLE dbo.Building ADD HasPool BIT NOT NULL CONSTRAINT DF_Building_HasPool DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasGym')
    ALTER TABLE dbo.Building ADD HasGym BIT NOT NULL CONSTRAINT DF_Building_HasGym DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasBBQ')
    ALTER TABLE dbo.Building ADD HasBBQ BIT NOT NULL CONSTRAINT DF_Building_HasBBQ DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasEventRoom')
    ALTER TABLE dbo.Building ADD HasEventRoom BIT NOT NULL CONSTRAINT DF_Building_HasEventRoom DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasPetArea')
    ALTER TABLE dbo.Building ADD HasPetArea BIT NOT NULL CONSTRAINT DF_Building_HasPetArea DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasGreenAreas')
    ALTER TABLE dbo.Building ADD HasGreenAreas BIT NOT NULL CONSTRAINT DF_Building_HasGreenAreas DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'Has247Security')
    ALTER TABLE dbo.Building ADD Has247Security BIT NOT NULL CONSTRAINT DF_Building_Has247Security DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasPorter')
    ALTER TABLE dbo.Building ADD HasPorter BIT NOT NULL CONSTRAINT DF_Building_HasPorter DEFAULT (0);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'HasCameras')
    ALTER TABLE dbo.Building ADD HasCameras BIT NOT NULL CONSTRAINT DF_Building_HasCameras DEFAULT (0);
GO

-- Corrige una corrida anterior de este mismo script (misma sesión) que había
-- agregado estas 9 columnas como BIT NULL -- si ya existen y todavía admiten
-- NULL, se backfillea a 0 y se las pasa a NOT NULL con su default, sin volver
-- a crearlas (evita perder la columna si ya tuviera datos reales).
DECLARE @col NVARCHAR(50), @sql NVARCHAR(MAX);
DECLARE amenity_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Building')
      AND name IN ('HasPool','HasGym','HasBBQ','HasEventRoom','HasPetArea','HasGreenAreas','Has247Security','HasPorter','HasCameras')
      AND is_nullable = 1;
OPEN amenity_cursor;
FETCH NEXT FROM amenity_cursor INTO @col;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'UPDATE dbo.Building SET ' + QUOTENAME(@col) + N' = 0 WHERE ' + QUOTENAME(@col) + N' IS NULL;';
    EXEC sp_executesql @sql;
    SET @sql = N'ALTER TABLE dbo.Building ALTER COLUMN ' + QUOTENAME(@col) + N' BIT NOT NULL;';
    EXEC sp_executesql @sql;
    FETCH NEXT FROM amenity_cursor INTO @col;
END
CLOSE amenity_cursor;
DEALLOCATE amenity_cursor;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Building
    @IdBuilding UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Location NVARCHAR(300),
    @Type INT,
    @Floors INT,
    @Basements INT,
    @Apartments INT,
    @Parkings INT,
    @Deposits INT,
    @Others INT,
    @TotalArea DECIMAL(18, 2),
    @IsActive BIT,
    @IsTemplate BIT,
    @IdAccount UNIQUEIDENTIFIER = NULL,
    @ConstructionYear INT = NULL,
    @Phone NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL,
    @Elevators INT = NULL,
    @AdminName NVARCHAR(150) = NULL,
    @AdminPhone NVARCHAR(30) = NULL,
    @EmergencyPhone NVARCHAR(30) = NULL,
    @OfficeHours NVARCHAR(200) = NULL,
    @HasPool BIT = NULL,
    @HasGym BIT = NULL,
    @HasBBQ BIT = NULL,
    @HasEventRoom BIT = NULL,
    @HasPetArea BIT = NULL,
    @HasGreenAreas BIT = NULL,
    @Has247Security BIT = NULL,
    @HasPorter BIT = NULL,
    @HasCameras BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Building
        (IdBuilding, Name, Location, Type, Floors, Basements, Apartments, Parkings, Deposits, Others, TotalArea, IsActive, IsTemplate, IdAccount,
         ConstructionYear, Phone, Email, Elevators, AdminName, AdminPhone, EmergencyPhone, OfficeHours,
         HasPool, HasGym, HasBBQ, HasEventRoom, HasPetArea, HasGreenAreas, Has247Security, HasPorter, HasCameras)
    VALUES
        (@IdBuilding, @Name, @Location, @Type, @Floors, @Basements, @Apartments, @Parkings, @Deposits, @Others, @TotalArea, @IsActive, @IsTemplate, @IdAccount,
         @ConstructionYear, @Phone, @Email, @Elevators, @AdminName, @AdminPhone, @EmergencyPhone, @OfficeHours,
         @HasPool, @HasGym, @HasBBQ, @HasEventRoom, @HasPetArea, @HasGreenAreas, @Has247Security, @HasPorter, @HasCameras);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Building
    @IdBuilding UNIQUEIDENTIFIER,
    @Name NVARCHAR(200),
    @Location NVARCHAR(300),
    @Type INT,
    @Floors INT,
    @Basements INT,
    @Apartments INT,
    @Parkings INT,
    @Deposits INT,
    @Others INT,
    @TotalArea DECIMAL(18, 2),
    @IsActive BIT,
    @IsTemplate BIT,
    @IdAccount UNIQUEIDENTIFIER = NULL,
    @ConstructionYear INT = NULL,
    @Phone NVARCHAR(30) = NULL,
    @Email NVARCHAR(150) = NULL,
    @Elevators INT = NULL,
    @AdminName NVARCHAR(150) = NULL,
    @AdminPhone NVARCHAR(30) = NULL,
    @EmergencyPhone NVARCHAR(30) = NULL,
    @OfficeHours NVARCHAR(200) = NULL,
    @HasPool BIT = NULL,
    @HasGym BIT = NULL,
    @HasBBQ BIT = NULL,
    @HasEventRoom BIT = NULL,
    @HasPetArea BIT = NULL,
    @HasGreenAreas BIT = NULL,
    @Has247Security BIT = NULL,
    @HasPorter BIT = NULL,
    @HasCameras BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Building
    SET Name = @Name,
        Location = @Location,
        Type = @Type,
        Floors = @Floors,
        Basements = @Basements,
        Apartments = @Apartments,
        Parkings = @Parkings,
        Deposits = @Deposits,
        Others = @Others,
        TotalArea = @TotalArea,
        IsActive = @IsActive,
        IsTemplate = @IsTemplate,
        IdAccount = COALESCE(@IdAccount, IdAccount),
        ConstructionYear = @ConstructionYear,
        Phone = @Phone,
        Email = @Email,
        Elevators = @Elevators,
        AdminName = @AdminName,
        AdminPhone = @AdminPhone,
        EmergencyPhone = @EmergencyPhone,
        OfficeHours = @OfficeHours,
        HasPool = @HasPool,
        HasGym = @HasGym,
        HasBBQ = @HasBBQ,
        HasEventRoom = @HasEventRoom,
        HasPetArea = @HasPetArea,
        HasGreenAreas = @HasGreenAreas,
        Has247Security = @Has247Security,
        HasPorter = @HasPorter,
        HasCameras = @HasCameras
    WHERE IdBuilding = @IdBuilding;
END
GO

-- Building es una entidad EF "keyless" (HasNoKey) -- EF exige que TODA
-- columna mapeada en Models.Building esté en el SELECT de CUALQUIER proc que
-- se use para hidratarlo (GetAllBuildingByOwnerAsync, GetAllBuildingsPublicAsync,
-- GetBuildingsByAccountAsync, GetBuildingByIdAsync -- ver BDLayout.Get.cs).
-- GET_TemplateBuilding no se toca: usa "SELECT TOP 1 *", ya trae todo.
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
            b.HasCameras
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
        HasCameras
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
           HasPool, HasGym, HasBBQ, HasEventRoom, HasPetArea, HasGreenAreas, Has247Security, HasPorter, HasCameras
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
            HasCameras
    FROM    dbo.Building
    WHERE   IdBuilding = @IdBuilding;
END
GO

-- ---------------------------------------------------------------------------
-- 2. RealEstateUnit -- 15 columnas nuevas (ConstructedArea NO se agrega --
--    confirmado duplicado de BuiltArea, nunca usado en ModalUnit.razor).
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Status')
    ALTER TABLE dbo.RealEstateUnit ADD Status NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Orientation')
    ALTER TABLE dbo.RealEstateUnit ADD Orientation NVARCHAR(20) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasBalcony')
    ALTER TABLE dbo.RealEstateUnit ADD HasBalcony BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasParking')
    ALTER TABLE dbo.RealEstateUnit ADD HasParking BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasStorage')
    ALTER TABLE dbo.RealEstateUnit ADD HasStorage BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasAirConditioning')
    ALTER TABLE dbo.RealEstateUnit ADD HasAirConditioning BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'IsFurnished')
    ALTER TABLE dbo.RealEstateUnit ADD IsFurnished BIT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'PlateNumber')
    ALTER TABLE dbo.RealEstateUnit ADD PlateNumber NVARCHAR(20) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasElectricCharging')
    ALTER TABLE dbo.RealEstateUnit ADD HasElectricCharging BIT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasWater')
    ALTER TABLE dbo.RealEstateUnit ADD HasWater BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasSecurity')
    ALTER TABLE dbo.RealEstateUnit ADD HasSecurity BIT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'EstimatedValue')
    ALTER TABLE dbo.RealEstateUnit ADD EstimatedValue DECIMAL(18, 2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'LastRenovationDate')
    ALTER TABLE dbo.RealEstateUnit ADD LastRenovationDate DATE NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Restrictions')
    ALTER TABLE dbo.RealEstateUnit ADD Restrictions NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasElevatorAccess')
    ALTER TABLE dbo.RealEstateUnit ADD HasElevatorAccess BIT NULL;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Unit
    @IdUnit UNIQUEIDENTIFIER,
    @UnitNumber NVARCHAR(50),
    @Area DECIMAL(18, 2),
    @Number INT,
    @TypeUnit INT,
    @IsAvailable BIT,
    @IdBuilding UNIQUEIDENTIFIER,
    @Floor INT = NULL,
    @Tower NVARCHAR(50) = NULL,
    @LocationCode NVARCHAR(50) = NULL,
    @Bedrooms INT = NULL,
    @Bathrooms INT = NULL,
    @BuiltArea DECIMAL(18, 2) = NULL,
    @IsCovered BIT = NULL,
    @IsForDisabled BIT = NULL,
    @VehicleType NVARCHAR(30) = NULL,
    @Height DECIMAL(18, 2) = NULL,
    @HasVentilation BIT = NULL,
    @HasElectricity BIT = NULL,
    @Notes NVARCHAR(500) = NULL,
    @Status NVARCHAR(20) = NULL,
    @Orientation NVARCHAR(20) = NULL,
    @HasBalcony BIT = NULL,
    @HasParking BIT = NULL,
    @HasStorage BIT = NULL,
    @HasAirConditioning BIT = NULL,
    @IsFurnished BIT = NULL,
    @PlateNumber NVARCHAR(20) = NULL,
    @HasElectricCharging BIT = NULL,
    @HasWater BIT = NULL,
    @HasSecurity BIT = NULL,
    @EstimatedValue DECIMAL(18, 2) = NULL,
    @LastRenovationDate DATE = NULL,
    @Restrictions NVARCHAR(500) = NULL,
    @HasElevatorAccess BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.RealEstateUnit
        (IdUnit, UnitNumber, Area, Number, TypeUnit, IsAvailable, IdBuilding,
         Floor, Tower, LocationCode, Bedrooms, Bathrooms, BuiltArea,
         IsCovered, IsForDisabled, VehicleType, Height, HasVentilation, HasElectricity, Notes,
         Status, Orientation, HasBalcony, HasParking, HasStorage, HasAirConditioning, IsFurnished,
         PlateNumber, HasElectricCharging, HasWater, HasSecurity,
         EstimatedValue, LastRenovationDate, Restrictions, HasElevatorAccess)
    VALUES
        (@IdUnit, @UnitNumber, @Area, @Number, @TypeUnit, @IsAvailable, @IdBuilding,
         @Floor, @Tower, @LocationCode, @Bedrooms, @Bathrooms, @BuiltArea,
         @IsCovered, @IsForDisabled, @VehicleType, @Height, @HasVentilation, @HasElectricity, @Notes,
         @Status, @Orientation, @HasBalcony, @HasParking, @HasStorage, @HasAirConditioning, @IsFurnished,
         @PlateNumber, @HasElectricCharging, @HasWater, @HasSecurity,
         @EstimatedValue, @LastRenovationDate, @Restrictions, @HasElevatorAccess);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Unit
    @IdUnit UNIQUEIDENTIFIER,
    @UnitNumber NVARCHAR(50),
    @Area DECIMAL(18, 2),
    @TypeUnit INT = NULL,
    @IsAvailable BIT = NULL,
    @Floor INT = NULL,
    @Tower NVARCHAR(50) = NULL,
    @LocationCode NVARCHAR(50) = NULL,
    @Bedrooms INT = NULL,
    @Bathrooms INT = NULL,
    @BuiltArea DECIMAL(18, 2) = NULL,
    @IsCovered BIT = NULL,
    @IsForDisabled BIT = NULL,
    @VehicleType NVARCHAR(30) = NULL,
    @Height DECIMAL(18, 2) = NULL,
    @HasVentilation BIT = NULL,
    @HasElectricity BIT = NULL,
    @Notes NVARCHAR(500) = NULL,
    @Status NVARCHAR(20) = NULL,
    @Orientation NVARCHAR(20) = NULL,
    @HasBalcony BIT = NULL,
    @HasParking BIT = NULL,
    @HasStorage BIT = NULL,
    @HasAirConditioning BIT = NULL,
    @IsFurnished BIT = NULL,
    @PlateNumber NVARCHAR(20) = NULL,
    @HasElectricCharging BIT = NULL,
    @HasWater BIT = NULL,
    @HasSecurity BIT = NULL,
    @EstimatedValue DECIMAL(18, 2) = NULL,
    @LastRenovationDate DATE = NULL,
    @Restrictions NVARCHAR(500) = NULL,
    @HasElevatorAccess BIT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.RealEstateUnit
    SET UnitNumber = @UnitNumber,
        Area = @Area,
        TypeUnit = COALESCE(@TypeUnit, TypeUnit),
        IsAvailable = COALESCE(@IsAvailable, IsAvailable),
        Floor = @Floor,
        Tower = @Tower,
        LocationCode = @LocationCode,
        Bedrooms = @Bedrooms,
        Bathrooms = @Bathrooms,
        BuiltArea = @BuiltArea,
        IsCovered = @IsCovered,
        IsForDisabled = @IsForDisabled,
        VehicleType = @VehicleType,
        Height = @Height,
        HasVentilation = @HasVentilation,
        HasElectricity = @HasElectricity,
        Notes = @Notes,
        Status = @Status,
        Orientation = @Orientation,
        HasBalcony = @HasBalcony,
        HasParking = @HasParking,
        HasStorage = @HasStorage,
        HasAirConditioning = @HasAirConditioning,
        IsFurnished = @IsFurnished,
        PlateNumber = @PlateNumber,
        HasElectricCharging = @HasElectricCharging,
        HasWater = @HasWater,
        HasSecurity = @HasSecurity,
        EstimatedValue = @EstimatedValue,
        LastRenovationDate = @LastRenovationDate,
        Restrictions = @Restrictions,
        HasElevatorAccess = @HasElevatorAccess
    WHERE IdUnit = @IdUnit;
END
GO

-- RealEstateUnit NO es una entidad EF keyless hidratada por FromSqlRaw --
-- BDLayout.GetUnitsByBuildingAsync arma cada RealEstateUnit a mano desde un
-- DataTable (ADO.NET puro) y mergea los campos propios en memoria vía
-- MergeUnitExtraFields/GET_UnitExtraFieldsByBuilding -- así que sólo hace
-- falta ampliar ese proc + el merge en C#, no tocar GET_UnitsByBuilding.
CREATE OR ALTER PROCEDURE dbo.GET_UnitExtraFieldsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        IdUnit, Floor, Tower, LocationCode, Bedrooms, Bathrooms, BuiltArea,
        IsCovered, IsForDisabled, VehicleType, Height, HasVentilation, HasElectricity, Notes,
        Status, Orientation, HasBalcony, HasParking, HasStorage, HasAirConditioning, IsFurnished,
        PlateNumber, HasElectricCharging, HasWater, HasSecurity,
        EstimatedValue, LastRenovationDate, Restrictions, HasElevatorAccess
    FROM dbo.RealEstateUnit
    WHERE IdBuilding = @IdBuilding;
END
GO

-- ---------------------------------------------------------------------------
-- 3. ApartmentOwner -- 2 columnas nuevas (Occupation, Employer -- confirmadas
--    en uso en ModalOwner.razor). UnitNumber (Owner.cs) se descarta, no
--    corresponde columna: ya viene denormalizado en OwnerUnitView.
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'Occupation')
    ALTER TABLE dbo.ApartmentOwner ADD Occupation NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'Employer')
    ALTER TABLE dbo.ApartmentOwner ADD Employer NVARCHAR(150) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Owner
    @IdOwner              UNIQUEIDENTIFIER,
    @IdNumber             NVARCHAR(20),
    @Names                NVARCHAR(100),
    @Surname              NVARCHAR(100) = NULL,
    @Address              NVARCHAR(80),
    @PhoneNumber          NVARCHAR(20),
    @IdBuilding           UNIQUEIDENTIFIER,
    @IdTypeIdNumber       INT = NULL,
    @Email                NVARCHAR(100) = NULL,
    @IsActive             BIT = 1,
    @MobilePhone          NVARCHAR(20) = NULL,
    @WorkPhone            NVARCHAR(20) = NULL,
    @RelationshipType     NVARCHAR(30) = NULL,
    @BusinessName         NVARCHAR(200) = NULL,
    @LegalRepresentative  NVARCHAR(200) = NULL,
    @RucType              NVARCHAR(10) = NULL,
    @Nationality          NVARCHAR(50) = NULL,
    @CivilStatus          NVARCHAR(20) = NULL,
    @BirthDate            DATE = NULL,
    @Occupation           NVARCHAR(150) = NULL,
    @Employer             NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ApartmentOwner
        (IdOwner, IdentityDocument, FirstName, LastName, Address, PhoneNumber, IdBuilding, IdTypeIdNumber,
         Email, IsActive, MobilePhone, WorkPhone, RelationshipType,
         BusinessName, LegalRepresentative, RucType, Nationality, CivilStatus, BirthDate,
         Occupation, Employer)
    VALUES
        (@IdOwner, @IdNumber, @Names, @Surname, @Address, @PhoneNumber, @IdBuilding, @IdTypeIdNumber,
         @Email, @IsActive, @MobilePhone, @WorkPhone, @RelationshipType,
         @BusinessName, @LegalRepresentative, @RucType, @Nationality, @CivilStatus, @BirthDate,
         @Occupation, @Employer);
END;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Owner
    @IdOwner              UNIQUEIDENTIFIER,
    @IdNumber             NVARCHAR(20),
    @Names                NVARCHAR(100),
    @Surname              NVARCHAR(100) = NULL,
    @Address              NVARCHAR(80),
    @PhoneNumber          NVARCHAR(20),
    @IdTypeIdNumber       INT = NULL,
    @Email                NVARCHAR(100) = NULL,
    @IsActive             BIT = NULL,
    @MobilePhone          NVARCHAR(20) = NULL,
    @WorkPhone            NVARCHAR(20) = NULL,
    @RelationshipType     NVARCHAR(30) = NULL,
    @BusinessName         NVARCHAR(200) = NULL,
    @LegalRepresentative  NVARCHAR(200) = NULL,
    @RucType              NVARCHAR(10) = NULL,
    @Nationality          NVARCHAR(50) = NULL,
    @CivilStatus          NVARCHAR(20) = NULL,
    @BirthDate            DATE = NULL,
    @Occupation           NVARCHAR(150) = NULL,
    @Employer             NVARCHAR(150) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ApartmentOwner
    SET IdentityDocument = @IdNumber,
        FirstName = @Names,
        LastName = @Surname,
        Address = @Address,
        PhoneNumber = @PhoneNumber,
        IdTypeIdNumber = @IdTypeIdNumber,
        Email = @Email,
        IsActive = COALESCE(@IsActive, IsActive),
        MobilePhone = @MobilePhone,
        WorkPhone = @WorkPhone,
        RelationshipType = @RelationshipType,
        BusinessName = @BusinessName,
        LegalRepresentative = @LegalRepresentative,
        RucType = @RucType,
        Nationality = @Nationality,
        CivilStatus = @CivilStatus,
        BirthDate = @BirthDate,
        Occupation = @Occupation,
        Employer = @Employer
    WHERE IdOwner = @IdOwner;
END;
GO

-- OwnerUnitView.IsRealEstateCompanyOwner: la columna ya existía en
-- ApartmentOwner y en VW_OwnerUnit desde 2026-09-15_106_*.sql, pero
-- GET_OwnerByBuilding (que hidrata OwnerUnitView, entidad EF keyless) todavía
-- no la traía en su SELECT explícito -- "Fase 2" mencionada en ese script,
-- pendiente hasta ahora. Sin este ALTER, agregar la propiedad a
-- OwnerUnitView.cs rompe TODOS los llamados a GetOwnersByBuildingAsync (EF
-- exige que toda columna mapeada esté en el SELECT del proc que la hidrata).
CREATE OR ALTER PROCEDURE [dbo].[GET_OwnerByBuilding]
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdGroupUnit, TotalArea, GroupNumber, IdUnit, UnitNumber, Area, TypeUnit, Number, IsAvailable,
            IdGroupOwnerRol, [Role], IdOwner, IdentityDocument, [Address], PhoneNumber, FirstName, LastName,
            Email, IsActive, IdBuilding, IdTypeIdNumber, IsRealEstateCompanyOwner
    FROM    VW_OwnerUnit
    WHERE   IdBuilding = @IdBuilding
    ORDER BY TypeUnit, [role], GroupNumber
END
GO
