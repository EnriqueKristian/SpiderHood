-- =============================================================================
-- Sólo LECTURA -- no modifica nada. Junta la información que falta para dos
-- pasos pendientes de la revisión de Usuarios/Roles de hoy:
--
-- 1) Nombre exacto del permiso "assign_roles" tal como aparece en la pantalla
--    Configuración > Roles y Permisos (columna "Name"), para que sepas qué
--    buscar ahí y activárselo también a "Administrador" (hoy sólo lo tiene
--    SysAdmin -- por eso un Administrador no puede entrar a
--    /Settings/UserRoles todavía, aunque ya se scopeó la pantalla por edificio).
--
-- 2) Confirmar qué objeto real hay detrás de GET_InvitationByCode (la SP que
--    ya lee SpiderHood.Models.InvitationModel) -- antes de construir el botón
--    "Invitar" nuevo hace falta ver su texto real: si usa una tabla
--    dbo.Invitation con ApartmentNumber (int) en vez de IdGroupUnit (uniqueidentifier),
--    conviene NO resucitarla tal cual (quedaría desalineada con el sistema real
--    de unidades que ya usa el resto de la app, incluida la solicitud de
--    acceso que se acaba de agregar hoy) y en cambio armar una tabla/flujo
--    nuevo y más simple, coherente con IdGroupUnit.
-- =============================================================================

SET NOCOUNT ON;

PRINT '--- 1) Permiso assign_roles ---';
SELECT PermissionId, PermissionKey, Name, Description, [Group]
FROM dbo.Permissions
WHERE PermissionKey = 'assign_roles';

PRINT '--- 2) Texto real de GET_InvitationByCode (o el nombre real si difiere) ---';
IF OBJECT_ID('dbo.GET_InvitationByCode') IS NOT NULL
    EXEC sp_helptext 'GET_InvitationByCode';
ELSE
    PRINT 'No existe dbo.GET_InvitationByCode -- revisar StoredProcedures.cs en el código para el nombre real.';
