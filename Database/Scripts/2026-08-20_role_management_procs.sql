/*
    Adds the stored procedures required for full Role/Permission CRUD
    (Settings/Roles, Settings/Roles/Permissions, Settings/UserRoles).

    Before this script, only GET_AllRoles, GET_RoleById, GET_PermissionsByRole
    and INS_RolePermissions existed — there was no way to insert, update or
    delete a Role, clear a role's permissions before reassigning them, assign
    a role to a user, or list real users for the "User Roles" page.

    Run this against the SpiderHoodContext database before deploying the
    corresponding application changes.
*/

IF OBJECT_ID('dbo.INS_Role', 'P') IS NOT NULL DROP PROCEDURE dbo.INS_Role;
GO
CREATE PROCEDURE dbo.INS_Role
    @IdRole UNIQUEIDENTIFIER,
    @RoleName VARCHAR(100),
    @Description VARCHAR(500),
    @IsSystem BIT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Roles (IdRole, RoleName, Description, IsSystem, CreatedAt)
    VALUES (@IdRole, @RoleName, @Description, @IsSystem, GETUTCDATE());
END;
GO

IF OBJECT_ID('dbo.UPD_Role', 'P') IS NOT NULL DROP PROCEDURE dbo.UPD_Role;
GO
CREATE PROCEDURE dbo.UPD_Role
    @IdRole UNIQUEIDENTIFIER,
    @RoleName VARCHAR(100),
    @Description VARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Roles
    SET RoleName = @RoleName,
        Description = @Description
    WHERE IdRole = @IdRole;
END;
GO

IF OBJECT_ID('dbo.DEL_Role', 'P') IS NOT NULL DROP PROCEDURE dbo.DEL_Role;
GO
CREATE PROCEDURE dbo.DEL_Role
    @IdRole UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Roles WHERE IdRole = @IdRole AND IsSystem = 1)
    BEGIN
        RAISERROR('No se puede eliminar un rol de sistema.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM UserBuildingAssociation WHERE IdRole = @IdRole)
    BEGIN
        RAISERROR('No se puede eliminar un rol asignado a usuarios en edificios.', 16, 1);
        RETURN;
    END

    DELETE FROM RolePermissions WHERE RoleId = @IdRole;
    DELETE FROM UserRole WHERE IdRole = @IdRole;
    DELETE FROM MenuPermissions WHERE IdRole = @IdRole;
    DELETE FROM Roles WHERE IdRole = @IdRole;
END;
GO

IF OBJECT_ID('dbo.DEL_RolePermissionsByRole', 'P') IS NOT NULL DROP PROCEDURE dbo.DEL_RolePermissionsByRole;
GO
CREATE PROCEDURE dbo.DEL_RolePermissionsByRole
    @IdRole UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM RolePermissions WHERE RoleId = @IdRole;
END;
GO

IF OBJECT_ID('dbo.INS_UserRole', 'P') IS NOT NULL DROP PROCEDURE dbo.INS_UserRole;
GO
CREATE PROCEDURE dbo.INS_UserRole
    @IdUser UNIQUEIDENTIFIER,
    @IdRole UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO UserRole (IdUser, IdRole, CreatedAt)
    VALUES (@IdUser, @IdRole, GETUTCDATE());
END;
GO

IF OBJECT_ID('dbo.DEL_UserRoleByUser', 'P') IS NOT NULL DROP PROCEDURE dbo.DEL_UserRoleByUser;
GO
CREATE PROCEDURE dbo.DEL_UserRoleByUser
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM UserRole WHERE IdUser = @IdUser;
END;
GO

IF OBJECT_ID('dbo.GET_AllUsersWithRoles', 'P') IS NOT NULL DROP PROCEDURE dbo.GET_AllUsersWithRoles;
GO
CREATE PROCEDURE dbo.GET_AllUsersWithRoles
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.IdUser,
        u.Email AS UserEmail,
        (u.FirstName + ' ' + u.LastName) AS UserName,
        ISNULL(r.RoleName, '') AS CurrentRole
    FROM Users u
    LEFT JOIN UserRole ur ON ur.IdUser = u.IdUser
    LEFT JOIN Roles r ON r.IdRole = ur.IdRole
    ORDER BY u.Email;
END;
GO

IF OBJECT_ID('dbo.GET_RoleByUserId', 'P') IS NOT NULL DROP PROCEDURE dbo.GET_RoleByUserId;
GO
CREATE PROCEDURE dbo.GET_RoleByUserId
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 r.*
    FROM Roles r
    INNER JOIN UserRole ur ON ur.IdRole = r.IdRole
    WHERE ur.IdUser = @IdUser;
END;
GO
