-- =============================================================================
-- Corrige CalendarItem.Category: en vez de un catálogo propio hardcodeado
-- (Ascensor/Bombas/Limpieza/...) pasa a usar la tabla Category real del
-- edificio -- la misma que ya usan Gastos/Presupuesto (Icon/Color ya están
-- configurados ahí, Configuración > Categorías).
--
-- Reemplaza la columna Category (NVARCHAR, enum C# MaintenanceCategory que se
-- elimina) por IdCategory (UNIQUEIDENTIFIER, FK lógica a Category.IdCategory
-- -- sin FK física, mismo criterio que Incident.IdGroupUnit, para no
-- arriesgarme a un nombre de constraint o un esquema que no puedo confirmar
-- desde acá). GET_CalendarItems* ahora traen Name/Icon/Color por LEFT JOIN.
--
-- Corré esto DESPUÉS de 2026-09-02_11_CalendarMaintenance.sql (crea la tabla
-- si todavía no existe). Es seguro correrlo aunque ya hayas usado el
-- calendario -- ALTER TABLE es idempotente, pero OJO: cualquier item ya
-- creado pierde su categoría vieja (quedan sin categorizar, no hay forma de
-- mapear el enum viejo a un IdCategory real automáticamente).
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Columna
-- -----------------------------------------------------------------------------

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarItem')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CalendarItem') AND name = 'IdCategory')
    BEGIN
        ALTER TABLE dbo.CalendarItem ADD IdCategory UNIQUEIDENTIFIER NULL;
        CREATE INDEX IX_CalendarItem_IdCategory ON dbo.CalendarItem (IdCategory);
    END

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CalendarItem') AND name = 'Category')
    BEGIN
        ALTER TABLE dbo.CalendarItem DROP COLUMN Category;
    END
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures (reemplazan los de 2026-09-02_11 completos)
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_CalendarItem
    @IdCalendarItem UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @Type NVARCHAR(20),
    @IdCategory UNIQUEIDENTIFIER = NULL,
    @StartDate DATETIME2,
    @EndDate DATETIME2 = NULL,
    @Location NVARCHAR(200) = NULL,
    @Responsible NVARCHAR(200) = NULL,
    @Cost DECIMAL(18,2) = NULL,
    @Status NVARCHAR(20),
    @Recurrence NVARCHAR(20),
    @RecurrenceInterval INT = 1,
    @RecurrenceEndDate DATETIME2 = NULL,
    @IdRecurrenceGroup UNIQUEIDENTIFIER = NULL,
    @IsRecurrenceMaster BIT = 0,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.CalendarItem
        (IdCalendarItem, IdBuilding, Title, Description, Type, IdCategory, StartDate, EndDate,
         Location, Responsible, Cost, Status,
         Recurrence, RecurrenceInterval, RecurrenceEndDate, IdRecurrenceGroup, IsRecurrenceMaster,
         CreatedBy, CreatedOn, ModifiedBy, ModifiedOn)
    VALUES
        (@IdCalendarItem, @IdBuilding, @Title, @Description, @Type, @IdCategory, @StartDate, @EndDate,
         @Location, @Responsible, @Cost, @Status,
         @Recurrence, @RecurrenceInterval, @RecurrenceEndDate, @IdRecurrenceGroup, @IsRecurrenceMaster,
         @CreatedBy, SYSUTCDATETIME(), NULL, NULL);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_CalendarItem
    @IdCalendarItem UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @IdCategory UNIQUEIDENTIFIER = NULL,
    @StartDate DATETIME2,
    @EndDate DATETIME2 = NULL,
    @Location NVARCHAR(200) = NULL,
    @Responsible NVARCHAR(200) = NULL,
    @Cost DECIMAL(18,2) = NULL,
    @ModifiedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CalendarItem
    SET Title = @Title,
        Description = @Description,
        IdCategory = @IdCategory,
        StartDate = @StartDate,
        EndDate = @EndDate,
        Location = @Location,
        Responsible = @Responsible,
        Cost = @Cost,
        ModifiedBy = @ModifiedBy,
        ModifiedOn = SYSUTCDATETIME()
    WHERE IdCalendarItem = @IdCalendarItem;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemsByBuilding
    @IdBuilding UNIQUEIDENTIFIER,
    @From DATETIME2 = NULL,
    @To DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ci.IdCalendarItem, ci.IdBuilding, ci.Title, ci.Description, ci.Type, ci.IdCategory, ci.StartDate, ci.EndDate,
           ci.Location, ci.Responsible, ci.Cost, ci.Status,
           ci.Recurrence, ci.RecurrenceInterval, ci.RecurrenceEndDate, ci.IdRecurrenceGroup, ci.IsRecurrenceMaster,
           ci.CreatedBy, ci.CreatedOn, ci.ModifiedBy, ci.ModifiedOn,
           cat.Description AS CategoryName, cat.Icon AS CategoryIcon, cat.Color AS CategoryColor
    FROM dbo.CalendarItem ci
    LEFT JOIN dbo.Category cat ON cat.IdCategory = ci.IdCategory
    WHERE ci.IdBuilding = @IdBuilding
      AND (@From IS NULL OR ci.StartDate >= @From)
      AND (@To IS NULL OR ci.StartDate <= @To)
    ORDER BY ci.StartDate ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemById
    @IdCalendarItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ci.IdCalendarItem, ci.IdBuilding, ci.Title, ci.Description, ci.Type, ci.IdCategory, ci.StartDate, ci.EndDate,
           ci.Location, ci.Responsible, ci.Cost, ci.Status,
           ci.Recurrence, ci.RecurrenceInterval, ci.RecurrenceEndDate, ci.IdRecurrenceGroup, ci.IsRecurrenceMaster,
           ci.CreatedBy, ci.CreatedOn, ci.ModifiedBy, ci.ModifiedOn,
           cat.Description AS CategoryName, cat.Icon AS CategoryIcon, cat.Color AS CategoryColor
    FROM dbo.CalendarItem ci
    LEFT JOIN dbo.Category cat ON cat.IdCategory = ci.IdCategory
    WHERE ci.IdCalendarItem = @IdCalendarItem;
END
GO
