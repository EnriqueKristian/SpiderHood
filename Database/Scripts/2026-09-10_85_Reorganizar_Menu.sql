-- =============================================================================
-- Reorganización del menú, acordada con el usuario (sesión 2026-09-10):
--
-- 1) "Presupuesto" tenía 10 hijos mezclando generación de presupuesto con
--    conciliación bancaria -- se separa en dos grupos:
--    - Presupuesto (queda): Presupuestos, Listado de Cuotas, Cuotas
--      Extraordinarias, Multas y Moras, Lecturas de Agua, Emitir Recibos PDF.
--    - Conciliación (nuevo grupo): Estado de Cuenta, Conciliación (vista
--      combinada -- se deja OCULTA a propósito, "cuando se acostumbren a
--      ver cómo funciona la conciliación la habilitaremos"), Conciliar
--      Gastos, Conciliar Pagos, Gastos.
-- 2) DisplayOrder duplicado bajo "budget" (3 hijos con orden 3), "resident"
--    (2 hijos con orden 2) y "settings" (2 hijos con orden 9) -- SQL no
--    garantiza el orden entre filas empatadas, así que el menú podía
--    "temblar" entre cargas. Se renumera todo sin empates.
-- 3) "Migración de Datos" vivía como ítem raíz suelto (única excepción sin
--    agrupar junto con "Calendario") -- pasa a ser hijo de "Configuración".
-- 4) 7 ítems (no 6, se sumó "Mis Pagos" al revisar de nuevo) todavía tenían
--    el ícono placeholder "bi bi-house" sin personalizar -- se les asigna
--    uno propio.
--
-- Se usan los mismos SPs que ya usa la pantalla /Settings/MenuItems
-- (INS_MenuItem/UPD_MenuItem) en vez de tocar la tabla directo -- mismo
-- criterio de siempre, evita tener que confirmar el nombre real de la tabla.
-- Los parámetros van posicionales, en el mismo orden exacto que ya usa
-- BDLayout.Add.cs/Update.cs: (IdMenu, IdParent, ItemKey, Title, Icon, Url,
-- Target, DisplayOrder, IsVisible, BadgeText, BadgeColor[, UpdatedAt]).
--
-- Idempotente en la parte de datos (todo son UPDATE por IdMenu fijo), EXCEPTO
-- el INSERT del grupo nuevo -- no correr este script dos veces sin revisar,
-- o va a crear un segundo grupo "Conciliación" duplicado.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @IdConciliacion UNIQUEIDENTIFIER = NEWID();
DECLARE @IdSettings UNIQUEIDENTIFIER = '6ACF696A-49F0-445C-8CFA-15F65D5C1A81';

-- =============================================================================
-- 1) Nuevo grupo raíz "Conciliación" -- orden 4, empuja Portal del Residente/
--    Junta/Incidencias/Reportes/Configuración un lugar hacia abajo (ver
--    sección 3 más abajo).
-- =============================================================================
EXEC INS_MenuItem
    @IdConciliacion, NULL, 'reconciliation_menu', 'Conciliación', 'bi-bank',
    NULL, 'menu-conciliacion', 4, 1, NULL, NULL;

-- =============================================================================
-- 2) Los 5 ítems que se mudan de "Presupuesto" a "Conciliación" -- mismo
--    IdMenu/ItemKey/Title/Url/BadgeText/BadgeColor que ya tenían, sólo cambia
--    IdParent + DisplayOrder (y, en dos casos, Icon/IsVisible).
-- =============================================================================

-- Estado de Cuenta -- primer paso del flujo (cargar el excel del banco)
EXEC UPD_MenuItem
    '4AA2004A-4CE2-4300-8FE2-325F8D748E0A', @IdConciliacion, 'uploadstatement',
    'Estado de cuenta', 'bi-file-earmark-arrow-down', 'movement', NULL,
    1, 1, NULL, NULL, SYSDATETIME();

-- Conciliación (vista combinada) -- IsVisible = 0 a propósito, pedido
-- explícito del usuario: "estará por ahora no visible, y cuando se
-- acostumbren a ver cómo funciona la conciliación la habilitaremos".
EXEC UPD_MenuItem
    'BDD2A071-85A1-4786-A963-1AD08B56EAC8', @IdConciliacion, 'reconcileexpenses',
    'Conciliación', 'bi-receipt', 'ReconcileExpenses', NULL,
    2, 0, NULL, 'danger', SYSDATETIME();

-- Conciliar Gastos
EXEC UPD_MenuItem
    '940F1D20-124A-43CA-9989-A65BC97C11AB', @IdConciliacion, 'reconciliationhistory',
    'Conciliar Gastos', 'bi-clock-history', 'ConciliacionGastos', NULL,
    3, 1, 'Conciliacion de Gastos', 'success', SYSDATETIME();

-- Conciliar Pagos -- de paso, ícono propio (tenía el placeholder bi-house)
EXEC UPD_MenuItem
    '8B4B6754-7E63-4AA0-B97D-E331AD915353', @IdConciliacion, NULL,
    'Conciliar Pagos', 'bi-arrow-down-circle', 'ConciliacionPagos', NULL,
    4, 1, 'Conciliacion de Pagos', 'info', SYSDATETIME();

-- Gastos -- de paso, ícono propio (mismo bi-cash-coin que ya usa la tarjeta
-- "Total Gastos" en /expense, para que sea reconocible)
EXEC UPD_MenuItem
    '37D1F503-2285-4685-AFAB-A0FC299F2941', @IdConciliacion, NULL,
    'Gastos', 'bi-cash-coin', 'expense', NULL,
    5, 1, 'Gastos', 'danger', SYSDATETIME();

-- =============================================================================
-- 3) Los 6 ítems que quedan en "Presupuesto" -- renumerados 1-6 sin huecos
--    ni empates (Presupuestos ya estaba en 1, se deja igual). Sólo cambia
--    DisplayOrder (y, en un caso, Icon) -- IdParent sigue siendo "budget".
-- =============================================================================

-- Listado de Cuotas: 3 -> 2
EXEC UPD_MenuItem
    '25B61CEB-29AA-4179-8785-EF27C69B7FDC', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    'reconcileinstallments', 'Listado de Cuotas', 'bi-wallet2', 'installments-list',
    NULL, 2, 1, NULL, 'danger', SYSDATETIME();

-- Cuotas Extraordinarias: 8 -> 3, de paso ícono propio
EXEC UPD_MenuItem
    '6DBF1BAA-0465-41FE-84AB-09102679C8C4', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    NULL, 'Cuotas Extraordinarias', 'bi-plus-circle', 'cuotaextraordinaria',
    NULL, 3, 1, 'Cuotas Extraordinarias', 'danger', SYSDATETIME();

-- Multas y Moras: 9 -> 4
EXEC UPD_MenuItem
    'D12E3B2B-3305-4C69-A787-FC286569F6F8', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    'reconciliation', 'Multas y Moras', 'bi-arrow-left-right', 'multasymora',
    NULL, 4, 1, 'Multas y Moras', 'danger', SYSDATETIME();

-- Lecturas de agua: 4 -> 5
EXEC UPD_MenuItem
    '16FEAD5E-5CA9-47A8-8800-FABFCF13C1F4', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    'waterreadings', 'Lecturas de agua', 'bi-droplet', 'WaterReadings',
    NULL, 5, 1, NULL, 'danger', SYSDATETIME();

-- Emitir recibos PDF: 7 -> 6
EXEC UPD_MenuItem
    'A926ADEB-B71B-4CD7-A408-B2DD4ED111E1', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    'emitreceipts', 'Emitir recibos PDF', 'bi-file-earmark-pdf', 'EmitReceipts',
    NULL, 6, 1, NULL, 'danger', SYSDATETIME();

-- =============================================================================
-- 4) Corre el resto de los ítems raíz un lugar hacia abajo (4->5, 5->6, etc.)
--    para hacerle lugar a "Conciliación" en la posición 4. Sólo cambia
--    DisplayOrder -- todo lo demás igual.
-- =============================================================================

-- Portal del Residente: 4 -> 5
EXEC UPD_MenuItem
    'C30303F7-DF5D-4526-976E-85C0881A1C79', NULL, 'resident', 'Portal del Residente',
    'bi-house-door', NULL, 'menu-residente', 5, 1, NULL, NULL, SYSDATETIME();

-- Junta de Propietarios: 5 -> 6
EXEC UPD_MenuItem
    '467674F2-BD3B-41C7-A4FB-B13B5152B914', NULL, 'board', 'Junta de Propietarios',
    'bi-person-badge', NULL, 'menu-junta', 6, 1, NULL, NULL, SYSDATETIME();

-- Incidencias y Comun.: 6 -> 7
EXEC UPD_MenuItem
    '0DAA1EB5-7D1F-475C-B240-1DA0B57F0644', NULL, 'incidents', 'Incidencias y Comun.',
    'bi-chat-dots', NULL, 'menu-incidencias', 7, 1, NULL, NULL, SYSDATETIME();

-- Reportes: 7 -> 8
EXEC UPD_MenuItem
    '34FD5E0D-CA9B-4193-AA4D-D046C95E4D92', NULL, 'reports', 'Reportes',
    'bi-pie-chart', NULL, 'menu-reportes', 8, 1, NULL, NULL, SYSDATETIME();

-- Configuración: 8 -> 9
EXEC UPD_MenuItem
    @IdSettings, NULL, 'settings', 'Configuración',
    'bi-gear', NULL, 'menu-settings', 9, 1, NULL, NULL, SYSDATETIME();

-- =============================================================================
-- 5) "Migración de Datos" pasa de ítem raíz suelto a hijo de "Configuración"
--    (orden 14, después de "Edición de Permisos"). No se toca Url -- el link
--    en sí no depende de dónde cuelga en el árbol.
-- =============================================================================
EXEC UPD_MenuItem
    'F3A6C1D8-2E7B-4C5A-9F0D-6B8E4A2C1F73', @IdSettings, 'migration_templates',
    'Migración de Datos', 'bi bi-file-earmark-arrow-down', '/migracion/plantillas',
    NULL, 14, 1, NULL, NULL, SYSDATETIME();

-- =============================================================================
-- 6) Renumera los hijos de "Portal del Residente" (Mis Pagos/Ver presupuesto
--    empataban en orden 2) -- de paso, ícono propio para "Mis Pagos" (tenía
--    el placeholder bi-house).
-- =============================================================================

-- Mis Pagos: ícono propio (orden se queda en 2)
EXEC UPD_MenuItem
    '50968697-DC4F-4FE8-8FD0-10183998C028', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    NULL, 'Mis Pagos', 'bi-credit-card', 'MyPayments', NULL,
    2, 1, 'Mis pagos', 'info', SYSDATETIME();

-- Ver presupuesto: 2 -> 3
EXEC UPD_MenuItem
    '816542AE-4BD4-4423-9362-97310D9BEB17', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    'viewbudget', 'Ver presupuesto', 'bi-graph-up-arrow', 'view-budget', NULL,
    3, 1, NULL, 'danger', SYSDATETIME();

-- Comunicados: 3 -> 4
EXEC UPD_MenuItem
    'E70091F1-596F-4D0D-8E59-025259908549', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    'myannouncements', 'Comunicados', 'bi-bell', 'MyAnnouncements', NULL,
    4, 1, NULL, NULL, SYSDATETIME();

-- Reportar incidencia: 4 -> 5
EXEC UPD_MenuItem
    '6FF2FA10-7E13-4349-82E3-C154D00053D8', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    'reportincident', 'Reportar incidencia', 'bi-exclamation-triangle',
    'ReportIncident', NULL, 5, 1, NULL, NULL, SYSDATETIME();

-- Mi consumo de agua: 5 -> 6
EXEC UPD_MenuItem
    '1A8FE72D-EF48-418C-98F3-270D13EA6796', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    'myconsumption', 'Mi consumo de agua', 'bi-droplet', 'MyConsumption', NULL,
    6, 1, NULL, 'danger', SYSDATETIME();

-- =============================================================================
-- 7) Renumera los hijos de "Configuración" (System Logs/Asignar Roles
--    empataban en orden 9) -- de paso, ícono propio para System Logs/
--    WorkFlows/Edición de Permisos (placeholder bi-house).
-- =============================================================================

-- System Logs: ícono propio (orden se queda en 9)
EXEC UPD_MenuItem
    '8571E886-59A7-4364-85E2-447D7CCC4BFC', @IdSettings, NULL, 'System Logs',
    'bi-journal-text', 'Settings/SystemLogs', NULL, 9, 1, 'Ver logs', 'danger',
    SYSDATETIME();

-- Asignar Roles: 9 -> 10
EXEC UPD_MenuItem
    '1E825A3E-EDFD-4025-988E-F8A1CF26F969', @IdSettings, NULL, 'Asignar Roles',
    'bi-person-gear', 'Settings/UserRoles', NULL, 10, 1, NULL, 'info',
    SYSDATETIME();

-- Acerca de: 10 -> 11
EXEC UPD_MenuItem
    'E52FB66E-C164-4B20-9154-A7FFEE264AC6', @IdSettings, 'about', 'Acerca de',
    'bi-info-circle', 'About', NULL, 11, 1, NULL, 'danger', SYSDATETIME();

-- WorkFlows: 12 (queda en 12), ícono propio
EXEC UPD_MenuItem
    'A8A39C43-CD0B-4D2B-AE27-D8E134C21EF4', @IdSettings, NULL, 'WorkFlows',
    'bi-diagram-3', 'workflow', NULL, 12, 1, 'Worflows', 'info', SYSDATETIME();

-- Edición de Permisos: 13 (queda en 13), ícono propio
EXEC UPD_MenuItem
    '9A2975AE-81D5-4A6C-AAF2-80DBE822C313', @IdSettings, NULL, 'Edicion de Permisos',
    'bi-key-fill', 'Settings/Permissions', NULL, 13, 1, NULL, 'danger',
    SYSDATETIME();

-- (Migración de Datos = 14, ver sección 5 más arriba)

PRINT 'Reorganización de menú aplicada.';

-- Verificación -- correr esto después para confirmar el árbol final:
-- EXEC GET_MenuItem
