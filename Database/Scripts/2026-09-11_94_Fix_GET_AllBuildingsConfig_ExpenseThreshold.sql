-- =============================================================================
-- URGENTE: GET_AllBuildingsConfig (usada por AuthService.LoginAsync en CADA
-- login) lista columnas explícitas y no incluía ExpenseApprovalThreshold
-- (agregada en 2026-09-11_93) -- EF Core (FromSqlRaw<BuildingConfiguration>)
-- exige que el SELECT devuelva TODAS las columnas mapeadas de la entidad, así
-- que tronaba con "required column ... was not present" apenas alguien
-- intentaba loguearse. Se agrega la columna al SELECT, sin tocar el resto
-- (mismo texto confirmado con sp_helptext, sólo se suma una línea).
-- =============================================================================

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
            c.ExpenseApprovalThreshold
    FROM    BuildingConfiguration c
    JOIN    Building b ON c.IdBuilding = b.IdBuilding
    JOIN    UserBuildingAssociation ub ON b.IdBuilding = ub.IdBuilding
    WHERE   ub.IdUser = @IdUser
END
