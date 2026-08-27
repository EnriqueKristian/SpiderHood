-- Agrega el texto configurable del pie del recibo de mantenimiento (PDF) a
-- BuildingConfiguration, editable por edificio en Configuración > Defaults. Mismo
-- patrón que la migración anterior (DebtWarningDays/DebtCriticalDays).
--
-- El texto admite placeholders que InstallmentExportService reemplaza al generar el
-- PDF: {DPTO}, {Propietario}, {NroCta}, {Banco}, {Titular}, {CCI}, {Administrador},
-- {CorreoADM}. Si queda vacío, el recibo simplemente no muestra esa línea.
--
-- Ejecutar en orden, contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

-- 1) Nueva columna. NOT NULL con default vacío, mismo patrón que las anteriores.
ALTER TABLE dbo.BuildingConfiguration
    ADD ReceiptFooterText NVARCHAR(1000) NOT NULL CONSTRAINT DF_BuildingConfiguration_ReceiptFooterText DEFAULT ('');
GO

-- 2) UPD_BuildingConfiguration: se agrega el parámetro nuevo AL FINAL de la lista,
--    después de DebtCriticalDays (BDLayout.Update.cs llama a este SP con parámetros
--    posicionales, así que el orden importa).
ALTER PROCEDURE dbo.UPD_BuildingConfiguration
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
    @DefaultCategory UNIQUEIDENTIFIER,
    @WaterReadingDefault UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @DebtWarningDays INT,
    @DebtCriticalDays INT,
    @ReceiptFooterText NVARCHAR(1000)
AS
BEGIN
    UPDATE dbo.BuildingConfiguration
    SET Currency = @Currency,
        PaymentMethods = @PaymentMethods,
        PaymentPeriod = @PaymentPeriod,
        DueDay = @DueDay,
        FineAmount = @FineAmount,
        LateInterestRate = @LateInterestRate,
        InvoiceDay = @InvoiceDay,
        MinWaterConsumtion = @MinWaterConsumtion,
        DefaultFixedCharge = @DefaultFixedCharge,
        DefaultCategory = @DefaultCategory,
        WaterReadingDefault = @WaterReadingDefault,
        IdBuilding = @IdBuilding,
        DebtWarningDays = @DebtWarningDays,
        DebtCriticalDays = @DebtCriticalDays,
        ReceiptFooterText = @ReceiptFooterText
    WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
END;
GO

-- 3) GET_AllBuildingsConfig: lista columnas explícitas, hay que sumar la nueva o
--    EF Core (FromSqlRaw<BuildingConfiguration>) truena buscándola en el resultado.
ALTER PROCEDURE [dbo].[GET_AllBuildingsConfig]
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
            c.ReceiptFooterText
    FROM    BuildingConfiguration c
    JOIN    Building b ON c.IdBuilding = b.IdBuilding
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

-- GET_BuildingConfiguration usa SELECT *, así que no necesita cambios: la columna
-- nueva aparece sola en cuanto corre el paso 1.
