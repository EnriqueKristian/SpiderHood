-- =============================================================================
-- Diagnóstico de solo lectura (housekeeping del entorno de desarrollo del
-- usuario, no forma parte de ningún Paso del plan) -- no borra nada. Responde:
-- "¿por qué no puedo borrar los usuarios que no sean admin@spiderhood.com?".
--
-- No hay ninguna función de borrado de usuarios en la app (ni UI, ni
-- BDLayout.DeleteRecordAsync, ni proc DEL_User) -- si se intentó borrar, fue
-- directo por SSMS. Este script encuentra DINÁMICAMENTE (via sys.foreign_keys)
-- todas las tablas que tienen una FK real hacia dbo.Users(IdUser), y cuenta
-- cuántas filas referencian a cada usuario que NO sea el que se quiere conservar
-- -- así no dependemos de listar las tablas a mano y arriesgarnos a que un DELETE
-- explote a mitad de camino por una FK que no vimos.
--
-- Ajustá @KeepEmail si el usuario a conservar es otro.
-- =============================================================================

SET NOCOUNT ON;
GO

DECLARE @KeepEmail NVARCHAR(256) = N'admin@spiderhood.com';

PRINT '--- Usuarios candidatos a borrar (todos menos @KeepEmail) ---';
SELECT IdUser, Email, FirstName, LastName, IsActive
FROM dbo.Users
WHERE Email <> @KeepEmail;

PRINT '--- Filas que los referencian, por tabla (0 = esa tabla no bloquea nada) ---';
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql +
    N'SELECT ''' + s.name + '.' + t.name + ''' AS Tabla, ''' + c.name + ''' AS Columna, COUNT(*) AS Filas ' +
    N'FROM ' + QUOTENAME(s.name) + '.' + QUOTENAME(t.name) + ' x ' +
    N'JOIN dbo.Users u ON u.IdUser = x.' + QUOTENAME(c.name) + ' ' +
    N'WHERE u.Email <> @KeepEmail ' +
    N'UNION ALL '
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables  t ON t.object_id = fkc.parent_object_id
JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
WHERE fk.referenced_object_id = OBJECT_ID('dbo.Users');

IF LEN(@sql) > 0
BEGIN
    SET @sql = LEFT(@sql, LEN(@sql) - LEN('UNION ALL '));
    EXEC sp_executesql @sql, N'@KeepEmail NVARCHAR(256)', @KeepEmail;
END
ELSE
    PRINT 'No se encontró ninguna FK física hacia dbo.Users -- si el borrado igual falla, no es por esto (revisar el mensaje de error real de SSMS).';
GO
