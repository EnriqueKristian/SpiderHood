-- =============================================================================
-- Logs de sistema: niveles (Critical/Error/Warning/Information), toggle de
-- Super Usuario (para no llenar la BD si nadie lo necesita) y purga por
-- retención.
--
-- Tablas y SPs 100% nuevos. Idempotente.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemLog')
BEGIN
    CREATE TABLE dbo.SystemLog
    (
        Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [Timestamp] DATETIME2        NOT NULL,
        Level       NVARCHAR(20)     NOT NULL,   -- Critical/Error/Warning/Information
        Category    NVARCHAR(200)    NOT NULL,   -- Categoría del ILogger (nombre de clase)
        Message     NVARCHAR(MAX)    NOT NULL,
        Exception   NVARCHAR(MAX)    NULL,
        IdUser      UNIQUEIDENTIFIER NULL,
        UserName    NVARCHAR(256)    NULL,
        IdBuilding  UNIQUEIDENTIFIER NULL
    );

    CREATE INDEX IX_SystemLog_Timestamp ON dbo.SystemLog ([Timestamp] DESC);
END
GO

-- Fila única global (Id = 1) -- no es por edificio: es una config de sistema,
-- del Super Usuario (SysAdmin).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SystemLogSettings')
BEGIN
    CREATE TABLE dbo.SystemLogSettings
    (
        Id            INT           NOT NULL PRIMARY KEY CHECK (Id = 1),
        IsEnabled     BIT           NOT NULL DEFAULT (0),
        MinLevel      NVARCHAR(20)  NOT NULL DEFAULT ('Error'),
        RetentionDays INT           NOT NULL DEFAULT (30),
        UpdatedBy     NVARCHAR(256) NULL,
        UpdatedOn     DATETIME2     NULL
    );

    INSERT INTO dbo.SystemLogSettings (Id, IsEnabled, MinLevel, RetentionDays)
    VALUES (1, 0, 'Error', 30);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_SystemLog
    @Id UNIQUEIDENTIFIER,
    @Timestamp DATETIME2,
    @Level NVARCHAR(20),
    @Category NVARCHAR(200),
    @Message NVARCHAR(MAX),
    @Exception NVARCHAR(MAX) = NULL,
    @IdUser UNIQUEIDENTIFIER = NULL,
    @UserName NVARCHAR(256) = NULL,
    @IdBuilding UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.SystemLog (Id, [Timestamp], Level, Category, Message, Exception, IdUser, UserName, IdBuilding)
    VALUES (@Id, @Timestamp, @Level, @Category, @Message, @Exception, @IdUser, @UserName, @IdBuilding);
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_SystemLogSettings
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, IsEnabled, MinLevel, RetentionDays, UpdatedBy, UpdatedOn
    FROM dbo.SystemLogSettings
    WHERE Id = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_SystemLogSettings
    @IsEnabled BIT,
    @MinLevel NVARCHAR(20),
    @RetentionDays INT,
    @UpdatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.SystemLogSettings
    SET IsEnabled = @IsEnabled,
        MinLevel = @MinLevel,
        RetentionDays = @RetentionDays,
        UpdatedBy = @UpdatedBy,
        UpdatedOn = SYSUTCDATETIME()
    WHERE Id = 1;
END
GO

-- Lista de los logs mas recientes (paginado se hace client-side en la UI, ver
-- PaginationClass<T>, igual que el resto de grillas de la app) -- @Top acota
-- cuanto se trae de una vez para no cargar toda la tabla.
CREATE OR ALTER PROCEDURE dbo.GET_SystemLogs_Recent
    @Top INT = 500
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@Top) Id, [Timestamp], Level, Category, Message, Exception, IdUser, UserName, IdBuilding
    FROM dbo.SystemLog
    ORDER BY [Timestamp] DESC;
END
GO

-- Purga por retención (ver SystemLogPurgeService, corre 1 vez al dia).
CREATE OR ALTER PROCEDURE dbo.DEL_SystemLogOlderThan
    @CutoffDate DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.SystemLog WHERE [Timestamp] < @CutoffDate;
END
GO
