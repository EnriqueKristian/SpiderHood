-- =============================================================================
-- Corrige los 8 UPD_MenuItem que fallaron en 2026-09-10_85_Reorganizar_Menu.sql
-- con "Cannot insert the value NULL into column 'ItemKey'... does not allow
-- nulls" -- la columna real ItemKey no admite NULL (los blancos del Excel
-- eran string vacío, no NULL). Mismos 8 ítems, mismos valores, sólo cambia
-- NULL -> '' en el parámetro ItemKey.
--
-- NO correr 2026-09-10_85 de nuevo -- ya insertó el grupo "Conciliación" y
-- actualizó los otros ~14 ítems correctamente; volver a correrlo entero
-- crearía un segundo grupo "Conciliación" duplicado. Este script sólo busca
-- el Id del grupo ya creado y reintenta los 8 que fallaron.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @IdConciliacion UNIQUEIDENTIFIER;
SELECT @IdConciliacion = IdMenu
FROM dbo.MenuItems
WHERE ItemKey = 'reconciliation_menu' AND IdParent IS NULL;

IF @IdConciliacion IS NULL
BEGIN
    RAISERROR('No se encontró el grupo "Conciliación" (ItemKey=reconciliation_menu) -- ¿se corrió el script _85 antes que este?', 16, 1);
    RETURN;
END

DECLARE @IdSettings UNIQUEIDENTIFIER = '6ACF696A-49F0-445C-8CFA-15F65D5C1A81';
DECLARE @Now DATETIME2 = SYSDATETIME();

-- Conciliar Pagos
EXEC UPD_MenuItem
    '8B4B6754-7E63-4AA0-B97D-E331AD915353', @IdConciliacion, '',
    'Conciliar Pagos', 'bi-arrow-down-circle', 'ConciliacionPagos', NULL,
    4, 1, 'Conciliacion de Pagos', 'info', @Now;

-- Gastos
EXEC UPD_MenuItem
    '37D1F503-2285-4685-AFAB-A0FC299F2941', @IdConciliacion, '',
    'Gastos', 'bi-cash-coin', 'expense', NULL,
    5, 1, 'Gastos', 'danger', @Now;

-- Cuotas Extraordinarias
EXEC UPD_MenuItem
    '6DBF1BAA-0465-41FE-84AB-09102679C8C4', '0A1CC2F2-6287-46F3-BE37-218A0E7F6206',
    '', 'Cuotas Extraordinarias', 'bi-plus-circle', 'cuotaextraordinaria',
    NULL, 3, 1, 'Cuotas Extraordinarias', 'danger', @Now;

-- Mis Pagos
EXEC UPD_MenuItem
    '50968697-DC4F-4FE8-8FD0-10183998C028', 'C30303F7-DF5D-4526-976E-85C0881A1C79',
    '', 'Mis Pagos', 'bi-credit-card', 'MyPayments', NULL,
    2, 1, 'Mis pagos', 'info', @Now;

-- System Logs
EXEC UPD_MenuItem
    '8571E886-59A7-4364-85E2-447D7CCC4BFC', @IdSettings, '', 'System Logs',
    'bi-journal-text', 'Settings/SystemLogs', NULL, 9, 1, 'Ver logs', 'danger',
    @Now;

-- Asignar Roles
EXEC UPD_MenuItem
    '1E825A3E-EDFD-4025-988E-F8A1CF26F969', @IdSettings, '', 'Asignar Roles',
    'bi-person-gear', 'Settings/UserRoles', NULL, 10, 1, NULL, 'info',
    @Now;

-- WorkFlows
EXEC UPD_MenuItem
    'A8A39C43-CD0B-4D2B-AE27-D8E134C21EF4', @IdSettings, '', 'WorkFlows',
    'bi-diagram-3', 'workflow', NULL, 12, 1, 'Worflows', 'info', @Now;

-- Edición de Permisos
EXEC UPD_MenuItem
    '9A2975AE-81D5-4A6C-AAF2-80DBE822C313', @IdSettings, '', 'Edicion de Permisos',
    'bi-key-fill', 'Settings/Permissions', NULL, 13, 1, NULL, 'danger',
    @Now;

PRINT 'Fix aplicado -- corré EXEC GET_MenuItem para confirmar el árbol completo.';
