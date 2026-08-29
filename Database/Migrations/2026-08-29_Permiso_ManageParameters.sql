-- =====================================================================================
-- Corrige un error del script de permisos anterior: ParameterPage.razor (Configuración
-- de Parámetros) se gateó asumiendo que "manage_parameters" ya existía como clave en BD
-- (igual que manage_categories, manage_periods, etc.), pero nunca se creó ni se asignó a
-- ningún rol. Resultado: ni Administrador ni SysAdmin veían el botón "Nuevo Parámetro"
-- (fail closed, como corresponde cuando falta la clave).
--
-- Este script crea "manage_parameters" y la asigna a Administrador y SysAdmin — el mismo
-- set que ya tienen ambos hoy (61 claves, tras el script de Gastos/Workflow) más esta
-- nueva. No toca Junta ni Residente.
--
-- Idempotente, mismo patrón ya validado (DEL_RolePermissionsByRole + re-insert vía
-- INS_RolePermissions).
-- =====================================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    ------------------------------------------------------------------
    -- 1. Nueva definición de permiso (grupo "settings")
    ------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM Permissions WHERE PermissionKey = 'manage_parameters')
    BEGIN
        INSERT INTO Permissions (PermissionId, PermissionKey, Name, Description, [Group])
        VALUES (
            NEWID(),
            'manage_parameters',
            'Gestionar Parámetros del Sistema',
            'Crear/editar/eliminar parámetros generales del sistema',
            'settings'
        );
    END

    ------------------------------------------------------------------
    -- 2. Set completo de claves para Administrador / SysAdmin: las 61 que ya tienen
    --    hoy (sin tocarlas) + manage_parameters.
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
        ('create_expense'), ('edit_expense'), ('manage_workflow');

    ------------------------------------------------------------------
    -- 3. Reconstruir el set de Administrador y de SysAdmin.
    ------------------------------------------------------------------
    DECLARE @IdAdministrador UNIQUEIDENTIFIER = '46198F07-F865-49A6-8057-571B867C5D1B';
    DECLARE @IdSysAdmin      UNIQUEIDENTIFIER = 'E6A7FC24-75C2-44CE-88BF-7FC5B2A0EED4';

    DECLARE @IdRoleActual UNIQUEIDENTIFIER;
    DECLARE @pid UNIQUEIDENTIFIER;

    DECLARE roleCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT @IdAdministrador
        UNION ALL
        SELECT @IdSysAdmin;

    OPEN roleCursor;
    FETCH NEXT FROM roleCursor INTO @IdRoleActual;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC DEL_RolePermissionsByRole @IdRoleActual;

        DECLARE permCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT p.PermissionId
            FROM Permissions p
            INNER JOIN @ClavesFullAccess k ON k.PermissionKey = p.PermissionKey;

        OPEN permCursor;
        FETCH NEXT FROM permCursor INTO @pid;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            EXEC INS_RolePermissions @IdRoleActual, @pid;
            FETCH NEXT FROM permCursor INTO @pid;
        END

        CLOSE permCursor;
        DEALLOCATE permCursor;

        FETCH NEXT FROM roleCursor INTO @IdRoleActual;
    END

    CLOSE roleCursor;
    DEALLOCATE roleCursor;

    COMMIT TRANSACTION;

    PRINT 'OK: manage_parameters creado (si no existía) y asignado a Administrador/SysAdmin.';

    -- Verificación rápida: debería mostrar 62 filas por cada rol.
    EXEC GET_PermissionsByRole 'Administrador';
    EXEC GET_PermissionsByRole 'SysAdmin';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
