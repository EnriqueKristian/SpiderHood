-- =============================================================================
-- Falta el ítem de menú para la pantalla de mantenimiento de Account (Docs/
-- Pendientes-Negocio-Consolidado.md #30, punto a) -- la página existe hace
-- rato (/Settings) y es alcanzable desde el dropdown del usuario en el header
-- ("Mi Suscripción", HeaderMainLayout.razor, gateado a rol Administrador),
-- pero nunca tuvo una fila en MenuItems -- así que no aparece en el menú
-- izquierdo "Configuración" junto con Mi Perfil/Seguridad/Usuarios/etc., que sí
-- son ítems reales de esa tabla. Ahora que /Settings también tiene "Datos de
-- Facturación" + "Logo de la Empresa" (agregados en esta misma sesión), tiene
-- más sentido que aparezca ahí -- no sólo escondido en el dropdown del avatar.
--
-- No se toca el link del header (sigue funcionando igual, es otro camino a la
-- misma página) -- esto sólo agrega el segundo camino, desde el menú
-- izquierdo. Mismo patrón que 2026-09-16_131_Seed_ViewBudget_MenuPermissions.sql:
-- crea el ítem + le asigna los roles que ya podían llegar a /Settings antes
-- (Administrador, SysAdmin -- mismo gate que HeaderMainLayout.razor).
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

-- EXEC con parámetros nombrados no admite una subquery como valor directo
-- (mismo motivo documentado en 2026-09-02_09_Seed_IncidentWorkflowCatalog.sql)
-- -- se resuelve primero en una variable.
DECLARE @IdParentSettings UNIQUEIDENTIFIER = (SELECT IdMenu FROM dbo.MenuItems WHERE ItemKey = 'settings');

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE ItemKey = 'account_billing')
EXEC dbo.INS_MenuItem
    @IdMenu = '3F7A9C2E-5B1D-4E8A-9C3F-7D2B8A4E6F19',
    @IdParent = @IdParentSettings,
    @ItemKey = 'account_billing',
    @Title = 'Cuenta y Facturación',
    @Icon = 'bi-credit-card',
    @Url = 'Settings',
    @Target = NULL,
    @DisplayOrder = 15,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO

DECLARE @IdMenuAccountBilling UNIQUEIDENTIFIER = (SELECT IdMenu FROM dbo.MenuItems WHERE ItemKey = 'account_billing');
DECLARE @IdRoleAdministrador UNIQUEIDENTIFIER = (SELECT IdRole FROM dbo.Roles WHERE RoleName = 'Administrador');
DECLARE @IdRoleSysAdmin UNIQUEIDENTIFIER = (SELECT IdRole FROM dbo.Roles WHERE RoleName = 'SysAdmin');

IF @IdMenuAccountBilling IS NOT NULL AND @IdRoleAdministrador IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.MenuPermissions WHERE IdMenu = @IdMenuAccountBilling AND IdRole = @IdRoleAdministrador)
    INSERT INTO dbo.MenuPermissions (IdMenu, IdRole) VALUES (@IdMenuAccountBilling, @IdRoleAdministrador);

IF @IdMenuAccountBilling IS NOT NULL AND @IdRoleSysAdmin IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.MenuPermissions WHERE IdMenu = @IdMenuAccountBilling AND IdRole = @IdRoleSysAdmin)
    INSERT INTO dbo.MenuPermissions (IdMenu, IdRole) VALUES (@IdMenuAccountBilling, @IdRoleSysAdmin);
GO
