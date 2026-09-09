-- =============================================================================
-- Fix: GET_ServiceReadingDetailList devolvía cada fila duplicada.
--
-- Causa raíz (confirmada por el usuario, definición real del SP):
--
--   JOIN GroupUnit g ON wr.IdGroupUnit = g.IdGroupUnit
--
-- GroupUnit tiene una fila por CADA unidad física (RealEstateUnit) que
-- compone un Grupo de Unidades -- un grupo con más de una unidad física
-- (ej. departamento + cochera + depósito facturados juntos bajo el mismo
-- IdGroupUnit, mismo concepto que ya se documentó al corregir el % de
-- Morosidad del dashboard) tiene más de una fila en GroupUnit para ese
-- mismo IdGroupUnit. El JOIN directo contra GroupUnit multiplica
-- (fan-out) cada fila de VW_ServiceReadingDetail por la cantidad de
-- unidades físicas del grupo -- confirmado con datos reales: cada
-- IdServiceReadingDetail aparecía EXACTAMENTE dos veces, con
-- CurrentReading/Consumption/CalculatedAmount/IdServiceReading idénticos
-- entre ambas copias (vienen de wr, sin duplicar) y sólo PreviousReading
-- distinto -- ver Docs/Pendientes-Negocio-Agua.md #3 para el detalle
-- completo de cómo se diagnosticó.
--
-- Fix: en vez de unir directo contra GroupUnit (1 o más filas por
-- IdGroupUnit), se une contra una subconsulta que primero colapsa
-- GroupUnit a UNA fila por IdGroupUnit (MIN(GroupNumber), determinístico
-- -- con varias unidades físicas en el mismo grupo, muestra el número más
-- bajo, típicamente el departamento en vez de la cochera/depósito). Se
-- mantiene el resto del SP idéntico (misma firma, mismas columnas, mismo
-- orden) para no romper ningún caller existente.
--
-- No corrige el otro problema encontrado (el SP no filtra por
-- IdBuilding, sólo por Period) -- eso requeriría agregar un parámetro
-- nuevo y tocar todos los callers de GetServiceReadingDetailbyPeriodAsync
-- (BlockWaterReading, MyReceipts, MyPayments, InstallmentDetailModal,
-- BudgetGenerator, los reportes de Consumo de Agua), un cambio más
-- grande que se deja para cuando el usuario decida encararlo -- mitigado
-- por ahora del lado del cliente en
-- ICalculoService.GetServiceReadingDetailsByBuildingAsync.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_ServiceReadingDetailList
@Period   DATETIME
AS
BEGIN
        SELECT  IdServiceReadingDetail, wr.IdGroupUnit, g.GroupNumber, [Period], Code, PreviousReading, CurrentReading, ReadingDate, CalculatedAmount, Minimum, IdServiceReading, Consumption
        FROM    VW_ServiceReadingDetail wr
        JOIN    (SELECT IdGroupUnit, MIN(GroupNumber) AS GroupNumber FROM GroupUnit GROUP BY IdGroupUnit) g
                ON wr.IdGroupUnit = g.IdGroupUnit
        WHERE   [Period] = @Period
        ORDER BY g.GroupNumber;
END;
GO
