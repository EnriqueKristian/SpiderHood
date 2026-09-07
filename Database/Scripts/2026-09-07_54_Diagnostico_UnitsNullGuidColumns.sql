-- =============================================================================
-- Diagnóstico de solo lectura (no forma parte de ningún Paso del plan, no
-- borra ni modifica nada) -- responde: "¿qué unidad de este edificio está
-- rompiendo GET_UnitsByType con SqlNullValueException?".
--
-- Contexto: BuildingService.GetGroupUnitsByTypeAsync (que llama a
-- GET_UnitsByType) mapea el resultado a Models.UnitView, cuyas columnas
-- Guid (IdUnit, IdBuilding, IdGroupUnit) NO son nullable en C# -- si el SP
-- devuelve NULL en cualquiera de ellas para una fila (típicamente porque esa
-- fila viene de un LEFT JOIN y la unidad todavía no tiene grupo/propietario
-- asignado), EF revienta con SqlNullValueException al leer esa fila con
-- SqlDataReader.GetGuid(). Esto ya afectaba el botón "Descargar plantilla"
-- de Lecturas de Agua (BlockWaterReading.razor) antes de esta plantilla --
-- no es un bug nuevo, solo no se había disparado hasta ahora en este edificio.
--
-- No conozco desde acá el nombre físico exacto de la tabla de unidades ni
-- cuál JOIN arma GET_UnitsByType (no está en Database/Scripts, vive
-- directo en la BD) -- así que este script NO asume nombres de columna:
-- descubre dinámicamente (vía sys.columns) todas las columnas uniqueidentifier
-- de dbo.RealEstateUnit y cuenta/lista cuáles tienen NULL para el edificio
-- indicado. Eso alcanza para encontrar la fila problemática sin necesidad
-- de ver el SP.
--
-- Ajustá @IdBuilding al edificio que estás migrando (Building.razor -> el
-- Guid aparece en la URL o en Configuración > Información del Edificio).
-- =============================================================================

SET NOCOUNT ON;
GO

DECLARE @IdBuilding UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- <-- reemplazar

IF @IdBuilding = '00000000-0000-0000-0000-000000000000'
BEGIN
    PRINT 'Reemplazá @IdBuilding por el Guid real del edificio antes de correr este script.';
    RETURN;
END

PRINT '--- Columnas uniqueidentifier de dbo.RealEstateUnit ---';
SELECT c.name AS Columna, c.is_nullable AS EsNullableEnBD
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.RealEstateUnit') AND t.name = 'uniqueidentifier';

PRINT '--- Unidades del edificio con alguna columna Guid en NULL (candidatas a romper GET_UnitsByType) ---';
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql +
    N'SELECT ''' + c.name + ''' AS ColumnaEnNull, u.IdUnit, u.UnitNumber, u.TypeUnit, u.Number, u.IsAvailable ' +
    N'FROM dbo.RealEstateUnit u ' +
    N'WHERE u.IdBuilding = @IdBuilding AND u.' + QUOTENAME(c.name) + ' IS NULL ' +
    N'UNION ALL '
FROM sys.columns c
JOIN sys.types t ON t.user_type_id = c.user_type_id
WHERE c.object_id = OBJECT_ID('dbo.RealEstateUnit')
  AND t.name = 'uniqueidentifier'
  AND c.name NOT IN ('IdUnit', 'IdBuilding'); -- estas dos son PK/FK obligatorias, nunca deberían salir NULL

IF LEN(@sql) > 0
BEGIN
    SET @sql = LEFT(@sql, LEN(@sql) - LEN('UNION ALL '));
    EXEC sp_executesql @sql, N'@IdBuilding UNIQUEIDENTIFIER', @IdBuilding;
END
ELSE
    PRINT 'dbo.RealEstateUnit no tiene columnas uniqueidentifier además de IdUnit/IdBuilding -- el NULL que rompe GetGuid no está en esta tabla; probablemente venga de la tabla de grupos/propietarios que GET_UnitsByType une con LEFT JOIN (revisar el SP directo en SSMS).';

PRINT '--- Todas las unidades del edificio, para contexto ---';
SELECT IdUnit, UnitNumber, TypeUnit, Number, IsAvailable
FROM dbo.RealEstateUnit
WHERE IdBuilding = @IdBuilding
ORDER BY UnitNumber;
GO
