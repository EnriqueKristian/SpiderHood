-- =====================================================================================
-- Stored procedures nuevos para /Settings/UserRoles.
--
-- La pantalla usaba hasta ahora una tabla separada (UserRole, vía GetAllUsersWithRoles /
-- AddUserRole / DeleteUserRoleByUser) que NO tiene ninguna relación con el sistema real
-- de sesión/permisos: AuthService.LoginAsync arma el menú y los permisos leyendo
-- UserBuildingAssociation, no UserRole. Cambiar el "rol actual" desde esa pantalla no
-- tenía ningún efecto en lo que el usuario ve al loguearse. Se reconstruye sobre
-- UserBuildingAssociation, la tabla real.
--
-- Un usuario puede tener varias filas ahí (un rol por edificio, o incluso varios roles
-- sobre el mismo edificio — caso real ya visto con Administrador/SysAdmin/Junta sobre el
-- mismo edificio). Por eso acá "asignar un rol" es AGREGAR una fila nueva (no reemplazar
-- la única que el usuario tenía), y "quitar un rol" es borrar esa fila puntual.
--
-- CREATE OR ALTER: seguro de re-correr.
-- =====================================================================================

-- Lista de asignaciones usuario+edificio+rol para la pantalla de administración.
CREATE OR ALTER PROCEDURE GET_AllUserBuildingRoles
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        u.IdUser,
        LTRIM(RTRIM(ISNULL(u.FirstName, '') + ' ' + ISNULL(u.LastName, ''))) AS UserName,
        u.Email AS UserEmail,
        ub.IdBuilding,
        b.Name AS BuildingName,
        ub.Role
    FROM UserBuildingAssociation ub
    INNER JOIN Users u ON u.IdUser = ub.IdUser
    INNER JOIN Building b ON b.IdBuilding = ub.IdBuilding
    ORDER BY b.Name, UserName, ub.Role;
END
GO

-- Otorga un rol (idempotente: no duplica si el usuario ya lo tiene en ese edificio).
-- Valida el nombre de rol contra Roles antes de insertar y resuelve IdRole a partir de
-- ahí — mismo problema que ya se corrigió en AcceptInvitationAsync (commit "Valida el
-- rol de una invitación..."): sin esto, un typo deja al usuario con un rol que el resto
-- de la app no reconoce (menú vacío, permisos vacíos).
CREATE OR ALTER PROCEDURE INS_UserBuildingRole
    @IdUser UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Role NVARCHAR(100),
    @ApprovedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @IdRole UNIQUEIDENTIFIER = (SELECT IdRole FROM Roles WHERE RoleName = @Role);

    IF @IdRole IS NULL
    BEGIN
        RAISERROR('El rol "%s" no existe en la tabla Roles.', 16, 1, @Role);
        RETURN;
    END

    IF NOT EXISTS (
        SELECT 1 FROM UserBuildingAssociation
        WHERE IdUser = @IdUser AND IdBuilding = @IdBuilding AND Role = @Role
    )
    BEGIN
        INSERT INTO UserBuildingAssociation
            (IdUser, IdBuilding, Role, IsApproved, RequestedAt, ApprovedAt, ApprovedBy, Status, RequiresApproval, IdRole)
        VALUES
            (@IdUser, @IdBuilding, @Role, 1, GETDATE(), GETDATE(), @ApprovedBy, 'Active', 0, @IdRole);
    END
END
GO

-- Quita un rol puntual (usuario + edificio + rol). No toca los demás roles que el
-- usuario pueda tener en otros edificios, ni otros roles en el mismo edificio.
CREATE OR ALTER PROCEDURE DEL_UserBuildingRole
    @IdUser UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Role NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM UserBuildingAssociation
    WHERE IdUser = @IdUser AND IdBuilding = @IdBuilding AND Role = @Role;
END
GO
