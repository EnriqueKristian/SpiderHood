-- =============================================================================
-- El usuario confirmó con una consulta directa que dbo.Permissions no tiene
-- ninguna fila para 'view_income_expense_report' (permiso nuevo del reporte de
-- Ingresos y Egresos, Docs/Pendientes-Negocio-Reportes.md #1) -- sin esa fila,
-- ningún rol puede tenerlo asignado y la página siempre muestra "No tenés
-- permiso para ver este reporte", sin importar el rol del usuario.
--
-- Mismo problema, mismo motivo, para los otros tres reportes agregados antes en
-- esta misma sesión (Recaudación/Morosidad/Consumo de Agua): cada uno usa su
-- propio PermissionKey (view_budget_execution/view_delinquency/
-- view_consumption_report) leído con IPermissionService.HasPermissionAsync,
-- pero ninguno de los tres se sembró nunca en dbo.Permissions -- no hay ninguna
-- pantalla en la app para CREAR un permiso nuevo (IPermissionAdminService sólo
-- lee/asigna permisos EXISTENTES a un rol, ver GetAllPermissionsAsync/
-- AssignPermissionsToRoleAsync), así que hace falta este INSERT directo.
--
-- IF NOT EXISTS en cada uno: seguro de correr aunque alguno ya se haya agregado
-- a mano mientras tanto. `Group = 'reports'` es el mismo valor que
-- PermissionAdminService.GetModuleDisplayName ya mapea a "Reportes" en la
-- pantalla de Roles y Permisos -- sin esto, el permiso existiría pero no
-- aparecería agrupado con el resto de los reportes en /roles.
--
-- Después de correr esto, falta asignar cada permiso al/los rol(es)
-- correspondientes desde /roles (Configuración > Roles y Permisos) -- ese paso
-- sigue siendo manual, este script sólo crea el catálogo.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'view_budget_execution')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'view_budget_execution', 'Ver Recaudación', 'Ver el reporte de Presupuesto vs. Recaudado por periodo', 'reports');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'view_delinquency')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'view_delinquency', 'Ver Morosidad', 'Ver el reporte de Morosidad por unidad', 'reports');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'view_consumption_report')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'view_consumption_report', 'Ver Consumo de Agua', 'Ver el reporte de Consumo de Agua del edificio', 'reports');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'view_income_expense_report')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'view_income_expense_report', 'Ver Ingresos y Egresos', 'Ver el reporte de Ingresos y Egresos por periodo', 'reports');
