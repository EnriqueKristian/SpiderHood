-- =============================================================================
-- Fix: GET_LastReconciliationSession (2026-09-09_71_ReconciliationSession.sql)
-- hacía SELECT * sobre dbo.ReconciliationSession, cuya columna se llama
-- IdBankAccount -- pero Models.Conciliacion (Classes/Budget/Conciliacion.cs)
-- ya tenía la propiedad CuentaBancariaId desde antes (no se renombró al
-- agregar la tabla). EF mapea por NOMBRE de columna en un FromSqlRaw, así
-- que tiraba "The required column 'CuentaBancariaId' was not present in the
-- results of a 'FromSql' operation" en cuanto se abría /conciliacion.
--
-- No se renombra la columna de la tabla (evita un ALTER TABLE sobre datos ya
-- cargados) -- alcanza con alias en el SELECT de este único SP, que es el
-- único lugar que hace SELECT * sobre esta tabla.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_LastReconciliationSession
    @IdBankAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        Id,
        IdBankAccount AS CuentaBancariaId,
        IdBuilding,
        FechaInicio,
        FechaFin,
        TransaccionesProcesadas,
        TransaccionesConciliadas,
        Diferencia,
        Completada,
        Fecha,
        Usuario,
        Notas
    FROM dbo.ReconciliationSession
    WHERE IdBankAccount = @IdBankAccount
    ORDER BY Fecha DESC;
END
GO
