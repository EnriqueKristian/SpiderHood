-- =============================================================================
-- Da de alta a Emily Echevarria en UserBuildingAssociation (rol Administrador,
-- Edificio Nova Alzamora).
--
-- Contexto: en /users (Gestión de Usuarios) alguien le asignó el rol
-- "Administrador" desde el modal "Editar Usuario". Ese dropdown SOLO escribe en
-- dbo.UserRole (un rol global, sin edificio, que NO tiene ningún efecto en lo
-- que el usuario ve al loguearse). Por eso Emily aparecía en /users con badge
-- "Administrador" pero NO aparecía en /Settings/UserRoles (que lee
-- UserBuildingAssociation, la tabla real que usa AuthService.LoginAsync para
-- armar el menú y los permisos) -- en la práctica, Emily no tenía ningún acceso
-- real sobre ningún edificio.
--
-- Este script usa el mismo SP que ya usa la app (INS_UserBuildingRole, llamado
-- por el combo "Agregar rol" de /Settings/UserRoles) para crear esa fila real.
-- Según cómo esté escrito el SP, puede quedar aprobada de una o pendiente --
-- si queda pendiente, va a aparecer en el panel "Solicitudes Pendientes de
-- Aprobación" arriba de la tabla en /Settings/UserRoles: ahí mismo se aprueba
-- con el botón "Aprobar" (ya funciona, no hace falta SQL para eso).
-- =============================================================================

SET NOCOUNT ON;

DECLARE @IdEmily UNIQUEIDENTIFIER = (SELECT IdUser FROM dbo.Users WHERE Email = N'emilykarolez@gmail.com');
DECLARE @IdBuilding UNIQUEIDENTIFIER = (SELECT IdBuilding FROM dbo.Building WHERE Name LIKE N'%Nova Alzamora%');
DECLARE @IdSysAdmin UNIQUEIDENTIFIER = (SELECT IdUser FROM dbo.Users WHERE Email = N'admin@spiderhood.com');

IF @IdEmily IS NULL
BEGIN
    RAISERROR('No se encontró el usuario con email emilykarolez@gmail.com en dbo.Users.', 16, 1);
    RETURN;
END
IF @IdBuilding IS NULL
BEGIN
    RAISERROR('No se encontró ningún edificio en dbo.Building cuyo Name contenga "Nova Alzamora".', 16, 1);
    RETURN;
END
IF @IdSysAdmin IS NULL
BEGIN
    RAISERROR('No se encontró el usuario admin@spiderhood.com en dbo.Users (usado como ApprovedBy).', 16, 1);
    RETURN;
END

-- Evitar duplicar si ya se corrió antes o si alguien ya la agregó desde la UI
IF EXISTS (
    SELECT 1 FROM dbo.UserBuildingAssociation
    WHERE IdUser = @IdEmily AND IdBuilding = @IdBuilding AND Role = 'Administrador'
)
BEGIN
    PRINT 'Emily ya tiene el rol Administrador en Nova Alzamora -- no se insertó nada.';
END
ELSE
BEGIN
    EXEC INS_UserBuildingRole @IdEmily, @IdBuilding, 'Administrador', @IdSysAdmin;
    PRINT 'Fila creada -- revisar /Settings/UserRoles (tabla principal o "Solicitudes Pendientes").';
END

SELECT * FROM dbo.UserBuildingAssociation WHERE IdUser = @IdEmily;
