-- =============================================================================
-- Auditoria (Usuario + Fecha de Creacion/Modificacion) -- segundo lote de
-- cabeceras: Period, ServiceReading, BankAccount, Category,
-- BuildingConfiguration (Fase B.1 del plan; el primer lote -- Building/Owner/
-- BudgetHeader/Expense -- ver 2026-09-01_01_Audit_HeaderColumns.sql).
--
-- OJO nombres de tabla: segun el diagrama de BD que compartio el usuario, la
-- tabla de Period se llama "Periods" (plural) -- las demas coinciden con el
-- nombre de la clase C#. Revisar contra tu diagrama real antes de correr,
-- igual que con el script anterior.
--
-- Mismo patron que el primer lote: columnas nuevas NULL-able + un SP dedicado
-- por tabla solo para estampar auditoria (no toca ningun SP existente).
-- Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Columnas nuevas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Periods') AND name = 'CreatedBy')
    ALTER TABLE dbo.Periods ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Periods') AND name = 'CreatedOn')
    ALTER TABLE dbo.Periods ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Periods') AND name = 'ModifiedBy')
    ALTER TABLE dbo.Periods ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Periods') AND name = 'ModifiedOn')
    ALTER TABLE dbo.Periods ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceReading') AND name = 'CreatedBy')
    ALTER TABLE dbo.ServiceReading ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceReading') AND name = 'CreatedOn')
    ALTER TABLE dbo.ServiceReading ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceReading') AND name = 'ModifiedBy')
    ALTER TABLE dbo.ServiceReading ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceReading') AND name = 'ModifiedOn')
    ALTER TABLE dbo.ServiceReading ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'CreatedBy')
    ALTER TABLE dbo.BankAccount ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'CreatedOn')
    ALTER TABLE dbo.BankAccount ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'ModifiedBy')
    ALTER TABLE dbo.BankAccount ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BankAccount') AND name = 'ModifiedOn')
    ALTER TABLE dbo.BankAccount ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Category') AND name = 'CreatedBy')
    ALTER TABLE dbo.Category ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Category') AND name = 'CreatedOn')
    ALTER TABLE dbo.Category ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Category') AND name = 'ModifiedBy')
    ALTER TABLE dbo.Category ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Category') AND name = 'ModifiedOn')
    ALTER TABLE dbo.Category ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'CreatedBy')
    ALTER TABLE dbo.BuildingConfiguration ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'CreatedOn')
    ALTER TABLE dbo.BuildingConfiguration ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'ModifiedBy')
    ALTER TABLE dbo.BuildingConfiguration ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BuildingConfiguration') AND name = 'ModifiedOn')
    ALTER TABLE dbo.BuildingConfiguration ADD ModifiedOn DATETIME2 NULL;
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures de "estampado" de auditoria
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.UPD_PeriodAudit
    @IdPeriod UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.Periods
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdPeriod = @IdPeriod;
    ELSE
        UPDATE dbo.Periods
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdPeriod = @IdPeriod;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ServiceReadingAudit
    @IdServiceReading UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.ServiceReading
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdServiceReading = @IdServiceReading;
    ELSE
        UPDATE dbo.ServiceReading
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdServiceReading = @IdServiceReading;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BankAccountAudit
    @IdBankAccount UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.BankAccount
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBankAccount = @IdBankAccount;
    ELSE
        UPDATE dbo.BankAccount
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBankAccount = @IdBankAccount;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_CategoryAudit
    @IdCategory UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.Category
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdCategory = @IdCategory;
    ELSE
        UPDATE dbo.Category
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdCategory = @IdCategory;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BuildingConfigurationAudit
    @IdBuildingConfiguration UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.BuildingConfiguration
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
    ELSE
        UPDATE dbo.BuildingConfiguration
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBuildingConfiguration = @IdBuildingConfiguration;
END
GO
