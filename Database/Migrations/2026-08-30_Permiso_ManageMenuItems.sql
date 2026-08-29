-- =====================================================================================
-- Fase 3 del plan de reorganización de Pages: SettingPages/MenuItems.razor (lista/árbol
-- del menú, con botones Nuevo Item / Editar / Eliminar / Guardar Permisos / Guardar
-- Orden) y SettingPages/MenuItemForm.razor (Create/Edit) no tenían NINGÚN gating de
-- permisos — cualquier usuario que llegara a esas rutas podía crear, editar, eliminar y
-- reordenar el menú de navegación de toda la app, y reasignar qué roles ven qué. Se
-- gatean con una clave nueva, "manage_menu_items", en vez de reusar "access_settings"
-- (esa es más genérica, de sólo-acceso a la sección) o "manage_roles" (esa es sobre
-- roles/permisos, no sobre la estructura del menú) — no hay una clave existente que
-- cubra esto (ver CLAUDE.md: revisar antes de inventar una nueva).
--
-- Sin un flujo con distintos actores por paso (sólo Administrador/SysAdmin administran
-- el menú), así que es una sola clave para todo el módulo, igual que manage_roles,
-- manage_users, manage_categories, etc. — se la damos a Administrador Y SysAdmin, mismo
-- criterio que esos módulos (a diferencia de "assign_roles", que quedó deliberadamente
-- sólo para SysAdmin).
--
-- Reconstruye el set completo de cada rol (DEL_RolePermissionsByRole + INS_RolePermissions)
-- en vez de un INSERT directo a RolePermissions, mismo patrón ya validado. Set base de
-- 62 claves tomado de 2026-08-29_Permiso_ManageParameters.sql (el más reciente que
-- reconstruyó Administrador); SysAdmin parte de ese mismo set base + "assign_roles" (ver
-- 2026-08-30_Permiso_AssignRoles.sql). Nunca toca Junta ni Residente.
-- =====================================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    ------------------------------------------------------------------
    -- 1. Nueva definición de permiso (grupo "settings")
    ------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionKey = 'manage_menu_items')
    BEGIN
        INSERT INTO Permissions (PermissionId, PermissionKey, Name, Description, [Group])
        VALUES (
            NEWID(),
            'manage_menu_items',
            'Administrar Menú de Navegación',
            'Crear, editar, eliminar y reordenar los ítems del menú, y asignar qué roles ven cada uno',
            'settings'
        );
    END

    ------------------------------------------------------------------
    -- 2. Set base común (62 claves que Administrador y SysAdmin ya comparten hoy).
    ------------------------------------------------------------------
    DECLARE @ClavesBase TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesBase (PermissionKey)
    VALUES
        ('access_settings'), ('apply_surcharges'), ('assign_units'), ('authorize_expenses'),
        ('board_member'), ('clone_budget'), ('close_budget'), ('create_announcements'),
        ('create_budget'), ('create_building'), ('create_extraordinary_fee'),
        ('create_owner'), ('create_unit'),
        ('delete_budget'), ('delete_building'), ('delete_owner'), ('delete_unit'),
        ('edit_budget'), ('edit_building'), ('edit_incident'), ('edit_owner'), ('edit_unit'),
        ('emit_receipts'), ('export_receipts'), ('manage_budget'),
        ('manage_buildings'), ('manage_categories'), ('manage_minutes'), ('manage_periods'),
        ('manage_parameters'),
        ('manage_roles'), ('manage_security'), ('manage_users'), ('manage_water_readings'),
        ('publish_budget'), ('reconcile_expenses'), ('reconcile_installments'),
        ('resident_portal'), ('resolve_incident'), ('submit_budget'), ('upload_statements'),
        ('view_about'), ('view_announcements'), ('view_budget_execution'),
        ('view_budget_resident'), ('view_budgets'), ('view_buildings'),
        ('view_consumption_report'), ('view_dashboard'), ('view_delinquency'),
        ('view_income_expenses'), ('view_my_consumption'), ('view_my_receipts'),
        ('view_owners'), ('view_profile'), ('view_reconciliation'),
        ('view_reconciliation_history'), ('view_reports'), ('view_unitgroups'),
        ('create_expense'), ('edit_expense'), ('manage_workflow');

    ------------------------------------------------------------------
    -- 3. Set de Administrador: base + manage_menu_items (63 claves).
    ------------------------------------------------------------------
    DECLARE @ClavesAdministrador TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesAdministrador (PermissionKey) SELECT PermissionKey FROM @ClavesBase;
    INSERT INTO @ClavesAdministrador (PermissionKey) VALUES ('manage_menu_items');

    ------------------------------------------------------------------
    -- 4. Set de SysAdmin: base + assign_roles + manage_menu_items (64 claves).
    ------------------------------------------------------------------
    DECLARE @ClavesSysAdmin TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesSysAdmin (PermissionKey) SELECT PermissionKey FROM @ClavesBase;
    INSERT INTO @ClavesSysAdmin (PermissionKey) VALUES ('assign_roles'), ('manage_menu_items');

    ------------------------------------------------------------------
    -- 5. Reconstruir Administrador.
    ------------------------------------------------------------------
    DECLARE @IdAdministrador UNIQUEIDENTIFIER = '46198F07-F865-49A6-8057-571B867C5D1B';
    DECLARE @pid UNIQUEIDENTIFIER;

    EXEC DEL_RolePermissionsByRole @IdAdministrador;

    DECLARE permCursorAdmin CURSOR LOCAL FAST_FORWARD FOR
        SELECT p.PermissionId
        FROM Permissions p
        INNER JOIN @ClavesAdministrador k ON k.PermissionKey = p.PermissionKey;

    OPEN permCursorAdmin;
    FETCH NEXT FROM permCursorAdmin INTO @pid;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC INS_RolePermissions @IdAdministrador, @pid;
        FETCH NEXT FROM permCursorAdmin INTO @pid;
    END
    CLOSE permCursorAdmin;
    DEALLOCATE permCursorAdmin;

    ------------------------------------------------------------------
    -- 6. Reconstruir SysAdmin.
    ------------------------------------------------------------------
    DECLARE @IdSysAdmin UNIQUEIDENTIFIER = 'E6A7FC24-75C2-44CE-88BF-7FC5B2A0EED4';

    EXEC DEL_RolePermissionsByRole @IdSysAdmin;

    DECLARE permCursorSysAdmin CURSOR LOCAL FAST_FORWARD FOR
        SELECT p.PermissionId
        FROM Permissions p
        INNER JOIN @ClavesSysAdmin k ON k.PermissionKey = p.PermissionKey;

    OPEN permCursorSysAdmin;
    FETCH NEXT FROM permCursorSysAdmin INTO @pid;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC INS_RolePermissions @IdSysAdmin, @pid;
        FETCH NEXT FROM permCursorSysAdmin INTO @pid;
    END
    CLOSE permCursorSysAdmin;
    DEALLOCATE permCursorSysAdmin;

    COMMIT TRANSACTION;

    PRINT 'OK: manage_menu_items creado (si no existía) y asignado a Administrador (63) y SysAdmin (64).';

    -- Verificación rápida.
    EXEC GET_PermissionsByRole 'Administrador';
    EXEC GET_PermissionsByRole 'SysAdmin';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
