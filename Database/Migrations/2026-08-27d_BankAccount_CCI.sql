-- Agrega el número de Cuenta Interbancaria (CCI) a BankAccount, editable en
-- Configuración del Edificio y usado en el pie del recibo de mantenimiento (placeholder
-- {CCI} de InstallmentExportService.ResolveFooterText, hoy resuelto siempre vacío).
--
-- Basado en el texto vigente de UPD_BankAccount/INS_BankAccount y sp_help BankAccount
-- provisto por el usuario. El parámetro nuevo se agrega AL FINAL de cada procedure:
-- BDLayout.Update.cs/BDLayout.Add.cs llaman a estos SPs con parámetros posicionales, así
-- que el orden importa y el código en C# se actualiza para pasarlo en este mismo orden.
--
-- Ejecutar en orden, contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

-- 1) Nueva columna. NOT NULL con default vacío, mismo patrón que ReceiptFooterText.
ALTER TABLE dbo.BankAccount
    ADD CCI NVARCHAR(20) NOT NULL CONSTRAINT DF_BankAccount_CCI DEFAULT ('');
GO

-- 2) UPD_BankAccount
ALTER PROCEDURE dbo.UPD_BankAccount
    @IdBankAccount UNIQUEIDENTIFIER,
    @AccountName NVARCHAR(100),
    @AccountNumber NVARCHAR(20),
    @BankName NVARCHAR(50),
    @AccountType INT,
    @Status INT = NULL,
    @CCI NVARCHAR(20) = NULL
AS
BEGIN
    UPDATE dbo.BankAccount
    SET
        AccountName = @AccountName,
        AccountNumber = @AccountNumber,
        BankName = @BankName,
        AccountType = @AccountType,
        Status = @Status,
        CCI = @CCI
    WHERE IdBankAccount = @IdBankAccount;
END;
GO

-- 3) INS_BankAccount
ALTER PROCEDURE dbo.INS_BankAccount
    @IdBankAccount      UNIQUEIDENTIFIER,
    @AccountName        NVARCHAR(100),
    @AccountNumber      NVARCHAR(20),
    @BankName           NVARCHAR(50),
    @AccountType        INT,
    @IdBuilding         UNIQUEIDENTIFIER,
    @Status             INT = NULL,
    @CCI                NVARCHAR(20) = NULL
AS
BEGIN
    INSERT INTO dbo.BankAccount (IdBankAccount, AccountName, AccountNumber, BankName, AccountType, IdBuilding, Status, CCI)
                    VALUES (@IdBankAccount, @AccountName, @AccountNumber, @BankName, @AccountType, @IdBuilding, @Status, @CCI);
END;
GO
