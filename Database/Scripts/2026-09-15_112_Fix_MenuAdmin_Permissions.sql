-- =============================================================================
-- Fix: "Administrar Menú" a veces no aplicaba los permisos que se enviaban.
--
-- Causa raíz: MenuAdminService.CreateMenuItemAsync/UpdateMenuItemAsync intentan
-- borrar TODOS los permisos existentes de un item de menú antes de re-insertar
-- el set nuevo elegido en el formulario, pero construían el objeto a borrar con
-- sólo IdMenu seteado (IdRole quedaba en el Guid.Empty por default de C#) --
-- DEL_MenuItemPermission borra por (IdMenu, IdRole) exacto, así que ese borrado
-- nunca encontraba ninguna fila real y no borraba nada. Resultado: un rol que
-- ya tenía acceso a un item de menú NUNCA podía perderlo destildando el
-- checkbox y guardando -- sólo se podían AGREGAR roles, nunca quitarlos, sin
-- ningún error visible (la UI mostraba "guardado" igual).
--
-- dbo.MenuPermissions tampoco tenía ninguna restricción UNIQUE -- cualquier
-- INSERT repetido de la misma pareja (IdMenu, IdRole) creaba una fila
-- duplicada en silencio en vez de fallar, así que el try/catch "asumir
-- duplicado" de MenuAdminService.UpdateMenuItemPermissionsAsync nunca se
-- disparaba por esa vía tampoco.
--
-- Fix: 1) nueva DEL_MenuItemPermissionsByMenu (borra TODAS las filas de un
-- IdMenu sin importar el rol -- mismo criterio que ya usa DEL_MenuItem para
-- limpiar antes de borrar el item entero). 2) dedupe + UNIQUE constraint real
-- sobre (IdMenu, IdRole) para que un INSERT repetido falle de verdad en vez
-- de acumular filas fantasma.
--
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.DEL_MenuItemPermissionsByMenu
    @IdMenu UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.MenuPermissions WHERE IdMenu = @IdMenu;
END
GO

-- Dedupe: si algún INSERT repetido ya alcanzó a crear filas duplicadas
-- (IdMenu, IdRole) antes de este fix, se quedan sólo con una -- necesario
-- para poder agregar la restricción UNIQUE de abajo sin que falle.
IF EXISTS (
    SELECT 1 FROM dbo.MenuPermissions
    GROUP BY IdMenu, IdRole
    HAVING COUNT(*) > 1
)
BEGIN
    ;WITH Duplicados AS (
        SELECT IdMenu, IdRole,
               ROW_NUMBER() OVER (PARTITION BY IdMenu, IdRole ORDER BY (SELECT NULL)) AS rn
        FROM dbo.MenuPermissions
    )
    DELETE FROM Duplicados WHERE rn > 1;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.MenuPermissions') AND name = 'UQ_MenuPermissions_Menu_Role'
)
BEGIN
    CREATE UNIQUE INDEX UQ_MenuPermissions_Menu_Role ON dbo.MenuPermissions (IdMenu, IdRole);
END
GO
