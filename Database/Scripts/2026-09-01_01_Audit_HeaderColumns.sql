-- =============================================================================
-- Auditoria (Usuario + Fecha de Creacion/Modificacion) en tablas cabecera.
--
-- Alcance inicial: Building, Owner, BudgetHeader, Expense (ver plan de
-- implementaciones pendientes). El resto de cabeceras (Period, ServiceReading,
-- BankAccount, Category, BuildingConfiguration) se agrega mas adelante con el
-- mismo patron.
--
-- No modifica ningun Stored Procedure existente: agrega columnas nuevas
-- (NULL-able, no rompen INSERTs actuales que no las conozcan) y 4 SPs nuevos,
-- dedicados solo a "estampar" auditoria, que se llaman por separado desde la
-- app justo despues del INSERT/UPDATE que ya existe.
--
-- Idempotente: se puede correr mas de una vez sin error.
-- Revisar y correr primero contra una copia de desarrollo/staging.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Columnas nuevas
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'CreatedBy')
    ALTER TABLE dbo.Building ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'CreatedOn')
    ALTER TABLE dbo.Building ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'ModifiedBy')
    ALTER TABLE dbo.Building ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Building') AND name = 'ModifiedOn')
    ALTER TABLE dbo.Building ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Owner') AND name = 'CreatedBy')
    ALTER TABLE dbo.Owner ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Owner') AND name = 'CreatedOn')
    ALTER TABLE dbo.Owner ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Owner') AND name = 'ModifiedBy')
    ALTER TABLE dbo.Owner ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Owner') AND name = 'ModifiedOn')
    ALTER TABLE dbo.Owner ADD ModifiedOn DATETIME2 NULL;
GO

-- BudgetHeader ya tiene CreatedBy/CreatedOn (ver Classes/Models.cs BudgetHeader) --
-- solo faltan Modified*.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BudgetHeader') AND name = 'ModifiedBy')
    ALTER TABLE dbo.BudgetHeader ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BudgetHeader') AND name = 'ModifiedOn')
    ALTER TABLE dbo.BudgetHeader ADD ModifiedOn DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Expense') AND name = 'CreatedBy')
    ALTER TABLE dbo.Expense ADD CreatedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Expense') AND name = 'CreatedOn')
    ALTER TABLE dbo.Expense ADD CreatedOn DATETIME2 NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Expense') AND name = 'ModifiedBy')
    ALTER TABLE dbo.Expense ADD ModifiedBy NVARCHAR(256) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Expense') AND name = 'ModifiedOn')
    ALTER TABLE dbo.Expense ADD ModifiedOn DATETIME2 NULL;
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures de "estampado" de auditoria (uno por tabla, deliberado
--    en vez de un SP generico con SQL dinamico -- mas simple de revisar y
--    sin riesgo de inyeccion).
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.UPD_BuildingAudit
    @IdBuilding UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.Building
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBuilding = @IdBuilding;
    ELSE
        UPDATE dbo.Building
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBuilding = @IdBuilding;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_OwnerAudit
    @IdOwner UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.Owner
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdOwner = @IdOwner;
    ELSE
        UPDATE dbo.Owner
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdOwner = @IdOwner;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_BudgetHeaderAudit
    @IdBudgetHeader UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.BudgetHeader
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBudgetHeader = @IdBudgetHeader;
    ELSE
        UPDATE dbo.BudgetHeader
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdBudgetHeader = @IdBudgetHeader;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ExpenseAudit
    @IdExpense UNIQUEIDENTIFIER,
    @PerformedBy NVARCHAR(256),
    @IsCreate BIT
AS
BEGIN
    SET NOCOUNT ON;
    IF @IsCreate = 1
        UPDATE dbo.Expense
        SET CreatedBy = @PerformedBy, CreatedOn = SYSUTCDATETIME(),
            ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdExpense = @IdExpense;
    ELSE
        UPDATE dbo.Expense
        SET ModifiedBy = @PerformedBy, ModifiedOn = SYSUTCDATETIME()
        WHERE IdExpense = @IdExpense;
END
GO
