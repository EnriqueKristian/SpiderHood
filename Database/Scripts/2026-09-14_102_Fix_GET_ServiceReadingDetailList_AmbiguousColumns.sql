-- =============================================================================
-- Fix: GET_ServiceReadingDetailList (Database/Scripts/2026-09-09_74_..._
-- FiltraPorEdificio.sql) agregó el JOIN contra dbo.ServiceReading para poder
-- filtrar por @IdBuilding, pero esa tabla también tiene columnas Period e
-- IdServiceReading (mismos nombres que VW_ServiceReadingDetail) -- el SELECT
-- y el WHERE de ese script las dejaron sin calificar, así que el SP nunca
-- pudo ni siquiera compilarse/ejecutarse ("Ambiguous column name 'Period'" /
-- "'IdServiceReading'"). Bloqueaba por completo abrir un Presupuesto ya
-- guardado (BudgetGenerator carga WaterReadings al inicializar) para
-- cualquier edificio con al menos una lectura de agua cargada.
--
-- Se califican esas dos columnas con el alias de la vista (wr.), que es de
-- donde tienen que salir -- el join contra sr sólo existía para el filtro de
-- @IdBuilding, no para aportar columnas al SELECT.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_ServiceReadingDetailList
@Period      DATETIME,
@IdBuilding  UNIQUEIDENTIFIER
AS
BEGIN
        SELECT  wr.IdServiceReadingDetail, wr.IdGroupUnit, g.GroupNumber, wr.[Period], wr.Code, wr.PreviousReading, wr.CurrentReading, wr.ReadingDate, wr.CalculatedAmount, wr.Minimum, wr.IdServiceReading, wr.Consumption
        FROM    VW_ServiceReadingDetail wr
        JOIN    (SELECT IdGroupUnit, MIN(GroupNumber) AS GroupNumber FROM GroupUnit GROUP BY IdGroupUnit) g
                ON wr.IdGroupUnit = g.IdGroupUnit
        JOIN    dbo.ServiceReading sr
                ON sr.IdServiceReading = wr.IdServiceReading
        WHERE   wr.[Period] = @Period
                AND sr.IdBuilding = @IdBuilding
        ORDER BY g.GroupNumber;
END;
GO
