-- =============================================================================
-- Permiso "edit_account" (Docs/Pendientes-Negocio-Consolidado.md #30, punto
-- b) -- hasta ahora la sección "Datos de Facturación" (Settings.razor) sólo
-- chequeaba currentUser.Role is "Administrador" or "SysAdmin" a mano, sin
-- forma de que el SysAdmin le diera ese permiso a otro rol (ej. un
-- Colaborador con un rol distinto, o Junta) sin también volverlo
-- Administrador global.
--
-- Mismo patrón que 2026-09-09_76_Seed_ReportPermissions.sql/
-- 2026-09-11_92_Seed_ApproveExpensesPermission.sql: sólo crea el catálogo,
-- IF NOT EXISTS. Administrador y SysAdmin lo siguen teniendo por el chequeo
-- de rol existente en el código (Settings.razor OR-ea rol-o-permiso, no se
-- les saca nada) -- este permiso es sólo para EXTENDER el acceso a otros
-- roles, no para restringir a los que ya lo tenían. Asignarlo a un rol
-- adicional sigue siendo manual desde /roles (Configuración > Roles y
-- Permisos).
--
-- No se agregan "view_account"/"export_account" todavía: hoy nada gatea la
-- lectura (se sigue mostrando a cualquiera que llegue a /Settings, sin
-- cambios) ni existe ninguna función de exportación de Account -- agregar
-- esas claves ahora sería un permiso sin nada del otro lado que lo
-- verifique. Se agregan cuando haya una razón de negocio concreta para
-- restringir lectura o cuando exista el export.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'edit_account')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'edit_account', 'Editar Datos de Facturación', 'Editar Razón Social/Nombre, RUC/DNI, Teléfono y demás datos de la Cuenta de Facturación en /Settings', 'settings');
