-- =============================================================================
-- Modelo Natural/Empresa para Account (Docs/Pendientes-Negocio-Consolidado.md
-- #30, punto c) -- hoy Account sólo tiene RazonSocial/RucDni genéricos, sin
-- distinguir si la cuenta es de una persona natural administrando a título
-- propio o de una empresa/inmobiliaria administradora formal.
--
-- Mismo criterio que ya usa Owner para Persona Natural/Jurídica
-- (Database/Scripts/2026-09-05_52_Owner_ExtraFields.sql): RazonSocial se
-- sigue reutilizando para los dos casos (nombre completo o razón social de
-- la empresa) -- no se duplica en dos campos --, y los campos nuevos son
-- adicionales, todos NULL-able (fail-open).
--
-- AccountType es NOT NULL DEFAULT(1) -- Natural -- no NULL: es un INT que
-- respalda un enum en C# (Models.AccountType), no un dato opcional como el
-- resto; todo Account existente (creado antes de este script) queda
-- clasificado como Natural, que el usuario puede corregir a Empresa desde
-- la pantalla de mantenimiento (Settings.razor) si corresponde.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'AccountType')
    ALTER TABLE dbo.Account ADD AccountType INT NOT NULL CONSTRAINT DF_Account_AccountType DEFAULT (1);
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'LegalRepresentative')
    ALTER TABLE dbo.Account ADD LegalRepresentative NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'FiscalAddress')
    ALTER TABLE dbo.Account ADD FiscalAddress NVARCHAR(300) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @AccountType INT = 1,
    @LegalRepresentative NVARCHAR(200) = NULL,
    @FiscalAddress NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Account (IdAccount, RazonSocial, RucDni, Telefono, CreatedAt, AccountType, LegalRepresentative, FiscalAddress)
    VALUES (@IdAccount, @RazonSocial, @RucDni, @Telefono, SYSUTCDATETIME(), @AccountType, @LegalRepresentative, @FiscalAddress);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @AccountType INT = 1,
    @LegalRepresentative NVARCHAR(200) = NULL,
    @FiscalAddress NVARCHAR(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Account
    SET RazonSocial = @RazonSocial,
        RucDni = @RucDni,
        Telefono = @Telefono,
        AccountType = @AccountType,
        LegalRepresentative = @LegalRepresentative,
        FiscalAddress = @FiscalAddress
    WHERE IdAccount = @IdAccount;
END
GO

-- Account es EF keyless -- GET_AccountByUser es el único proc que lo hidrata
-- (confirmado: único ExecuteQueryListAsync<Models.Account> en BDLayout.Get.cs),
-- así que es el único que necesita las columnas nuevas en el SELECT.
CREATE OR ALTER PROCEDURE dbo.GET_AccountByUser
    @IdUser UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1
        a.IdAccount,
        a.RazonSocial,
        a.RucDni,
        a.Telefono,
        a.CreatedAt,
        a.AccountType,
        a.LegalRepresentative,
        a.FiscalAddress
    FROM dbo.Account a
    INNER JOIN dbo.AccountUser au ON au.IdAccount = a.IdAccount
    WHERE au.IdUser = @IdUser
    ORDER BY au.CreatedAt ASC;
END
GO
