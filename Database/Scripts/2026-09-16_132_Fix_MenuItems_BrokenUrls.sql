-- =============================================================================
-- Verificación pedida por el usuario del export MenuItems.xlsx: 7 ítems del
-- menú apuntaban a una página que no existe en el código (ni la ruta ni el
-- archivo .razor aparecen en ningún lado del repo), y 1 apuntaba a una página
-- real pero equivocada para lo que dice el título.
--
-- De esos 8, 6 tienen un destino correcto identificable con evidencia clara
-- (otro ítem hermano ya usa la misma funcionalidad, o el propio código
-- confirma dónde vive la funcionalidad real) -- esos 6 se corrigen acá.
--
-- Los otros 2 (ver el comentario al final) NO se tocan: no encontré ninguna
-- funcionalidad ya construida que les corresponda, así que "el link correcto"
-- no existe todavía en el código -- corregir el Url ahí sería inventar un
-- destino, no corregir uno real. Quedan documentados para que el usuario
-- decida (¿se construye la página? ¿se apunta a otra existente? ¿se borra
-- el ítem del menú?).
--
-- Idempotente: sólo actualiza si el Url todavía es el roto original (así no
-- pisa un cambio manual posterior si este script se corre más de una vez).
-- =============================================================================

SET NOCOUNT ON;
GO

-- 1. "Autorizar gastos" (Junta) -- Url roto: "AuthorizeExpenses" (no existe
--    ninguna página/ruta con ese nombre). ApprovalsPages/Approvals.razor
--    (ruta /approvals) ya maneja aprobación de gastos (_canApproveExpenses,
--    botón "Aprobar" sobre gastos pendientes) -- mismo destino que su
--    hermano "Aprobaciones" (boardapprovebudget) en el mismo grupo.
UPDATE MenuItems SET Url = 'approvals'
WHERE IdMenu = 'D3C91599-FF24-4E5E-AEED-0C7904E3AC6E' AND Url = 'AuthorizeExpenses';

-- 2. "Reportes financieros" (Junta) -- Url roto: "FinancialReports" (no
--    existe). Docs/Pendientes-Negocio-Reportes.md confirma que
--    ReportPages/IncomeExpenseReport.razor (ruta /reports/IncomeExpenses,
--    ya usada por el ítem "Ingresos y egresos" del grupo Reportes) es EL
--    reporte financiero de la app.
UPDATE MenuItems SET Url = 'reports/IncomeExpenses'
WHERE IdMenu = 'D61790BD-72F8-43B1-98B2-ABD87C8DBAB1' AND Url = 'FinancialReports';

-- 3. "Emitir recibos PDF" (Presupuesto) -- Url roto: "EmitReceipts" (no
--    existe). La generación de recibos en PDF/ZIP (GenerateAllReceiptsZip /
--    GetOrGenerateAllReceiptsZipAsync) vive exclusivamente en
--    BudgetPages/BudgetGenerator.razor (ruta /budgetgenerator/).
UPDATE MenuItems SET Url = 'budgetgenerator'
WHERE IdMenu = 'A926ADEB-B71B-4CD7-A408-B2DD4ED111E1' AND Url = 'EmitReceipts';

-- 4. "Reportar incidencia" (Portal del Residente) -- Url roto:
--    "ReportIncident" (no existe). IncidentPages/IncidentList.razor (ruta
--    /incidents, mismo destino que "Incidencias" del grupo admin) ya tiene
--    el flujo "si quien reporta es Residente, se asocia automáticamente a
--    SU unidad" -- es una página consciente del rol, no exclusiva de admin.
UPDATE MenuItems SET Url = 'incidents'
WHERE IdMenu = '6FF2FA10-7E13-4349-82E3-C154D00053D8' AND Url = 'ReportIncident';

-- 5. "Crear comunicado" (Incidencias y Comun.) -- Url apuntaba a "calendar":
--    esa ruta SÍ existe, pero CalendarPage.razor sólo soporta crear ítems de
--    tipo Mantenimiento o Evento -- no tiene ningún tipo "Comunicado/Anuncio".
--    CommunicationPages/Announcements.razor (ruta /announcements, mismo
--    destino que su hermano "Ver comunicados") sí tiene el botón "Nuevo
--    Comunicado" que abre el modal de creación real.
UPDATE MenuItems SET Url = 'announcements'
WHERE IdMenu = '2166F900-1896-43A7-96C5-E23046792060' AND Url = 'calendar';

-- 6. "Asignar unidades" (Adm. Edificio) -- Url roto: "AssignUnits" (la ruta
--    real es /AssignUnits/{IdGroupUnit:guid} -- exige un Id de grupo de
--    unidades que un ítem de menú fijo no puede proveer). Owners.razor es
--    la ÚNICA pantalla del código que navega a AssignUnits
--    (GoToAssignUnits, botón por fila), así que es el único punto de entrada
--    real. Se apunta ahí -- mismo destino que su hermano "Residentes"
--    (owners), lo cual es esperable: "asignar unidades" es una acción QUE SE
--    HACE DESDE la pantalla de Residentes, no una pantalla propia.
UPDATE MenuItems SET Url = 'Owners'
WHERE IdMenu = 'C2029674-05FE-4310-B9F9-9002603F7F2C' AND Url = 'AssignUnits';
GO

-- =============================================================================
-- NO corregidos -- sin una funcionalidad ya construida a la que apuntarlos,
-- así que no hay "link correcto" que poner sin inventar un destino. Deja
-- documentado para que el usuario decida:
--
-- * "Exportar recibos" (IdMenu 947E6BC2-72B0-4738-9683-12F307D9C289, grupo
--   Reportes) -- Url roto: "ExportReceipts". No encontré ninguna página que
--   exporte recibos como reporte (distinto de "Emitir recibos PDF", que sí
--   se corrigió arriba y vive en /budgetgenerator/). Puede ser un ítem
--   planeado pero nunca implementado.
--
-- * "Acerca de" (IdMenu E52FB66E-C164-4B20-9154-A7FFEE264AC6, grupo
--   Configuración) -- Url roto: "About". No existe ninguna página "About"/
--   "Acerca de" en el código.
-- =============================================================================
