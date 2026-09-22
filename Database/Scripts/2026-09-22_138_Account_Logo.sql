-- =============================================================================
-- Logo de la empresa administradora (Docs/Pendientes-Negocio-Consolidado.md
-- #30, punto d) -- se usa en la emisión de recibos (InstallmentExportService,
-- Classes/Utilities.cs) y queda disponible para PDFs/reportes futuros que lo
-- necesiten. Reusa IFileStorageService (mismo mecanismo ya construido para
-- fotos de Incidencias y Recibos PDF, punto 18) -- no un storage aparte.
--
-- LogoPath guarda la ruta RELATIVA que devuelve IFileStorageService.SaveAsync
-- (nunca una ruta absoluta ni una URL pública -- el archivo sigue fuera de
-- wwwroot, se lee siempre a través de la app). LogoContentType para saber
-- cómo interpretarlo al armar el data URI (image/png, image/jpeg, etc.) sin
-- tener que adivinarlo de la extensión.
--
-- UPD_Account_Logo separado de UPD_Account (mismo criterio que
-- UPD_BuildingConfiguration_ExpenseThreshold en
-- 2026-09-11_92_Expense_Approval_Threshold.sql): subir un logo es una acción
-- aparte del formulario de datos de facturación, no queremos que cada guardado
-- de RazonSocial/RucDni tenga que mandar también el logo (o pisarlo con NULL
-- por accidente si el caller no lo conoce en ese momento).
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'LogoPath')
    ALTER TABLE dbo.Account ADD LogoPath NVARCHAR(500) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Account') AND name = 'LogoContentType')
    ALTER TABLE dbo.Account ADD LogoContentType NVARCHAR(100) NULL;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Account_Logo
    @IdAccount UNIQUEIDENTIFIER,
    @LogoPath NVARCHAR(500) = NULL,
    @LogoContentType NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Account
    SET LogoPath = @LogoPath,
        LogoContentType = @LogoContentType
    WHERE IdAccount = @IdAccount;
END
GO

-- Account es EF keyless -- toda columna mapeada en Models.Account debe estar
-- en el SELECT de CUALQUIER proc que lo hidrate (GET_AccountByUser YA
-- existía; GET_AccountById es nuevo, lo usa AccountService para resolver el
-- logo de un Building a partir de Building.IdAccount, sin depender de por
-- qué usuario se está exportando el recibo).
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
        a.LogoContentType
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
        LogoContentType
    FROM dbo.Account
    WHERE IdAccount = @IdAccount;
END
GO
