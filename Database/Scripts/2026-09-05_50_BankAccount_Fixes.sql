-- =============================================================================
-- Fix sobre "Nueva Cuenta Bancaria" (BuildingPage.razor, sección Moneda y
-- Cuentas): el guardado fallaba en silencio -- BankAccountService.AddBankAccount
-- atrapaba la excepción y sólo hacía Console.WriteLine, así que el usuario
-- nunca veía el error real. Se confirmó contra la base real (sys.columns) que
-- CCI y AccountNumber son NVARCHAR(20) -- un CCI típico con guiones de
-- formato ("011-149-0200449798-24", 21 caracteres) supera ese límite y
-- SQL Server rechaza el INSERT por truncamiento.
--
-- De paso: CurrentBalance/ReconciledBalance/LastReconciliation ya existían
-- como columnas de dbo.BankAccount pero ningún proc (INS ni UPD) las
-- persistía -- "Saldo Inicial *" del formulario se descartaba en silencio.
-- Se agregan a ambos procs.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

SET NOCOUNT ON;
GO

-- CCI es opcional (sin *, y el proc ya lo declara @CCI = NULL) -- amplía a 30
-- para tolerar CCI con guiones de formato sin perder los 20 dígitos reales.
-- AccountNumber es obligatorio en el formulario (con *) -- mismo criterio.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'CCI' AND max_length = 40)
    ALTER TABLE dbo.BankAccount ALTER COLUMN CCI NVARCHAR(30) NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'AccountNumber' AND max_length = 40)
    ALTER TABLE dbo.BankAccount ALTER COLUMN AccountNumber NVARCHAR(30) NOT NULL;
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
    @ReconciledBalance  DECIMAL(18, 2) = NULL,
    @CurrentBalance     DECIMAL(18, 2) = NULL
AS
BEGIN
    -- "Saldo Inicial" del formulario es a la vez el saldo actual y el
    -- conciliado al momento de crear la cuenta -- todavía no hay ningún
    -- movimiento/conciliación real encima. Si no se manda, ambos quedan en 0
    -- en vez de NULL (decimal no-nullable en el modelo C#, ver
    -- Classes/Budget/BankAccount.cs).
    INSERT INTO dbo.BankAccount
        (IdBankAccount, AccountName, AccountNumber, BankName, AccountType, IdBuilding, Status, CCI,
         ReconciledBalance, CurrentBalance)
    VALUES
        (@IdBankAccount, @AccountName, @AccountNumber, @BankName, @AccountType, @IdBuilding, @Status, @CCI,
         ISNULL(@ReconciledBalance, 0), ISNULL(@CurrentBalance, @ReconciledBalance));
END;
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BankAccount
    @IdBankAccount      UNIQUEIDENTIFIER,
    @AccountName        NVARCHAR(100),
    @AccountNumber      NVARCHAR(30),
    @BankName           NVARCHAR(50),
    @AccountType        INT,
    @Status             INT = NULL,
    @CCI                NVARCHAR(30) = NULL,
    @ReconciledBalance  DECIMAL(18, 2) = NULL
AS
BEGIN
    -- CurrentBalance no se toca acá a propósito -- lo actualiza el flujo de
    -- Conciliación (ReconciliationWorkspace.razor), no la edición manual de
    -- los datos de la cuenta.
    UPDATE dbo.BankAccount
    SET AccountName = @AccountName,
        AccountNumber = @AccountNumber,
        BankName = @BankName,
        AccountType = @AccountType,
        Status = @Status,
        CCI = @CCI,
        ReconciledBalance = COALESCE(@ReconciledBalance, ReconciledBalance)
    WHERE IdBankAccount = @IdBankAccount;
END;
GO
