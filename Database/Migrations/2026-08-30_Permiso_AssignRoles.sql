-- =====================================================================================
-- Agrega "assign_roles": a diferencia de manage_roles (que Administrador y SysAdmin
-- comparten desde el principio del barrido de permisos), esta clave es deliberadamente
-- SOLO para SysAdmin — decidido así para que un Administrador no pueda auto-asignarse
-- (ni asignarle a otro usuario) el rol SysAdmin ni ningún otro desde
-- /Settings/UserRoles. Administrador conserva manage_roles para todo lo demás de esta
-- sección (crear roles, editar los permisos de un rol) — este script NO toca su set.
--
-- Mismo patrón ya validado (DEL_RolePermissionsByRole + re-insert vía
-- INS_RolePermissions), aplicado solo a SysAdmin.
-- =====================================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    ------------------------------------------------------------------
    -- 1. Nueva definición de permiso (grupo "settings")
    ------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionKey = 'assign_roles')
    BEGIN
        INSERT INTO Permissions (PermissionId, PermissionKey, Name, Description, [Group])
        VALUES (
            NEWID(),
            'assign_roles',
            'Asignar Roles a Usuarios',
            'Otorgar o quitarle un rol a un usuario sobre un edificio',
            'settings'
        );
    END

    ------------------------------------------------------------------
    -- 2. Set completo de claves para SysAdmin: las 62 que ya tiene hoy (sin tocarlas)
    --    + assign_roles.
    ------------------------------------------------------------------
    DECLARE @ClavesFullAccess TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesFullAccess (PermissionKey)
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
        ('create_expense'), ('edit_expense'), ('manage_workflow'),
        ('assign_roles');

    ------------------------------------------------------------------
    -- 3. Reconstruir el set de SysAdmin únicamente. Administrador NO se toca.
    ------------------------------------------------------------------
    DECLARE @IdSysAdmin UNIQUEIDENTIFIER = 'E6A7FC24-75C2-44CE-88BF-7FC5B2A0EED4';
    DECLARE @pid UNIQUEIDENTIFIER;

    EXEC DEL_RolePermissionsByRole @IdSysAdmin;

    DECLARE permCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT p.PermissionId
        FROM Permissions p
        INNER JOIN @ClavesFullAccess k ON k.PermissionKey = p.PermissionKey;

    OPEN permCursor;
    FETCH NEXT FROM permCursor INTO @pid;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC INS_RolePermissions @IdSysAdmin, @pid;
        FETCH NEXT FROM permCursor INTO @pid;
    END

    CLOSE permCursor;
    DEALLOCATE permCursor;

    COMMIT TRANSACTION;

    PRINT 'OK: assign_roles creado (si no existía) y asignado solo a SysAdmin (63 claves). Administrador queda intacto (62).';

    -- Verificación rápida: SysAdmin debería mostrar 63 filas; Administrador, 62 (sin assign_roles).
    EXEC GET_PermissionsByRole 'Administrador';
    EXEC GET_PermissionsByRole 'SysAdmin';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
