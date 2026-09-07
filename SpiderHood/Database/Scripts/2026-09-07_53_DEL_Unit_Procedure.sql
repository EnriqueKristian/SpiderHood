-- Crea el stored procedure DEL_Unit, que faltaba en la base de datos.
-- IBuildingService.DeleteUnitAsync (Services/IBuildingService.cs) siempre lo
-- invocó, pero como no existía, cada intento de borrar una unidad fallaba con
-- "Could not find stored procedure 'DEL_Unit'" -- error que además se tragaba
-- en silencio (catch sin rethrow), asi que la UI quedaba como si el borrado
-- hubiera funcionado. Ahora DeleteUnitAsync devuelve OperationResult y
-- distingue esta clase de error de una violación real de FK (SQL 547), asi
-- que hace falta que el proc exista para que la validación de asociaciones
-- (propietario, grupo de unidades, cuotas) se dispare correctamente via las
-- FKs reales de dbo.RealEstateUnit en vez de un catch genérico.
--
-- Idempotente: CREATE OR ALTER no falla si el proc ya existe.
--
-- Ejecutar contra la base de datos de la app (ver DEPLOY-Production.md §2).

CREATE OR ALTER PROCEDURE dbo.DEL_Unit
    @IdUnit UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.RealEstateUnit
    WHERE IdUnit = @IdUnit;
END
GO
