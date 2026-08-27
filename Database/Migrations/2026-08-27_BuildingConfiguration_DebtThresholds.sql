-- Agrega los umbrales de días de atraso (para colorear la Deuda en /cuotas) a
-- BuildingConfiguration, configurables por edificio en Configuración > Defaults,
-- Multas y Mora. De paso corrige un JOIN mal escrito en GET_AllBuildingsConfig
-- (ver el paso 3 más abajo).
--
-- Ejecutar en orden, contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

-- 1) Nuevas columnas. NOT NULL con default, mismo patrón que DefaultFixedCharge.
ALTER TABLE dbo.BuildingConfiguration
    ADD DebtWarningDays  INT NOT NULL CONSTRAINT DF_BuildingConfiguration_DebtWarningDays  DEFAULT (30),
        DebtCriticalDays INT NOT NULL CONSTRAINT DF_BuildingConfiguration_DebtCriticalDays DEFAULT (60);
GO

-- 2) UPD_BuildingConfiguration: se agregan los 2 parámetros nuevos AL FINAL de la lista.
--    El orden importa: BDLayout.Update.cs llama a este SP con parámetros posicionales
--    (EF Core ExecuteSqlRawAsync con {0},{1},...), así que el código en C# se actualiza
--    para pasarlos en este mismo orden.
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
    @DebtCriticalDays INT
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
        DebtCriticalDays = @DebtCriticalDays
    WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
END;
GO

-- 3) GET_AllBuildingsConfig: lista columnas explícitas, hay que sumar las 2 nuevas o
--    EF Core (FromSqlRaw<BuildingConfiguration>) truena buscándolas en el resultado.
--    De paso se corrige "JOIN Building b ON c.IdBuilding = c.IdBuilding": comparaba la
--    columna consigo misma (siempre true), así que el JOIN a Building no filtraba nada
--    y la consulta devolvía la configuración de TODOS los edificios (duplicada una vez
--    por cada edificio asociado al usuario), no solo la de sus propios edificios. El
--    llamador (AuthenticationService, en el login) hoy blinda el resultado final
--    re-filtrando por IdBuilding en C#, así que no se veían datos de otros edificios en
--    pantalla, pero la consulta hacía trabajo de más y quedaba expuesta a filtrar mal si
--    ese re-filtro cambia. Se corrige a la condición real: c.IdBuilding = b.IdBuilding.
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
            c.DebtCriticalDays
    FROM    BuildingConfiguration c
    JOIN    Building b ON c.IdBuilding = b.IdBuilding
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
GO

-- GET_BuildingConfiguration usa SELECT *, así que no necesita cambios: las columnas
-- nuevas aparecen solas en cuanto corre el paso 1.
