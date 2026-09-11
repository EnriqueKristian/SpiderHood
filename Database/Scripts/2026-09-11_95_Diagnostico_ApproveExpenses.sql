-- Sólo LECTURA. Para saber por qué Junta no ve la sección "Gastos Pendientes de
-- Aprobación" en /aprobaciones: ¿existe el permiso approve_expenses? (no adivino
-- el nombre real de la tabla de roles/permisos por rol para chequear la
-- asignación -- eso se confirma más fácil y sin riesgo mirando directamente
-- /Settings/Roles/Permissions en la app: entrá como SysAdmin, elegí el rol
-- Junta, buscá "Aprobar Gastos" y fijate si el checkbox está marcado).
SELECT PermissionId, PermissionKey, Name, [Group]
FROM dbo.Permissions
WHERE PermissionKey = 'approve_expenses';
