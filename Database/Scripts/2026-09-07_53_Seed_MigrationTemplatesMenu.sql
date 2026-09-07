-- =============================================================================
-- Item de menú "Migración de Datos" -- vía el SP que ya usa el resto de la app
-- (dbo.INS_MenuItem), no un INSERT crudo, mismo criterio que
-- 2026-09-02_05_Incidents.sql / 2026-09-02_11_CalendarMaintenance.sql. Apunta a
-- /migracion/plantillas (MigrationTemplates.razor), donde el administrador
-- descarga las 4 plantillas de carga histórica (Cuotas y Pagos, Estado de
-- Cuenta, Lecturas de Agua, Presupuesto Histórico) personalizadas con las
-- unidades/cuentas/categorías ya registradas del edificio.
--
-- Gateo de acceso a la página es por permiso existente ("edit_building",
-- MigrationTemplates.razor.OnInitializedAsync), no por una clave de permiso
-- nueva -- no hace falta tocar Permission/RolePermission para esto.
--
-- NO es idempotente (mismo motivo que los dos scripts de referencia: no
-- conozco el nombre físico real de la tabla de menú desde acá para armar un
-- IF NOT EXISTS confiable). Si corrés el script dos veces vas a duplicar el
-- item de menú -- revisá el menú de Configuración > Items de Menú antes de
-- repetirlo.
-- =============================================================================

SET NOCOUNT ON;
GO

EXEC dbo.INS_MenuItem
    @IdMenu = 'F3A6C1D8-2E7B-4C5A-9F0D-6B8E4A2C1F73',
    @IdParent = NULL,
    @ItemKey = 'migration_templates',
    @Title = 'Migración de Datos',
    @Icon = 'bi bi-file-earmark-arrow-down',
    @Url = '/migracion/plantillas',
    @Target = NULL,
    @DisplayOrder = 60,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
