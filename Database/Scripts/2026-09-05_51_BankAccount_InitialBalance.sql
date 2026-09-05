-- =============================================================================
-- Fix sobre 2026-09-05_50_BankAccount_Fixes.sql: esa versión confundía
-- conceptos contables distintos -- guardaba "Saldo Inicial" del formulario en
-- @ReconciledBalance y ponía CurrentBalance en 0, y no mandaba
-- @LastReconciliation (columna NOT NULL) -- lo que rompía el INSERT con:
--   Msg 515: Cannot insert the value NULL into column 'LastReconciliation'...
--
-- Corregido según el usuario (dueño del negocio, no adivinado):
--   - InitialBalance (NUEVA columna): el "Saldo Inicial" que se carga UNA
--     SOLA VEZ al crear la cuenta -- no se puede modificar después (la UI lo
--     deshabilita al editar, ver BuildingPage.razor).
--   - CurrentBalance: arranca en 0 al crear la cuenta, sólo lectura en el
--     formulario -- lo actualiza el proceso de Conciliación (ingresos y
--     egresos reales), no la creación/edición manual de la cuenta.
--   - ReconciledBalance / LastReconciliation: tampoco se tocan desde este
--     formulario -- quedan exclusivamente para cuando se valide el proceso
--     de Conciliación (ReconciliationWorkspace.razor). LastReconciliation
--     pasa a ser NULL-able: una cuenta recién creada todavía no tuvo ninguna
--     conciliación.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'InitialBalance')
    ALTER TABLE dbo.BankAccount ADD InitialBalance DECIMAL(18, 2) NOT NULL CONSTRAINT DF_BankAccount_InitialBalance DEFAULT (0);
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'LastReconciliation' AND is_nullable = 0)
    ALTER TABLE dbo.BankAccount ALTER COLUMN LastReconciliation DATETIME NULL;
GO

CREATE OR ALTER PROCEDURE dbo.INS_BankAccount
    @IdBankAccount      UNIQUEIDENTIFIER,
    @AccountName        NVARCHAR(100),
    @AccountNumber      NVARCHAR(30),
    @BankName           NVARCHAR(50),
    @AccountType        INT,
    @IdBuilding         UNIQUEIDENTIFIER,
    @Status             INT = NULL,
    @CCI                NVARCHAR(30) = NULL,
    @InitialBalance     DECIMAL(18, 2) = 0
AS
BEGIN
    INSERT INTO dbo.BankAccount
        (IdBankAccount, AccountName, AccountNumber, BankName, AccountType, IdBuilding, Status, CCI,
         InitialBalance, CurrentBalance, ReconciledBalance, LastReconciliation)
    VALUES
        (@IdBankAccount, @AccountName, @AccountNumber, @BankName, @AccountType, @IdBuilding, @Status, @CCI,
         @InitialBalance, 0, 0, NULL);
END;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BankAccount
    @IdBankAccount      UNIQUEIDENTIFIER,
    @AccountName        NVARCHAR(100),
    @AccountNumber      NVARCHAR(30),
    @BankName           NVARCHAR(50),
    @AccountType        INT,
    @Status             INT = NULL,
    @CCI                NVARCHAR(30) = NULL
AS
BEGIN
    -- InitialBalance/CurrentBalance/ReconciledBalance/LastReconciliation NO se
    -- tocan acá: InitialBalance es fijo desde la creación (el campo queda
    -- disabled al editar, ver BuildingPage.razor), y los otros tres los
    -- administra el proceso de Conciliación.
    UPDATE dbo.BankAccount
    SET AccountName = @AccountName,
        AccountNumber = @AccountNumber,
        BankName = @BankName,
        AccountType = @AccountType,
        Status = @Status,
        CCI = @CCI
    WHERE IdBankAccount = @IdBankAccount;
END;
GO

-- Texto real (confirmado por el usuario, sp_helptext) + InitialBalance al
-- SELECT -- sin esto, BDLayout.GetBankAccountsByBuildingAsync (FromSqlRaw)
-- rompía con "The required column 'InitialBalance' was not present in the
-- results of a 'FromSql' operation" apenas se abriera la lista de cuentas
-- bancarias del edificio.
CREATE OR ALTER PROCEDURE dbo.GET_BankAccountsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdBankAccount,
            AccountName,
            AccountNumber,
            BankName,
            AccountType,
            InitialBalance,
            CurrentBalance,
            ReconciledBalance,
            LastReconciliation,
            IdBuilding,
            Status,
            CCI
    FROM    BankAccount b
    WHERE   IdBuilding = @IdBuilding
END
GO
