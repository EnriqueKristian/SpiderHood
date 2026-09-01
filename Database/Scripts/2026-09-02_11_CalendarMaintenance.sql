-- =============================================================================
-- Módulo de Calendario: Eventos y Mantenimientos programados del edificio, con
-- recurrencia (Ninguna/Diaria/Semanal/Mensual/Anual) y notificación por email
-- al crear/publicar (ver Services/ICalendarService.cs).
--
-- Un solo item de calendario -- distinguido por Type (Event/Maintenance).
-- Category/Cost/Responsible solo se completan para Type = Maintenance.
--
-- La recurrencia NO se expande en tiempo de lectura: al crear un item con
-- Recurrence != None, el service (no este script) genera una fila por cada
-- ocurrencia, todas compartiendo IdRecurrenceGroup. Por eso no hay columnas
-- de "regla" separadas de las de la primera ocurrencia -- cada fila ES una
-- ocurrencia real con su propia fecha.
--
-- Tabla 100% nueva -- no toca nada existente. Nace con columnas de auditoría
-- (CreatedBy/CreatedOn/ModifiedBy/ModifiedOn) desde el INSERT, mismo criterio
-- que Database/Scripts/2026-09-02_05_Incidents.sql.
--
-- Idempotente (salvo el paso 3, el item de menú -- mismo motivo que en los
-- scripts anteriores: no puedo confirmar desde acá el nombre físico real de
-- la tabla de menú para armar un IF NOT EXISTS confiable).
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Tabla
-- -----------------------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarItem')
BEGIN
    CREATE TABLE dbo.CalendarItem
    (
        IdCalendarItem      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding           UNIQUEIDENTIFIER NOT NULL,
        Title                NVARCHAR(200)    NOT NULL,
        Description          NVARCHAR(MAX)    NULL,
        Type                 NVARCHAR(20)     NOT NULL,   -- CalendarItemType: Event/Maintenance
        Category             NVARCHAR(20)     NULL,       -- MaintenanceCategory -- solo si Type = Maintenance
        StartDate            DATETIME2        NOT NULL,
        EndDate              DATETIME2        NULL,
        Location             NVARCHAR(200)    NULL,
        Responsible          NVARCHAR(200)    NULL,       -- proveedor/técnico a cargo (Maintenance) u organizador (Event)
        Cost                 DECIMAL(18,2)    NULL,
        Status               NVARCHAR(20)     NOT NULL,   -- CalendarItemStatus: Scheduled/Completed/Cancelled

        Recurrence           NVARCHAR(20)     NOT NULL,   -- RecurrenceType: None/Daily/Weekly/Monthly/Yearly
        RecurrenceInterval   INT              NOT NULL DEFAULT (1),
        RecurrenceEndDate    DATETIME2        NULL,
        IdRecurrenceGroup    UNIQUEIDENTIFIER NULL,        -- comparten grupo todas las ocurrencias generadas juntas
        IsRecurrenceMaster   BIT              NOT NULL DEFAULT (0),

        CreatedBy            NVARCHAR(256)    NOT NULL,
        CreatedOn            DATETIME2        NOT NULL,
        ModifiedBy           NVARCHAR(256)    NULL,
        ModifiedOn           DATETIME2        NULL
    );

    CREATE INDEX IX_CalendarItem_Building_StartDate ON dbo.CalendarItem (IdBuilding, StartDate);
    CREATE INDEX IX_CalendarItem_RecurrenceGroup ON dbo.CalendarItem (IdRecurrenceGroup);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Stored Procedures
-- -----------------------------------------------------------------------------

CREATE OR ALTER PROCEDURE dbo.INS_CalendarItem
    @IdCalendarItem UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @Type NVARCHAR(20),
    @Category NVARCHAR(20) = NULL,
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
        (IdCalendarItem, IdBuilding, Title, Description, Type, Category, StartDate, EndDate,
         Location, Responsible, Cost, Status,
         Recurrence, RecurrenceInterval, RecurrenceEndDate, IdRecurrenceGroup, IsRecurrenceMaster,
         CreatedBy, CreatedOn, ModifiedBy, ModifiedOn)
    VALUES
        (@IdCalendarItem, @IdBuilding, @Title, @Description, @Type, @Category, @StartDate, @EndDate,
         @Location, @Responsible, @Cost, @Status,
         @Recurrence, @RecurrenceInterval, @RecurrenceEndDate, @IdRecurrenceGroup, @IsRecurrenceMaster,
         @CreatedBy, SYSUTCDATETIME(), NULL, NULL);
END
GO

-- Edita una ocurrencia puntual (los campos de recurrencia no se tocan acá --
-- se fijan una sola vez al crear la serie completa).
CREATE OR ALTER PROCEDURE dbo.UPD_CalendarItem
    @IdCalendarItem UNIQUEIDENTIFIER,
    @Title NVARCHAR(200),
    @Description NVARCHAR(MAX) = NULL,
    @Category NVARCHAR(20) = NULL,
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
        Category = @Category,
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

-- Cambio rápido de estado (el dropdown Programado/Completado de la grilla).
CREATE OR ALTER PROCEDURE dbo.UPD_CalendarItemStatus
    @IdCalendarItem UNIQUEIDENTIFIER,
    @Status NVARCHAR(20),
    @ModifiedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.CalendarItem
    SET Status = @Status,
        ModifiedBy = @ModifiedBy,
        ModifiedOn = SYSUTCDATETIME()
    WHERE IdCalendarItem = @IdCalendarItem;
END
GO

-- @DeleteSeries = 1 borra esta ocurrencia y las futuras del mismo
-- IdRecurrenceGroup (StartDate >= la de esta fila); si la fila no pertenece a
-- una serie (IdRecurrenceGroup NULL), se comporta igual que @DeleteSeries = 0.
CREATE OR ALTER PROCEDURE dbo.DEL_CalendarItem
    @IdCalendarItem UNIQUEIDENTIFIER,
    @DeleteSeries BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @DeleteSeries = 1
    BEGIN
        DECLARE @IdRecurrenceGroup UNIQUEIDENTIFIER, @StartDate DATETIME2;
        SELECT @IdRecurrenceGroup = IdRecurrenceGroup, @StartDate = StartDate
        FROM dbo.CalendarItem
        WHERE IdCalendarItem = @IdCalendarItem;

        IF @IdRecurrenceGroup IS NOT NULL
        BEGIN
            DELETE FROM dbo.CalendarItem
            WHERE IdRecurrenceGroup = @IdRecurrenceGroup
              AND StartDate >= @StartDate;
            RETURN;
        END
    END

    DELETE FROM dbo.CalendarItem WHERE IdCalendarItem = @IdCalendarItem;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemsByBuilding
    @IdBuilding UNIQUEIDENTIFIER,
    @From DATETIME2 = NULL,
    @To DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdCalendarItem, IdBuilding, Title, Description, Type, Category, StartDate, EndDate,
           Location, Responsible, Cost, Status,
           Recurrence, RecurrenceInterval, RecurrenceEndDate, IdRecurrenceGroup, IsRecurrenceMaster,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.CalendarItem
    WHERE IdBuilding = @IdBuilding
      AND (@From IS NULL OR StartDate >= @From)
      AND (@To IS NULL OR StartDate <= @To)
    ORDER BY StartDate ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemById
    @IdCalendarItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdCalendarItem, IdBuilding, Title, Description, Type, Category, StartDate, EndDate,
           Location, Responsible, Cost, Status,
           Recurrence, RecurrenceInterval, RecurrenceEndDate, IdRecurrenceGroup, IsRecurrenceMaster,
           CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.CalendarItem
    WHERE IdCalendarItem = @IdCalendarItem;
END
GO

-- -----------------------------------------------------------------------------
-- 3) Item de menú "Calendario" -- vía dbo.INS_MenuItem, mismo criterio que
--    2026-09-02_05_Incidents.sql. Gateo de acceso a crear/editar es por rol
--    directo (Administrador/Junta/SysAdmin), Residente ve en solo lectura --
--    se resuelve en el código de la página, no acá.
-- -----------------------------------------------------------------------------

-- NO es idempotente este paso puntual (mismo motivo que en scripts anteriores).
-- Si corrés el script dos veces, vas a duplicar el item de menú -- revisá el
-- menú de Configuración > Items de Menú antes de repetirlo.
EXEC dbo.INS_MenuItem
    @IdMenu = '5F08C2D1-430B-43E0-B555-1AE9948BF9E9',
    @IdParent = NULL,
    @ItemKey = 'calendar',
    @Title = 'Calendario',
    @Icon = 'bi bi-calendar-event',
    @Url = '/calendar',
    @Target = NULL,
    @DisplayOrder = 55,
    @IsVisible = 1,
    @BadgeText = NULL,
    @BadgeColor = NULL;
GO
