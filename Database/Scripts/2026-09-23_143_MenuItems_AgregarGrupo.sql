-- =============================================================================
-- Agrupamiento del menú lateral (GENERAL / COMUNIDAD / ADMINISTRACIÓN), pedido
-- por el usuario -- la dirección visual ya se había aprobado en el canvas de
-- diseño (Dashboard-Light) al principio de la sesión, con una clase CSS
-- (.nav-section-header) que quedó definida pero sin usar hasta ahora. El menú
-- no tenía ningún concepto de "grupo" -- ni en la tabla, ni en el modelo, ni
-- en los SPs -- así que hace falta esta migración (agregar la columna, no
-- sólo tocar el layout).
--
-- Sólo aplica a items RAÍZ (IdParent IS NULL) -- los hijos de un submenú no
-- necesitan grupo propio, quedan agrupados bajo su padre como siempre.
--
-- Idempotente: el ALTER TABLE se salta si la columna ya existe, y los UPDATE
-- de abajo sólo tocan filas que todavía no tienen grupo asignado.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'MenuItems' AND COLUMN_NAME = 'GroupName'
)
BEGIN
    ALTER TABLE MenuItems ADD GroupName NVARCHAR(50) NULL;
END
GO

-- Backfill de los items raíz conocidos, por ItemKey (más estable entre
-- ambientes que un Guid). "Reservas y Mantenimientos" tiene el ItemKey vacío
-- en al menos un ambiente (mismo caso ya documentado para "Asignar Roles" en
-- 2026-09-10_86_Fix_ItemKey_Vacio...sql), así que ese matchea por Título.
-- Cualquier item raíz que NO matchee acá (por un ItemKey/Título distinto en
-- esta base real) queda sin grupo -- no rompe nada, sólo no muestra encabezado
-- de sección hasta que se le asigne uno manualmente desde
-- /Settings/MenuItems (nuevo campo "Grupo" en el formulario).
UPDATE MenuItems SET GroupName = 'General'
WHERE IdParent IS NULL AND GroupName IS NULL
  AND ItemKey IN ('dashboard', 'building', 'budget', 'reconciliation_menu', 'reports');

UPDATE MenuItems SET GroupName = 'Comunidad'
WHERE IdParent IS NULL AND GroupName IS NULL
  AND ItemKey IN ('resident', 'board', 'incidents');

UPDATE MenuItems SET GroupName = 'Administración'
WHERE IdParent IS NULL AND GroupName IS NULL
  AND ItemKey IN ('employees', 'settings');

UPDATE MenuItems SET GroupName = 'Comunidad'
WHERE IdParent IS NULL AND GroupName IS NULL AND Title = 'Reservas y Mantenimientos';
GO

-- GET_FullMenu (usada por LeftMenu.razor para armar el menú real por rol) --
-- se agrega GroupName a las dos ramas del UNION (item directamente permitido
-- + su padre), igual que el resto de las columnas.
ALTER PROCEDURE [dbo].[GET_FullMenu]
@IdRole UNIQUEIDENTIFIER
AS
BEGIN
    ;WITH AllowedMenus AS (
        SELECT m.IdMenu, m.IdParent, m.ItemKey, m.Title, m.Icon, m.Url, m.Target, m.DisplayOrder, m.GroupName
        FROM MenuItems m
        JOIN MenuPermissions mp ON m.IdMenu = mp.IdMenu
        WHERE mp.IdRole = @IdRole

        UNION

        SELECT p.IdMenu, p.IdParent, p.ItemKey, p.Title, p.Icon, p.Url, p.Target, p.DisplayOrder, p.GroupName
        FROM MenuItems p
        JOIN MenuItems m ON p.IdMenu = m.IdParent
        JOIN MenuPermissions mp ON m.IdMenu = mp.IdMenu
        WHERE mp.IdRole = @IdRole
    )
    SELECT *
    FROM AllowedMenus
    ORDER BY IdParent, DisplayOrder;
END;
GO

-- GET_MenuItem (usada por /Settings/MenuItems -- administración del menú) --
-- se agrega GroupName en las dos ramas de la CTE recursiva (raíz + hijos; en
-- los hijos siempre va a venir NULL, que es lo esperado).
ALTER PROCEDURE [dbo].[GET_MenuItem]
AS
BEGIN
    ;WITH MenuCTE AS (
        SELECT
            m.IdMenu, m.IdParent, m.ItemKey, m.Title, m.Icon, m.Url, m.Target, m.DisplayOrder,
            m.ItemKey AS ParentKey, m.IsVisible, m.BadgeText, m.BadgeColor, m.GroupName
        FROM MenuItems m
        WHERE m.IdParent IS NULL

        UNION ALL

        SELECT
            m.IdMenu, m.IdParent, m.ItemKey, m.Title, m.Icon, m.Url, m.Target, m.DisplayOrder,
            c.ItemKey AS ParentKey, m.IsVisible, m.BadgeText, m.BadgeColor, m.GroupName
        FROM MenuItems m
        INNER JOIN MenuCTE c ON m.IdParent = c.IdMenu
    )
    SELECT DISTINCT *
    FROM MenuCTE
    ORDER BY IdParent, DisplayOrder;
END
GO

ALTER PROCEDURE [dbo].[INS_MenuItem]
@IdMenu         UNIQUEIDENTIFIER,
@IdParent       UNIQUEIDENTIFIER = NULL,
@ItemKey        VARCHAR(100),
@Title          NVARCHAR(200),
@Icon           VARCHAR(200) = NULL,
@Url            VARCHAR(300) = NULL,
@Target         VARCHAR(200) = NULL,
@DisplayOrder   INT = 0,
@IsVisible      BIT = 1,
@BadgeText      NVARCHAR(100),
@BadgeColor     NVARCHAR(100),
@GroupName      NVARCHAR(50) = NULL
AS
BEGIN
    INSERT INTO MenuItems (IdMenu, IdParent, ItemKey, Title, Icon, Url, Target, DisplayOrder, IsVisible, BadgeText, BadgeColor, GroupName)
    VALUES (@IdMenu, @IdParent, @ItemKey, @Title, @Icon, @Url, @Target, @DisplayOrder, @IsVisible, @BadgeText, @BadgeColor, @GroupName);
END;
GO

ALTER PROCEDURE [dbo].[UPD_MenuItem]
@IdMenu         UNIQUEIDENTIFIER,
@IdParent       UNIQUEIDENTIFIER = NULL,
@ItemKey        VARCHAR(100),
@Title          NVARCHAR(200),
@Icon           VARCHAR(200) = NULL,
@Url            VARCHAR(300) = NULL,
@Target         VARCHAR(200) = NULL,
@DisplayOrder   INT = 0,
@IsVisible      BIT,
@BadgeText      NVARCHAR(100),
@BadgeColor     NVARCHAR(100),
@UpdatedAt      DATETIME,
@GroupName      NVARCHAR(50) = NULL
AS
BEGIN
    UPDATE  MenuItems
    SET     IdParent = @IdParent,
            ItemKey = @ItemKey,
            Title = @Title,
            Icon = @Icon,
            Url = @Url,
            Target = @Target,
            DisplayOrder = @DisplayOrder,
            IsVisible= @IsVisible,
            BadgeText = @BadgeText,
            BadgeColor = @BadgeColor,
            UpdatedAt = @UpdatedAt,
            GroupName = @GroupName
    WHERE   IdMenu = @IdMenu
END;
GO
