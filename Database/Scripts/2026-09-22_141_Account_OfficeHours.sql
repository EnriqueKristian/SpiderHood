-- =============================================================================
-- Horario de Atención en Account (Docs/Pendientes-Negocio-Consolidado.md #33)
-- -- necesario para que Building.OfficeHours pueda usar el mismo patrón de
-- fallback ya implementado para el logo (BuildingService.GetReceiptBrandingAsync):
-- si un Edificio no carga su propio horario, se usa el de la Account
-- (administradora) en su lugar, evitando que una administradora con varios
-- edificios tenga que tipear el mismo horario una vez por edificio.
--
-- NULL-able, fail-open -- toda Account existente queda sin este dato hasta
-- que alguien lo cargue desde Settings.razor.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'OfficeHours')
    ALTER TABLE dbo.Account ADD OfficeHours NVARCHAR(200) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @AccountType INT = 1,
    @LegalRepresentative NVARCHAR(200) = NULL,
    @FiscalAddress NVARCHAR(300) = NULL,
    @OfficeHours NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.Account (IdAccount, RazonSocial, RucDni, Telefono, CreatedAt, AccountType, LegalRepresentative, FiscalAddress, OfficeHours)
    VALUES (@IdAccount, @RazonSocial, @RucDni, @Telefono, SYSUTCDATETIME(), @AccountType, @LegalRepresentative, @FiscalAddress, @OfficeHours);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Account
    @IdAccount UNIQUEIDENTIFIER,
    @RazonSocial NVARCHAR(200) = NULL,
    @RucDni NVARCHAR(20) = NULL,
    @Telefono NVARCHAR(30) = NULL,
    @AccountType INT = 1,
    @LegalRepresentative NVARCHAR(200) = NULL,
    @FiscalAddress NVARCHAR(300) = NULL,
    @OfficeHours NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Account
    SET RazonSocial = @RazonSocial,
        RucDni = @RucDni,
        Telefono = @Telefono,
        AccountType = @AccountType,
        LegalRepresentative = @LegalRepresentative,
        FiscalAddress = @FiscalAddress,
        OfficeHours = @OfficeHours
    WHERE IdAccount = @IdAccount;
END
GO

-- Account es EF keyless -- toda columna mapeada en Models.Account debe estar
-- en el SELECT de los dos procs que lo hidratan (GET_AccountByUser y
-- GET_AccountById, ver 2026-09-22_138_Account_Logo.sql).
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
        a.FiscalAddress,
        a.LogoPath,
        a.LogoContentType,
        a.OfficeHours
    FROM dbo.Account a
    INNER JOIN dbo.AccountUser au ON au.IdAccount = a.IdAccount
    WHERE au.IdUser = @IdUser
    ORDER BY au.CreatedAt ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_AccountById
    @IdAccount UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        IdAccount,
        RazonSocial,
        RucDni,
        Telefono,
        CreatedAt,
        AccountType,
        LegalRepresentative,
        FiscalAddress,
        LogoPath,
        LogoContentType,
        OfficeHours
    FROM dbo.Account
    WHERE IdAccount = @IdAccount;
END
GO
