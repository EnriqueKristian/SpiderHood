-- =============================================================================
-- Paso 2 del plan en Docs/Design-Defaults-Sistema-Mixto.md: Edificio Template.
--
-- Un Building normal puede marcarse IsTemplate = 1 -- lo edita el SysAdmin con las
-- mismas pantallas que cualquier otro edificio (Configuración/Parámetros/
-- Categorías). Al crear un Building real, CreateBuildingAsync clona los valores de
-- BuildingConfiguration del template (Currency, PaymentMethods, PaymentPeriod,
-- DueDay, FineAmount, MinWaterConsumtion, DefaultFixedCharge, LateInterestRate,
-- InvoiceDay, DebtWarningDays, DebtCriticalDays, ReceiptFooterText) en vez de los
-- valores hardcodeados de CreateDefaultConfigurationAsync. Si no hay ningún
-- template todavía, sigue usando el fallback hardcodeado (no rompe nada).
--
-- Deliberadamente NO se restringe a un único IsTemplate=1: el usuario mencionó que
-- le sirve poder tener más de un edificio "template/demo" a mano. Si hay más de
-- uno marcado, GET_TemplateBuilding trae uno solo (TOP 1) -- el primero por
-- IdBuilding, sin ninguna lógica de cuál "gana"; ampliar esto a elegir entre
-- varios templates queda para cuando haga falta de verdad.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'IsTemplate'
)
BEGIN
    ALTER TABLE dbo.Building ADD IsTemplate BIT NOT NULL DEFAULT 0;
END
GO

-- Reemplaza el INS_Building de 2026-09-02_18 (sin @Number, ya corregido) para
-- sumar @IsTemplate.
CREATE OR ALTER PROCEDURE dbo.INS_Building
    @IdBuilding     UNIQUEIDENTIFIER,
    @Name           NVARCHAR(200),
    @Location       NVARCHAR(300),
    @Type           INT,
    @Floors         INT,
    @Basements      INT,
    @Apartments     INT,
    @Parkings       INT,
    @Deposits       INT,
    @Others         INT,
    @TotalArea      DECIMAL(18, 2),
    @IsActive       BIT,
    @IsTemplate     BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Building
        (IdBuilding, Name, Location, Type, Floors, Basements, Apartments,
         Parkings, Deposits, Others, TotalArea, IsActive, IsTemplate)
    VALUES
        (@IdBuilding, @Name, @Location, @Type, @Floors, @Basements,
         @Apartments, @Parkings, @Deposits, @Others, @TotalArea, @IsActive, @IsTemplate);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Building
    @IdBuilding     UNIQUEIDENTIFIER,
    @Name           NVARCHAR(200),
    @Location       NVARCHAR(300),
    @Type           INT,
    @Floors         INT,
    @Basements      INT,
    @Apartments     INT,
    @Parkings       INT,
    @Deposits       INT,
    @Others         INT,
    @TotalArea      DECIMAL(18, 2),
    @IsActive       BIT,
    @IsTemplate     BIT
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
        IsTemplate = @IsTemplate
    WHERE IdBuilding = @IdBuilding;
END
GO

-- SELECT * a propósito (no una lista de columnas a mano) -- así no hay riesgo de
-- que se desalinee con las columnas reales de dbo.Building como pasó con
-- Number/IsTemplate en los scripts anteriores.
CREATE OR ALTER PROCEDURE dbo.GET_TemplateBuilding
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 *
    FROM dbo.Building
    WHERE IsTemplate = 1
    ORDER BY IdBuilding;
END
GO
