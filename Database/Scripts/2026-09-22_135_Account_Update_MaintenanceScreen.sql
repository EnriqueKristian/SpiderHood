-- =============================================================================
-- Pantalla de mantenimiento de Account (Docs/Pendientes-Negocio-Consolidado.md
-- #30, punto a) -- hasta ahora Account (RazonSocial/RucDni/Telefono) sólo se
-- completaba una vez al registrarse (INS_Account, /register-admin) y no
-- había forma de editarlo después. Agrega el UPD_Account que faltaba.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Account
    SET RazonSocial = @RazonSocial,
        RucDni = @RucDni,
        Telefono = @Telefono
    WHERE IdAccount = @IdAccount;
END
GO
