-- =============================================================================
-- Bug preexistente, no introducido hoy: INS_User (confirmado con su
-- CREATE PROCEDURE real, sp_helptext) nunca tomaba @IsActive como parámetro --
-- lo insertaba hardcodeado en 0 (Inactivo). Cualquier usuario nuevo creado desde
-- la app (self-service, registro de Administrador nuevo vía /register-admin, o
-- un admin dándolo de alta desde Configuración > Usuarios) quedaba inactivo sin
-- que nadie lo supiera -- AddNewRecordAsync(UserModel) en BDLayout.Add.cs ya
-- ponía user.IsActive = true en el objeto C#, pero nunca lo mandaba al proc (sólo
-- pasaba IdUser/Email/PasswordHash/FirstName/LastName/PhoneNumber). Recién se
-- detectó ahora porque es la primera vez en la sesión que se prueba un alta +
-- login inmediato de punta a punta (RegisterNewAdministratorAsync, Paso del
-- self-service).
--
-- Se agrega @IsActive BIT con default 1 (en vez de 0) -- si en algún momento se
-- necesita dar de alta a alguien ya inactivo, el llamador lo puede pasar en 0
-- explícitamente; por default, un usuario nuevo nace activo.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_User
    @IdUser         UNIQUEIDENTIFIER,
    @Email          NVARCHAR(255),
    @PasswordHash   NVARCHAR(255),
    @FirstName      NVARCHAR(100),
    @LastName       NVARCHAR(100),
    @PhoneNumber    NVARCHAR(50),
    @IsActive       BIT = 1
AS
BEGIN
    INSERT INTO Users (
        IdUser, Email, PasswordHash, FirstName, LastName, PhoneNumber, IsActive, CreatedAt
    )
    VALUES (
        @IdUser, @Email, @PasswordHash, @FirstName, @LastName, @PhoneNumber, @IsActive, GETDATE()
    );
END;
GO
