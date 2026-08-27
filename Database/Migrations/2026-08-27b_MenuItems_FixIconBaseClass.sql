-- Corrige 2026-08-27_MenuItem_BootstrapIcons.sql: los 49 UPDATE de ese script dejaron
-- Icon como 'bi-gear', 'bi-cash-stack', etc. — les falta la clase base "bi" que Bootstrap
-- Icons necesita junto con la clase del ícono para dibujar el glifo (ver cualquier ícono
-- que sí funciona hoy en la app: siempre "bi bi-x", nunca "bi-x" solo). Por eso, aunque la
-- BD ya quedó actualizada, en la Vista de Lista no aparecía ningún ícono.
--
-- Este UPDATE le agrega el prefijo "bi " a cualquier Icon que empiece con "bi-" y todavía
-- no lo tenga. Solo toca las 49 filas que dejó el script anterior; cualquier ícono que ya
-- estuviera correcto como "bi bi-x" no matchea 'bi-%' (empieza con "bi ", no "bi-") así que
-- no se toca.
--
-- Ejecutar contra dbo.MenuItems (SSMS / Azure Data Studio / sqlcmd).

BEGIN TRAN;

UPDATE dbo.MenuItems
SET Icon = 'bi ' + Icon
WHERE Icon LIKE 'bi-%';

-- Deberían ser 49 filas afectadas (las mismas del script anterior). Verificar antes de
-- confirmar, por ejemplo:
-- SELECT IdMenu, Title, Icon FROM dbo.MenuItems WHERE Icon LIKE 'bi %' ORDER BY Title;
--
-- COMMIT;
-- ROLLBACK;
