-- =============================================================================
-- GET_ServiceReadingDetailList sólo filtraba por @Period, nunca por edificio
-- -- ver Docs/Pendientes-Negocio-Agua.md #3, punto 1. Si dos edificios
-- tienen un ServiceReading con el mismo Period exacto, sus
-- ServiceReadingDetail se mezclaban (confirmado con datos reales: una
-- unidad "101" de OTRO edificio aparecía junto a la "101" del edificio
-- consultado).
--
-- Se agrega @IdBuilding, uniendo contra dbo.ServiceReading (que sí tiene
-- IdBuilding, confirmado en 2026-09-02_04_Audit_MoreHeaders.sql) por
-- IdServiceReading -- ya viene en el SELECT de este mismo SP, así que el
-- join es directo. Cambia la firma: hay que actualizar a la vez todos los
-- callers de GetServiceReadingDetailbyPeriodAsync (BDLayout.Get.cs) --
-- BlockWaterReading.razor, MyReceipts.razor, MyPayments.razor,
-- InstallmentList.razor, BudgetGenerator.razor y
-- GetServiceReadingDetailsByBuildingAsync (reportes de Consumo de Agua) --
-- se actualizan todos juntos en el mismo commit que este script.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_ServiceReadingDetailList
@Period      DATETIME,
@IdBuilding  UNIQUEIDENTIFIER
AS
BEGIN
        SELECT  IdServiceReadingDetail, wr.IdGroupUnit, g.GroupNumber, [Period], Code, PreviousReading, CurrentReading, ReadingDate, CalculatedAmount, Minimum, IdServiceReading, Consumption
        FROM    VW_ServiceReadingDetail wr
        JOIN    (SELECT IdGroupUnit, MIN(GroupNumber) AS GroupNumber FROM GroupUnit GROUP BY IdGroupUnit) g
                ON wr.IdGroupUnit = g.IdGroupUnit
        JOIN    dbo.ServiceReading sr
                ON sr.IdServiceReading = wr.IdServiceReading
        WHERE   [Period] = @Period
                AND sr.IdBuilding = @IdBuilding
        ORDER BY g.GroupNumber;
END;
GO
