-- Login social (Google/Microsoft/Facebook/Apple) -- pedido explícito del usuario
-- junto con el refuerzo anti-spam del formulario de contacto.
--
-- Igual que Users.PasswordResetToken (2026-09-16_127): columnas propias en Users
-- (tabla real, vía BDLayout), no AspNetUsers/UserManager<IdentityUser> -- ese
-- flujo de Identity ya está documentado como no funcional de verdad en esta app
-- (ver el comentario grande en Program.cs sobre AddIdentity).
--
-- Un usuario puede tener a lo sumo UN proveedor externo vinculado por fila (no
-- hace falta una tabla aparte: alcanza con guardar el último usado, que es lo
-- que se necesita para reconocerlo la próxima vez que inicie sesión así). Si
-- más adelante alguien pide vincular más de un proveedor a la misma cuenta,
-- ahí sí se justifica una tabla 1-a-muchos -- no antes.

ALTER TABLE dbo.Users ADD ExternalProvider NVARCHAR(30) NULL;
GO

ALTER TABLE dbo.Users ADD ExternalProviderId NVARCHAR(200) NULL;
GO

-- GET_UsersByEmail tiene lista explícita de columnas -- agregar columnas a Users
-- sin agregarlas acá rompe la consulta apenas haya alguna fila (mismo bug que
-- 2026-09-16_126 y 2026-09-16_127, columna faltante vs. FromSql estricto de EF).
CREATE OR ALTER PROCEDURE [dbo].[GET_UsersByEmail]
    @Email VARCHAR(255)
AS
BEGIN
    SELECT
        IdUser,
        Email,
        PasswordHash,
        FirstName,
        LastName,
        PhoneNumber,
        IsActive,
        CreatedAt,
        EmailConfirmed,
        Token,
        PasswordResetToken,
        PasswordResetTokenExpiresAt,
        ExternalProvider,
        ExternalProviderId
    FROM Users
    WHERE Email = @Email;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_UserByExternalLogin]
    @Provider   NVARCHAR(30),
    @ProviderId NVARCHAR(200)
AS
BEGIN
    SELECT TOP 1 *
    FROM    dbo.Users
    WHERE   ExternalProvider = @Provider
            AND ExternalProviderId = @ProviderId;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[UPD_UserExternalLogin]
    @IdUser     UNIQUEIDENTIFIER,
    @Provider   NVARCHAR(30),
    @ProviderId NVARCHAR(200)
AS
BEGIN
    UPDATE  dbo.Users
    SET     ExternalProvider = @Provider,
            ExternalProviderId = @ProviderId
    WHERE   IdUser = @IdUser;
END
GO
