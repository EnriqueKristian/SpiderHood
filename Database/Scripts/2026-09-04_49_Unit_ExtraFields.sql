-- =============================================================================
-- RealEstateUnit (tabla física dbo.RealEstateUnit; los stored procedures usan
-- "Unit" a secas -- INS_Unit/UPD_Unit/GET_UnitsByBuilding -- por convención de
-- nombres, no porque la tabla se llame distinto) traía muy pocos campos
-- propios -- UnitNumber, Area, Number, TypeUnit, IsAvailable eran las únicas
-- columnas reales que persistían INS_Unit/UPD_Unit;
-- el resto de propiedades del modelo C# (GroupName, Names, Surname, TypeOwner,
-- IdOwner, Building, AreaTotal, TypeGroupUnit, IdGroupOwner) vienen de un JOIN
-- contra Owner/GroupOwner en GET_UnitsByBuilding -- son de una vista, no de la
-- tabla RealEstateUnit. No se toca GET_UnitsByBuilding en
-- este script (su JOIN no está confirmado por sp_helptext, y romperlo
-- afectaría a Owners.razor/UnitGroups.razor) -- los campos nuevos se traen con
-- un proc aparte (GET_UnitExtraFieldsByBuilding) y se mergean en memoria en
-- BDLayout.GetUnitsByBuildingAsync.
--
-- Campos nuevos, todos NULL-able (fail-open: unidades ya cargadas quedan sin
-- estos datos hasta que alguien las edite):
--   - Ubicación física: Floor, Tower, LocationCode (texto libre referencial --
--     ej. "S1", "SS", "Torre A-3" -- para sótanos/niveles/torres sin necesidad
--     de modelar la estructura del edificio de antemano).
--   - Sólo DPTO/OFICINA (TypeUnit 1/4): Bedrooms, Bathrooms, BuiltArea.
--   - Sólo ESTACIONAMIENTO (TypeUnit 2): IsCovered, IsForDisabled, VehicleType.
--   - Sólo DEPOSITO (TypeUnit 3): Height, HasVentilation, HasElectricity.
--   - Generales: Notes.
-- Deliberadamente NO se agrega "AdminFee" (cuota fija por unidad) ni
-- "UnitStatus" (Disponible/Ocupado/Mantenimiento): el primero no tiene ninguna
-- lógica de facturación real detrás todavía (el motor de Budget reparte por
-- Area/Number vía BudgetDetail.Type, no por un monto en la unidad -- agregarlo
-- ahora sería un campo que se guarda pero no afecta nada, igual al 2FA que ya
-- se marcó como no-funcional en Security.razor); el segundo duplicaría a
-- IsAvailable, que ya cumple ese rol.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Floor')
    ALTER TABLE dbo.RealEstateUnit ADD Floor INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Tower')
    ALTER TABLE dbo.RealEstateUnit ADD Tower NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'LocationCode')
    ALTER TABLE dbo.RealEstateUnit ADD LocationCode NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Bedrooms')
    ALTER TABLE dbo.RealEstateUnit ADD Bedrooms INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Bathrooms')
    ALTER TABLE dbo.RealEstateUnit ADD Bathrooms INT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'BuiltArea')
    ALTER TABLE dbo.RealEstateUnit ADD BuiltArea DECIMAL(18, 2) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'IsCovered')
    ALTER TABLE dbo.RealEstateUnit ADD IsCovered BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'IsForDisabled')
    ALTER TABLE dbo.RealEstateUnit ADD IsForDisabled BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'VehicleType')
    ALTER TABLE dbo.RealEstateUnit ADD VehicleType NVARCHAR(30) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Height')
    ALTER TABLE dbo.RealEstateUnit ADD Height DECIMAL(18, 2) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasVentilation')
    ALTER TABLE dbo.RealEstateUnit ADD HasVentilation BIT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'HasElectricity')
    ALTER TABLE dbo.RealEstateUnit ADD HasElectricity BIT NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'Notes')
    ALTER TABLE dbo.RealEstateUnit ADD Notes NVARCHAR(500) NULL;
GO

-- Auditoría (mismo patrón que Building/Owner/BudgetHeader/Expense, ver
-- 2026-09-01_01_Audit_HeaderColumns.sql) -- no se agregan al modelo C# ni a
-- ningún formulario: son sólo trazabilidad en la BD, estampada aparte vía
-- BDLayout.StampAuditAsync justo después del INSERT/UPDATE de negocio.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'CreatedBy')
    ALTER TABLE dbo.RealEstateUnit ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'CreatedOn')
    ALTER TABLE dbo.RealEstateUnit ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'ModifiedBy')
    ALTER TABLE dbo.RealEstateUnit ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RealEstateUnit') AND name = 'ModifiedOn')
    ALTER TABLE dbo.RealEstateUnit ADD ModifiedOn DATETIME2 NULL;
GO

-- ---------------------------------------------------------------------------
-- INS_Unit / UPD_Unit: se reescriben completos (igual criterio que
-- INS_Building/UPD_Building en 2026-09-04_48) -- son procs simples de
-- INSERT/UPDATE directo sobre una sola tabla, sin JOINs ni lógica de negocio
-- escondida, así que reconstruirlos desde las columnas conocidas es seguro.
-- ---------------------------------------------------------------------------

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
    @Notes NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.RealEstateUnit
        (IdUnit, UnitNumber, Area, Number, TypeUnit, IsAvailable, IdBuilding,
         Floor, Tower, LocationCode, Bedrooms, Bathrooms, BuiltArea,
         IsCovered, IsForDisabled, VehicleType, Height, HasVentilation, HasElectricity, Notes)
    VALUES
        (@IdUnit, @UnitNumber, @Area, @Number, @TypeUnit, @IsAvailable, @IdBuilding,
         @Floor, @Tower, @LocationCode, @Bedrooms, @Bathrooms, @BuiltArea,
         @IsCovered, @IsForDisabled, @VehicleType, @Height, @HasVentilation, @HasElectricity, @Notes);
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
    @Notes NVARCHAR(500) = NULL
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
        Notes = @Notes
    WHERE IdUnit = @IdUnit;
END
GO

-- Sólo los campos nuevos, por edificio -- se mergean en memoria contra el
-- resultado (sin tocar) de GET_UnitsByBuilding en
-- BDLayout.GetUnitsByBuildingAsync.
CREATE OR ALTER PROCEDURE dbo.GET_UnitExtraFieldsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        IdUnit, Floor, Tower, LocationCode, Bedrooms, Bathrooms, BuiltArea,
        IsCovered, IsForDisabled, VehicleType, Height, HasVentilation, HasElectricity, Notes
    FROM dbo.RealEstateUnit
    WHERE IdBuilding = @IdBuilding;
END
GO

-- Auditoría (ver BDLayout.StampAuditAsync / AuditableEntity.Unit).
CREATE OR ALTER PROCEDURE dbo.UPD_UnitAudit
    @IdUnit UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.RealEstateUnit
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdUnit = @IdUnit;
    ELSE
        UPDATE dbo.RealEstateUnit
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdUnit = @IdUnit;
END
GO
