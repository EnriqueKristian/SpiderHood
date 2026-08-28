-- =====================================================================================
-- Agrega los permisos granulares que faltan para Propietarios y Unidades (Edificios ya
-- tenía create_building/edit_building/delete_building/manage_buildings/assign_units
-- desde antes, y sólo se reutilizan). Los asigna a Administrador y SysAdmin — el mismo
-- set que ya tienen ambos hoy (51 claves, tras el script de Presupuesto) más las 6
-- nuevas.
--
-- No toca Junta ni Residente — no gestionan propietarios ni unidades hoy.
--
-- Idempotente: se puede correr más de una vez sin duplicar nada. Mismo patrón que
-- 2026-08-28_Permisos_Presupuesto_CuotasExtraordinarias.sql (que ya se corrió y
-- confirmó funcionando: DEL_RolePermissionsByRole + re-insert vía INS_RolePermissions,
-- para no depender de los nombres reales de columna de RolePermissions).
-- =====================================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    ------------------------------------------------------------------
    -- 1. Nuevas definiciones de permiso (grupo "building")
    ------------------------------------------------------------------
    DECLARE @NuevosPermisos TABLE (
        PermissionId   UNIQUEIDENTIFIER,
        PermissionKey  NVARCHAR(200),
        Name           NVARCHAR(200),
        Description    NVARCHAR(400),
        [Group]        NVARCHAR(100)
    );

    INSERT INTO @NuevosPermisos (PermissionId, PermissionKey, Name, Description, [Group])
    VALUES
        (NEWID(), 'create_owner', 'Crear Propietario',  'Registrar un nuevo propietario/residente', 'building'),
        (NEWID(), 'edit_owner',   'Editar Propietario',  'Editar los datos de un propietario/residente', 'building'),
        (NEWID(), 'delete_owner', 'Eliminar Propietario', 'Eliminar un propietario/residente', 'building'),
        (NEWID(), 'create_unit',  'Crear Unidad',        'Registrar una nueva unidad (individual o en bloque)', 'building'),
        (NEWID(), 'edit_unit',    'Editar Unidad',       'Editar los datos de una unidad', 'building'),
        (NEWID(), 'delete_unit',  'Eliminar Unidad',     'Eliminar una unidad', 'building');

    INSERT INTO Permissions (PermissionId, PermissionKey, Name, Description, [Group])
    SELECT n.PermissionId, n.PermissionKey, n.Name, n.Description, n.[Group]
    FROM @NuevosPermisos n
    WHERE NOT EXISTS (
        SELECT 1 FROM Permissions p WHERE p.PermissionKey = n.PermissionKey
    );

    ------------------------------------------------------------------
    -- 2. Set completo de claves para Administrador / SysAdmin: las 51 que ya tienen
    --    hoy (sin tocarlas) + las 6 nuevas de arriba.
    ------------------------------------------------------------------
    DECLARE @ClavesFullAccess TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesFullAccess (PermissionKey)
    VALUES
        ('access_settings'), ('apply_surcharges'), ('assign_units'), ('authorize_expenses'),
        ('board_member'), ('clone_budget'), ('close_budget'), ('create_announcements'),
        ('create_budget'), ('create_building'), ('create_extraordinary_fee'),
        ('delete_budget'), ('delete_building'), ('edit_budget'), ('edit_building'),
        ('edit_incident'), ('emit_receipts'), ('export_receipts'), ('manage_budget'),
        ('manage_buildings'), ('manage_categories'), ('manage_minutes'), ('manage_periods'),
        ('manage_security'), ('manage_users'), ('manage_water_readings'), ('publish_budget'),
        ('reconcile_expenses'), ('reconcile_installments'), ('resident_portal'),
        ('resolve_incident'), ('submit_budget'), ('upload_statements'), ('view_about'),
        ('view_announcements'), ('view_budget_execution'), ('view_budget_resident'),
        ('view_budgets'), ('view_buildings'), ('view_consumption_report'), ('view_dashboard'),
        ('view_delinquency'), ('view_income_expenses'), ('view_my_consumption'),
        ('view_my_receipts'), ('view_owners'), ('view_profile'), ('view_reconciliation'),
        ('view_reconciliation_history'), ('view_reports'), ('view_unitgroups'),
        -- nuevas de este script
        ('create_owner'), ('edit_owner'), ('delete_owner'),
        ('create_unit'), ('edit_unit'), ('delete_unit');

    ------------------------------------------------------------------
    -- 3. Reconstruir el set de Administrador y de SysAdmin (DEL + re-INS vía los
    --    procs existentes).
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

    PRINT 'OK: 6 permisos nuevos creados (si no existían) y asignados a Administrador/SysAdmin.';

    -- Verificación rápida: debería mostrar 57 filas por cada rol.
    EXEC GET_PermissionsByRole 'Administrador';
    EXEC GET_PermissionsByRole 'SysAdmin';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
