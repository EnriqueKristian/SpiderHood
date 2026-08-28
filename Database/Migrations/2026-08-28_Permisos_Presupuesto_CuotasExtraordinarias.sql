-- =====================================================================================
-- Agrega los permisos granulares que faltan para el módulo de Presupuesto / Cuotas
-- Extraordinarias / Multas y Mora, y los asigna a Administrador y SysAdmin (mismo set
-- que ya tienen ambos hoy, ver EXEC GET_PermissionsByRole 'Administrador'/'SysAdmin').
--
-- No toca Junta ni Residente — sus permisos actuales (approve_budget para Junta,
-- solo lectura para Residente) ya coinciden con lo que necesitan y no cambian.
--
-- Idempotente: se puede correr más de una vez sin duplicar nada. Los permisos NUEVOS
-- se insertan solo si no existen (por PermissionKey); el set final de Administrador y
-- SysAdmin se reconstruye por completo (DEL_RolePermissionsByRole + re-insert) para
-- garantizar que quede exactamente como se espera sin importar cuántas veces se corra.
-- =====================================================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

    ------------------------------------------------------------------
    -- 1. Nuevas definiciones de permiso (grupo "budget")
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
        (NEWID(), 'delete_budget',             'Eliminar Presupuesto',           'Eliminar un presupuesto en borrador/revisión', 'budget'),
        (NEWID(), 'clone_budget',               'Clonar Presupuesto',             'Clonar un presupuesto existente como punto de partida', 'budget'),
        (NEWID(), 'submit_budget',              'Enviar Presupuesto a Aprobación', 'Enviar un presupuesto en borrador a revisión de la Junta', 'budget'),
        (NEWID(), 'publish_budget',             'Publicar Presupuesto',           'Publicar (activar) un presupuesto ya aprobado', 'budget'),
        (NEWID(), 'close_budget',               'Cerrar Presupuesto',             'Cerrar un presupuesto Activo', 'budget'),
        (NEWID(), 'create_extraordinary_fee',   'Generar Cuota Extraordinaria',   'Crear una cuota extraordinaria (fondo de obras, cuotas especiales, etc.)', 'budget'),
        (NEWID(), 'apply_surcharges',           'Aplicar Multas y Mora',          'Aplicar multas e intereses moratorios sobre cuotas vencidas', 'budget');

    INSERT INTO Permissions (PermissionId, PermissionKey, Name, Description, [Group])
    SELECT n.PermissionId, n.PermissionKey, n.Name, n.Description, n.[Group]
    FROM @NuevosPermisos n
    WHERE NOT EXISTS (
        SELECT 1 FROM Permissions p WHERE p.PermissionKey = n.PermissionKey
    );

    ------------------------------------------------------------------
    -- 2. Set completo de claves para Administrador / SysAdmin: las 44 que ya tienen
    --    hoy (sin tocarlas) + las 7 nuevas de arriba.
    ------------------------------------------------------------------
    DECLARE @ClavesFullAccess TABLE (PermissionKey NVARCHAR(200));
    INSERT INTO @ClavesFullAccess (PermissionKey)
    VALUES
        ('access_settings'), ('assign_units'), ('authorize_expenses'), ('board_member'),
        ('create_announcements'), ('create_budget'), ('create_building'), ('delete_building'),
        ('edit_budget'), ('edit_building'), ('edit_incident'), ('emit_receipts'),
        ('export_receipts'), ('manage_budget'), ('manage_buildings'), ('manage_categories'),
        ('manage_minutes'), ('manage_periods'), ('manage_security'), ('manage_users'),
        ('manage_water_readings'), ('reconcile_expenses'), ('reconcile_installments'),
        ('resident_portal'), ('resolve_incident'), ('upload_statements'), ('view_about'),
        ('view_announcements'), ('view_budget_execution'), ('view_budget_resident'),
        ('view_budgets'), ('view_buildings'), ('view_consumption_report'), ('view_dashboard'),
        ('view_delinquency'), ('view_income_expenses'), ('view_my_consumption'),
        ('view_my_receipts'), ('view_owners'), ('view_profile'), ('view_reconciliation'),
        ('view_reconciliation_history'), ('view_reports'), ('view_unitgroups'),
        -- nuevas de este script
        ('delete_budget'), ('clone_budget'), ('submit_budget'), ('publish_budget'),
        ('close_budget'), ('create_extraordinary_fee'), ('apply_surcharges');

    ------------------------------------------------------------------
    -- 3. Reconstruir el set de Administrador y de SysAdmin (DEL + re-INS vía los
    --    procs existentes, para no depender de los nombres reales de columna de
    --    RolePermissions — que ya sabemos difieren del modelo C#, ver RoleId vs
    --    IdRole en DEL_RolePermissionsByRole).
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

    PRINT 'OK: 7 permisos nuevos creados (si no existían) y asignados a Administrador/SysAdmin.';

    -- Verificación rápida: debería mostrar 51 filas por cada rol.
    EXEC GET_PermissionsByRole 'Administrador';
    EXEC GET_PermissionsByRole 'SysAdmin';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    THROW;
END CATCH
