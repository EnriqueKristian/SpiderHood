-- =============================================================================
-- Limpieza de datos de prueba (housekeeping, no es parte del plan) -- borra el
-- edificio "Prueba 1" completo. Mismo patrón que
-- 2026-09-02_23_Cleanup_TestBuildings.sql, adaptado a un solo edificio por
-- nombre, y con un paso nuevo que ese script no necesitaba: ahora Category tiene
-- FK real hacia Expense/Exoneration/BudgetDetail/CalendarItem (Paso 4,
-- 2026-09-02_24_Category_RealFK.sql) -- si "Prueba 1" tiene algo cargado en esas
-- pantallas, el DELETE de Category falla en vez de dejar basura huérfana (se
-- borran esas filas primero, sólo las de este edificio).
--
-- Todo dentro de una transacción -- si algo falla a mitad de camino, no queda el
-- edificio a medio borrar.
-- =============================================================================

SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

BEGIN TRY

    DECLARE @IdPrueba1 UNIQUEIDENTIFIER = (SELECT IdBuilding FROM dbo.Building WHERE Name = N'Prueba 1');

    IF @IdPrueba1 IS NULL
    BEGIN
        RAISERROR('No se encontró ningún edificio llamado "Prueba 1" -- revisá el nombre antes de correr esto.', 16, 1);
    END

    DELETE FROM dbo.UserBuildingAssociation WHERE IdBuilding = @IdPrueba1;

    -- Filas que referencian una Category de este edificio (FK real, Paso 4) --
    -- se borran antes de poder borrar la Category en sí.
    DELETE e FROM dbo.Expense e JOIN dbo.Category c ON c.IdCategory = e.IdCategory WHERE c.IdBuilding = @IdPrueba1;
    DELETE e FROM dbo.Exoneration e JOIN dbo.Category c ON c.IdCategory = e.IdCategory WHERE c.IdBuilding = @IdPrueba1;
    DELETE bd FROM dbo.BudgetDetail bd JOIN dbo.Category c ON c.IdCategory = bd.IdCategory WHERE c.IdBuilding = @IdPrueba1;
    DELETE ci FROM dbo.CalendarItem ci JOIN dbo.Category c ON c.IdCategory = ci.IdCategory WHERE c.IdBuilding = @IdPrueba1;

    DELETE FROM dbo.Category WHERE IdBuilding = @IdPrueba1;
    DELETE FROM dbo.Parameter WHERE IdBuilding = @IdPrueba1;
    DELETE FROM dbo.BuildingConfiguration WHERE IdBuilding = @IdPrueba1;
    DELETE FROM dbo.Building WHERE IdBuilding = @IdPrueba1;

    COMMIT TRANSACTION;
    PRINT 'Prueba 1 eliminado.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
GO
