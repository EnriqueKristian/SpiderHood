-- =============================================================================
-- Docs/Design-Piloto-Mobile-Android.md, Fase 1 -- el ítem de menú "Ver
-- presupuesto" (ItemKey 'viewbudget', /view-budget, grupo "resident" del menú)
-- no tenía NINGÚN rol asignado en MenuPermissions -- ni siquiera Residente,
-- aunque el resto de los ítems del mismo grupo (Mis recibos, Mis Pagos,
-- Comunicados, Reportar incidencia, Mi consumo de agua) sí lo tienen para
-- SysAdmin + Residente. Parece un olvido al agregar la pantalla, no una
-- decisión -- la página (ResidentPages/ViewBudget.razor) ya funciona si se
-- navega directo a la URL, simplemente nunca apareció en el menú de nadie.
--
-- Se agrega para Residente (mismo patrón que sus ítems hermanos) y para Junta
-- (pedido del usuario para el piloto móvil -- "solo lectura primero", mismo
-- camino que Residente en vez de la pantalla de administración /budgetlist).
-- Costo de agregarlo es mínimo y reversible: si se evalúa que Junta no debería
-- tenerlo, alcanza con borrar la fila de MenuPermissions correspondiente.
--
-- Idempotente: no inserta si la fila ya existe.
-- =============================================================================

SET NOCOUNT ON;
GO

DECLARE @IdMenuViewBudget UNIQUEIDENTIFIER = (SELECT IdMenu FROM MenuItems WHERE ItemKey = 'viewbudget');
DECLARE @IdRoleResidente UNIQUEIDENTIFIER = (SELECT IdRole FROM roles WHERE RoleName = 'Residente');
DECLARE @IdRoleJunta UNIQUEIDENTIFIER = (SELECT IdRole FROM roles WHERE RoleName = 'Junta');

IF @IdMenuViewBudget IS NOT NULL AND @IdRoleResidente IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM MenuPermissions WHERE IdMenu = @IdMenuViewBudget AND IdRole = @IdRoleResidente)
    INSERT INTO MenuPermissions (IdMenu, IdRole) VALUES (@IdMenuViewBudget, @IdRoleResidente);

IF @IdMenuViewBudget IS NOT NULL AND @IdRoleJunta IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM MenuPermissions WHERE IdMenu = @IdMenuViewBudget AND IdRole = @IdRoleJunta)
    INSERT INTO MenuPermissions (IdMenu, IdRole) VALUES (@IdMenuViewBudget, @IdRoleJunta);
GO
