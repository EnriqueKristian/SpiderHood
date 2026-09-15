-- =============================================================================
-- Fundación: unidades sin propietario -> facturadas a la Inmobiliaria, y modo
-- de distribución (Mixto/Todo Fijo/Todo Porcentaje) a nivel Edificio.
-- (Docs/Pendientes-Negocio-Consolidado.md #1 -- decisión de negocio: usar
-- BuildingConfiguration.RealEstateCompany como pagador, no un Owner ficticio.)
--
-- Modelo real confirmado en BD (los nombres de las clases C# NO coinciden con
-- las tablas, mismo patrón visto ya varias veces en este repo):
--   RealEstateUnit.IdGroupUnit  -> FK directa a GroupUnit (NULL = sin vender)
--   GroupUnit (IdGroupUnit, GroupNumber, TotalArea) -- la entidad que se
--     factura de verdad (Installment.IdGroupUnit apunta acá)
--   OwnerGroupRole (IdOwner, Role, IdGroupUnit) -- une un ApartmentOwner a un
--     GroupUnit ("Role"=1 es el titular -- LoadDataDefaultAsync filtra por
--     Role==1 para armar la lista de owners que se facturan)
--   VW_OwnerUnit -- arranca FROM ApartmentOwner (join hacia afuera), por eso
--     una unidad sin fila de Owner nunca aparece en GET_OwnerByBuilding hoy.
--
-- 1. ApartmentOwner.IsRealEstateCompanyOwner -- marca el Owner "Inmobiliaria"
--    (protegido en UI/borrado, igual que Category.IsSystemCategory).
-- 2. BuildingConfiguration.DistributionMode -- 0=Mixto (default, cada
--    Categoría conserva su Distribution individual) / 1=TodoFijo /
--    2=TodoPorcentual.
-- 3. UPD_SyncUnsoldUnitsToRealEstateCompany -- crea (si no existe) UN
--    GroupUnit + Owner "Inmobiliaria" por edificio y enlaza ahí TODAS las
--    RealEstateUnit sin grupo (cualquier TypeUnit -- Depto/Oficina/
--    Estacionamiento/Depósito), recalculando TotalArea. Reutiliza la misma
--    lógica de INS_GroupOwner/UPD_GroupOwner ya existentes, no la duplica.
--    No hace nada (RAISERROR informativo) si el edificio todavía no cargó
--    los datos de la Inmobiliaria en Edificios > Inmobiliaria -- sin un
--    Name real ahí, el "titular" del recibo consolidado quedaría vacío.
-- 4. UPD_Category_BulkDistribution -- fuerza todas las Categorías NO-sistema
--    de un edificio a un solo TipoDistribucion (para TodoFijo/TodoPorcentual).
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

-- ---------------------------------------------------------------------------
-- 1. ApartmentOwner.IsRealEstateCompanyOwner
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ApartmentOwner') AND name = 'IsRealEstateCompanyOwner')
    ALTER TABLE dbo.ApartmentOwner ADD IsRealEstateCompanyOwner BIT NOT NULL CONSTRAINT DF_ApartmentOwner_IsRealEstateCompanyOwner DEFAULT (0);
GO

-- VW_OwnerUnit / GET_OwnerByBuilding: se agrega la columna nueva al SELECT
-- ahora (aunque el modelo C# OwnerUnitView todavía no la lea -- eso es Fase 2)
-- para no tener que volver a tocar SQL cuando se conecte del lado C#.
CREATE OR ALTER VIEW dbo.VW_OwnerUnit
AS
	SELECT
		ISNULL(gu.IdGroupUnit, '00000000-0000-0000-0000-000000000000') AS IdGroupUnit,
		ISNULL(gu.TotalArea, 0) AS TotalArea,
		ISNULL(gu.GroupNumber, 0) AS GroupNumber,
		ISNULL(r.IdUnit, '00000000-0000-0000-0000-000000000000') AS IdUnit,
		ISNULL(r.UnitNumber, '') AS UnitNumber,
		ISNULL(r.Area, 0) AS Area,
		ISNULL(r.TypeUnit, 1) AS TypeUnit,
		ISNULL(r.Number, 0) AS Number,
		ISNULL(r.IsAvailable, 0) AS IsAvailable,
		ISNULL(owr.IdGroupOwnerRol, '00000000-0000-0000-0000-000000000000') AS IdGroupOwnerRol,
		ISNULL(owr.[Role], 1) AS [Role],
		o.IdOwner,
		o.IdentityDocument,
		o.[Address],
		o.PhoneNumber,
		ISNULL(o.FirstName, '') AS FirstName,
		ISNULL(o.LastName, '') AS LastName,
		ISNULL(o.Email, '') AS Email,
		o.IsActive,
		o.IdBuilding,
		ISNULL(o.IdTypeIdNumber, 0) AS IdTypeIdNumber,
		o.IsRealEstateCompanyOwner
	FROM	ApartmentOwner o
	LEFT JOIN OwnerGroupRole owr ON owr.IdOwner = o.IdOwner
	LEFT JOIN GroupUnit gu ON gu.IdGroupUnit = owr.IdGroupUnit
	LEFT JOIN RealEstateUnit r ON r.IdGroupUnit = gu.IdGroupUnit;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_OwnerByBuilding]
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdGroupUnit, TotalArea, GroupNumber, IdUnit, UnitNumber, Area, TypeUnit, Number, IsAvailable, IdGroupOwnerRol, [Role], IdOwner, IdentityDocument, [Address], PhoneNumber, FirstName, LastName, Email, IsActive, IdBuilding, IdTypeIdNumber, IsRealEstateCompanyOwner
    FROM    VW_OwnerUnit
    WHERE   IdBuilding = @IdBuilding
    ORDER BY TypeUnit, [role], GroupNumber
END
GO

-- ---------------------------------------------------------------------------
-- 2. BuildingConfiguration.DistributionMode
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'DistributionMode')
    ALTER TABLE dbo.BuildingConfiguration ADD DistributionMode INT NOT NULL CONSTRAINT DF_BuildingConfiguration_DistributionMode DEFAULT (0);
GO

-- @DistributionMode queda al FINAL con default -- la llamada posicional
-- existente (11 parámetros, BDLayout.Add.cs) sigue funcionando sin tocarla,
-- igual que se hizo con ExpenseApprovalThreshold en su momento.
CREATE OR ALTER PROCEDURE [dbo].[INS_BuildingConfiguration]
    @IdBuildingConfiguration UNIQUEIDENTIFIER,
    @Currency VARCHAR(5),
    @PaymentMethods VARCHAR(100),
    @PaymentPeriod INT,
    @DueDay INT,
    @FineAmount DECIMAL(18,2),
    @LateInterestRate DECIMAL(18,2),
    @InvoiceDay INT,
    @MinWaterConsumtion DECIMAL(18,2),
    @DefaultFixedCharge DECIMAL(18,2),
    @IdBuilding UNIQUEIDENTIFIER,
    @DistributionMode INT = 0
AS
BEGIN
    INSERT INTO dbo.BuildingConfiguration (
        IdBuildingConfiguration, Currency, PaymentMethods, PaymentPeriod,
        DueDay, FineAmount, LateInterestRate, InvoiceDay, MinWaterConsumtion, DefaultFixedCharge, IdBuilding,
        DistributionMode
    )
    VALUES (
        @IdBuildingConfiguration, @Currency, @PaymentMethods, @PaymentPeriod,
        @DueDay, @FineAmount, @LateInterestRate, @InvoiceDay, @MinWaterConsumtion, @DefaultFixedCharge, @IdBuilding,
        @DistributionMode
    );
END;
GO

-- Mismo criterio que UPD_BuildingConfiguration_ExpenseThreshold: un UPD chico
-- y propio para este único campo, en vez de sumarlo a la lista posicional de
-- 16 parámetros de UPD_BuildingConfiguration (BDLayout.Update.cs la llama
-- posicionalmente -- agregar uno ahí exige tocar esa lista Y el SP en el
-- mismo orden exacto, sin ningún error de compilación si se desalinean).
CREATE OR ALTER PROCEDURE dbo.UPD_BuildingConfiguration_DistributionMode
    @IdBuildingConfiguration UNIQUEIDENTIFIER,
    @DistributionMode INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.BuildingConfiguration
    SET DistributionMode = @DistributionMode
    WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_AllBuildingsConfig]
@IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  c.IdBuildingConfiguration,
            c.Currency,
            c.PaymentMethods,
            c.PaymentPeriod,
            c.DueDay,
            c.FineAmount,
            c.LateInterestRate,
            c.InvoiceDay,
            c.IdBuilding,
            c.DefaultCategory,
            c.DefaultFixedCharge,
            c.MinWaterConsumtion,
            c.WaterReadingDefault,
            c.DebtWarningDays,
            c.DebtCriticalDays,
            c.ReceiptFooterText,
            c.ExpenseApprovalThreshold,
            c.DistributionMode
    FROM    BuildingConfiguration c
    JOIN    Building b ON c.IdBuilding = b.IdBuilding
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

-- GET_BuildingConfiguration usa SELECT * -- no hace falta tocarlo, la columna
-- nueva ya viaja sola.

-- ---------------------------------------------------------------------------
-- 3. Sincronizar unidades sin propietario -> Grupo "Inmobiliaria"
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.UPD_SyncUnsoldUnitsToRealEstateCompany
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdGroupUnit UNIQUEIDENTIFIER;
    DECLARE @IdOwner UNIQUEIDENTIFIER;
    DECLARE @CompanyName NVARCHAR(100);
    DECLARE @CompanyEmail NVARCHAR(100);
    DECLARE @CompanyPhone NVARCHAR(50);

    -- ¿Ya existe el Owner "Inmobiliaria" de este edificio? (idempotente --
    -- reutiliza el mismo grupo en cada corrida, no crea uno nuevo cada vez)
    SELECT TOP 1 @IdOwner = o.IdOwner, @IdGroupUnit = owr.IdGroupUnit
    FROM dbo.ApartmentOwner o
    JOIN dbo.OwnerGroupRole owr ON owr.IdOwner = o.IdOwner
    WHERE o.IdBuilding = @IdBuilding AND o.IsRealEstateCompanyOwner = 1;

    IF @IdOwner IS NULL
    BEGIN
        -- Datos de la Inmobiliaria (Contact.TypeContact = 2, cargado desde
        -- Edificios > Inmobiliaria) -- sin esto no hay a nombre de quién
        -- emitir el recibo consolidado, así que no se crea nada todavía.
        SELECT TOP 1 @CompanyName = ct.Name, @CompanyEmail = ct.Email, @CompanyPhone = ct.Phone
        FROM dbo.Contact ct
        JOIN dbo.BuildingConfiguration bc ON bc.IdBuildingConfiguration = ct.IdRelatedEntity
        WHERE bc.IdBuilding = @IdBuilding AND ct.TypeContact = 2;

        IF @CompanyName IS NULL OR LTRIM(RTRIM(@CompanyName)) = ''
        BEGIN
            RAISERROR('No hay datos de la Inmobiliaria configurados para este edificio (Edificios > Inmobiliaria). Configúralos antes de sincronizar unidades sin propietario.', 16, 1);
            RETURN;
        END

        SET @IdOwner = NEWID();
        INSERT INTO dbo.ApartmentOwner
            (IdOwner, IdentityDocument, Address, PhoneNumber, IdBuilding, FirstName, LastName, Email, IsActive, BusinessName, IsRealEstateCompanyOwner)
        VALUES
            (@IdOwner, '', '', ISNULL(@CompanyPhone, ''), @IdBuilding, @CompanyName, '', @CompanyEmail, 1, @CompanyName, 1);

        SET @IdGroupUnit = NEWID();
        DECLARE @NextGroupNumber INT;
        SELECT @NextGroupNumber = ISNULL(MAX(gu.GroupNumber), 0) + 1
        FROM dbo.GroupUnit gu
        JOIN dbo.RealEstateUnit r ON r.IdGroupUnit = gu.IdGroupUnit
        WHERE r.IdBuilding = @IdBuilding;

        -- Reusa INS_GroupOwner (crea GroupUnit + OwnerGroupRole con Role=1,
        -- el mismo "titular" que ya usa cualquier grupo real) en vez de
        -- duplicar el INSERT acá.
        EXEC dbo.INS_GroupOwner
            @IdGroupOwner = @IdGroupUnit,
            @IdOwner = @IdOwner,
            @Name = N'Inmobiliaria',
            @AreaTotal = 0,
            @TypeOwner = 1,
            @GroupNumber = @NextGroupNumber;
    END

    -- Enlazar TODAS las unidades sin grupo (cualquier TypeUnit) al grupo
    -- Inmobiliaria -- set-based, no una por una.
    UPDATE dbo.RealEstateUnit
    SET IdGroupUnit = @IdGroupUnit
    WHERE IdBuilding = @IdBuilding AND IdGroupUnit IS NULL;

    -- Recalcular el área total del grupo con TODAS las unidades ya
    -- enlazadas (reusa UPD_GroupOwner, mismo mecanismo que arma un grupo
    -- real cuando se le agregan unidades a mano).
    DECLARE @NewAreaTotal DECIMAL(18,2);
    SELECT @NewAreaTotal = ISNULL(SUM(Area), 0)
    FROM dbo.RealEstateUnit
    WHERE IdGroupUnit = @IdGroupUnit;

    EXEC dbo.UPD_GroupOwner @IdGroupOwner = @IdGroupUnit, @AreaTotal = @NewAreaTotal;
END
GO

-- ---------------------------------------------------------------------------
-- 4. Forzar Distribution en bloque (Modo Todo Fijo / Todo Porcentaje)
-- ---------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.UPD_Category_BulkDistribution
    @IdBuilding UNIQUEIDENTIFIER,
    @Distribution INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Mismo criterio de protección que UPD_Category: nunca toca "Sin
    -- Categorizar" ni ninguna otra categoría de sistema.
    UPDATE dbo.Category
    SET Distribution = @Distribution
    WHERE IdBuilding = @IdBuilding AND IsSystemCategory = 0;
END
GO
