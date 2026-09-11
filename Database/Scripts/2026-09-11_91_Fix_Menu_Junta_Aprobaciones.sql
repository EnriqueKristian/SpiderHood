-- =============================================================================
-- Los ítems "Aprob. Presupuesto" y "Autorizar gastos" bajo "Junta de Propietarios"
-- son links rotos hoy: no hay (ni hubo nunca en los scripts de migración) ninguna
-- página con esas URLs -- se ve que se crearon a mano desde /Settings/MenuItems
-- apuntando a algo que nunca se construyó.
--
-- Fase 1 de Aprobaciones (Docs/Pendientes-Negocio-*.md): se creó una sola pantalla
-- unificada /aprobaciones (Components/Pages/ApprovalsPages/Approvals.razor) que por
-- ahora sólo muestra presupuestos pendientes (el flujo de Gastos todavía no tiene
-- umbral configurable -- viene en una fase aparte). Este script:
--   1) Reutiliza el ítem "Aprob. Presupuesto" -- lo renombra a "Aprobaciones" y le
--      pone la URL real (aprobaciones).
--   2) Oculta "Autorizar gastos" (IsVisible=0, no se borra) hasta que exista la
--      pantalla real de Gastos -- un link roto visible es peor que no tener el ítem.
--
-- Se usa UPDATE directo (no UPD_MenuItem, que reemplaza la fila entera por posición)
-- porque no se conocen los demás valores actuales de estas dos filas (se crearon a
-- mano, no vía script) -- un UPDATE de una sola columna no tiene ese riesgo.
--
-- Ejecutar y revisar el menú de Junta de Propietarios después de correrlo.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @IdJunta UNIQUEIDENTIFIER = '467674F2-BD3B-41C7-A4FB-B13B5152B914'; -- Junta de Propietarios (ver 2026-09-10_85_Reorganizar_Menu.sql)

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE IdMenu = @IdJunta)
BEGIN
    RAISERROR('No se encontró el grupo "Junta de Propietarios" (Id esperado %s) -- revisar a mano.', 16, 1, @IdJunta);
    RETURN;
END

UPDATE dbo.MenuItems
SET Title = 'Aprobaciones', Url = 'aprobaciones'
WHERE IdParent = @IdJunta AND Title = 'Aprob. Presupuesto';

UPDATE dbo.MenuItems
SET IsVisible = 0
WHERE IdParent = @IdJunta AND Title IN ('Autorizar gastos', 'Autorizar Pagos');

SELECT Title, Url, IsVisible FROM dbo.MenuItems WHERE IdParent = @IdJunta ORDER BY DisplayOrder;
