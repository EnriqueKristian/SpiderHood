-- =============================================================================
-- Nuevo permiso "approve_expenses" (Fase 2 de Aprobaciones): aprobar/rechazar un
-- gasto manual que superó el monto configurado en BuildingConfiguration.
-- ExpenseApprovalThreshold. Aparte de "approve_budget" a propósito -- un edificio
-- podría querer que la Junta apruebe presupuestos pero no gastos puntuales (o
-- viceversa), mismo criterio que ya separa manage_users de manage_roles.
--
-- IF NOT EXISTS: seguro de correr aunque ya exista. Después de esto falta
-- asignarlo a los roles correspondientes desde /roles (Configuración > Roles y
-- Permisos) -- típicamente Junta, Administrador y SysAdmin -- ese paso sigue
-- siendo manual, este script sólo crea el catálogo (mismo patrón que
-- 2026-09-09_76_Seed_ReportPermissions.sql).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'approve_expenses')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'approve_expenses', 'Aprobar Gastos', 'Aprobar o rechazar un gasto que superó el monto configurado para el edificio', 'budget');
