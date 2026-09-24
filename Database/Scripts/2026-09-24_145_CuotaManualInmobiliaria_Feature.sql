-- =============================================================================
-- Feature "Agregar Cuota Manual": permite crear, contra el BudgetHeader
-- Ordinario ya publicado de un periodo pasado, una cuota puntual para el grupo
-- "Inmobiliaria" (unidades sin vender, ver Database/Scripts/2026-09-15_106_
-- Presupuesto_UnidadesSinPropietario_Foundation.sql) -- caso real: cuotas de
-- meses ya cerrados que nunca se cargaron para la Inmobiliaria.
--
-- No requiere ningún cambio de esquema ni stored procedure nuevo: reutiliza
-- IInstallmentService.AddInstallmentAsync (Data/BDLayout.Add.cs
-- AddNewRecordAsync(Installment), un INSERT genérico vía EF Core -- no pasa
-- por ninguna stored procedure, así que el problema de QUOTED_IDENTIFIER
-- documentado en 2026-09-24_144_CondonarDeuda_Feature.sql no aplica acá.
--
-- El motivo/quién/cuándo quedan en WorkflowAuditEntry (WorkflowAction.
-- ManualCharge), no en la cuota misma -- mismo patrón que Condonar Deuda.
--
-- Permiso nuevo, sin asignar a ningún rol por defecto (mismo patrón que
-- 2026-09-24_144_CondonarDeuda_Feature.sql) -- asignación manual desde /roles.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'agregar_cuota_manual')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'agregar_cuota_manual', 'Agregar Cuota Manual', 'Crear una cuota puntual contra un presupuesto ya publicado de un periodo pasado (regularización histórica de la Inmobiliaria)', 'reconciliation');
GO
