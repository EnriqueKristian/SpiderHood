-- Feature nueva: "¿Olvidaste tu contraseña?" (Login.razor ya tenía el link a
-- /forgot-password, pero esa ruta nunca existió -- 404 al hacer click). No había
-- ninguna tabla/columna de soporte para un flujo self-service de reset por email.
--
-- Se reutiliza el mismo patrón que ya usa Users.Token (email de confirmación,
-- IEmailConfirmationService) pero en columnas propias -- separadas a propósito de
-- Token, para no pisar un posible flujo de confirmación de email pendiente del mismo
-- usuario, y para poder expirar el link (algo que Token nunca tuvo).

ALTER TABLE dbo.Users ADD PasswordResetToken NVARCHAR(200) NULL;
GO

ALTER TABLE dbo.Users ADD PasswordResetTokenExpiresAt DATETIME2 NULL;
GO

CREATE OR ALTER PROCEDURE [dbo].[UPD_UserPasswordResetToken]
    @IdUser     UNIQUEIDENTIFIER,
    @Token      NVARCHAR(200) = NULL,
    @ExpiresAt  DATETIME2 = NULL
AS
BEGIN
    UPDATE  dbo.Users
    SET     PasswordResetToken = @Token,
            PasswordResetTokenExpiresAt = @ExpiresAt
    WHERE   IdUser = @IdUser;
END
GO

-- GET_UsersByEmail tiene lista explícita de columnas (a diferencia de GET_UserById,
-- que usa SELECT *) -- EF Core (FromSql) exige que TODA propiedad de UserModel tenga
-- su columna en el resultado, así que agregar columnas nuevas a la tabla sin
-- agregarlas acá rompe esta consulta apenas alguna fila exista (mismo bug que
-- Database/Scripts/2026-09-16_126_Fix_AccountStatementDetail_Origen_Column.sql,
-- mismo motivo: "required column X was not present in the results").
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
        PasswordResetTokenExpiresAt
    FROM Users
    WHERE Email = @Email;
END
GO
