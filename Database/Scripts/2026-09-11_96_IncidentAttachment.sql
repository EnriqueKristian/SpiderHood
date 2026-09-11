-- =============================================================================
-- Fotos/video en Incidencias (Docs/Pendientes-Negocio-Consolidado.md #18a) --
-- hoy Incident no tiene ninguna columna para adjuntar nada. Mismo criterio que
-- ReceiptFile (#18b, Database/Scripts/2026-09-11_95_ReceiptFile.sql): el
-- archivo va a disco/storage vía IFileStorageService, acá se guarda sólo la
-- ruta + metadatos.
--
-- Tabla 100% nueva -- no toca nada existente. Estructura de carpeta acordada:
-- incidents/{IdBuilding}/{IdIncident}/{IdAttachment}.{ext} -- una carpeta por
-- incidente (puede tener varias fotos/videos), sin año/mes como nivel aparte
-- (el volumen de incidentes por edificio no lo justifica, a diferencia de
-- Recibos que son mensuales por unidad).
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'IncidentAttachment')
BEGIN
    CREATE TABLE dbo.IncidentAttachment
    (
        IdAttachment  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdIncident    UNIQUEIDENTIFIER NOT NULL,
        IdBuilding    UNIQUEIDENTIFIER NOT NULL,
        FileName      NVARCHAR(260)    NOT NULL,   -- nombre original del archivo subido
        ContentType   NVARCHAR(100)    NOT NULL,
        FileSizeBytes INT              NOT NULL,
        FilePath      NVARCHAR(500)    NOT NULL,   -- ruta relativa en el storage (IFileStorageService)
        UploadedBy    UNIQUEIDENTIFIER NOT NULL,
        UploadedOn    DATETIME2        NOT NULL
    );

    CREATE INDEX IX_IncidentAttachment_Incident ON dbo.IncidentAttachment (IdIncident);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_IncidentAttachment
    @IdAttachment UNIQUEIDENTIFIER,
    @IdIncident UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FileName NVARCHAR(260),
    @ContentType NVARCHAR(100),
    @FileSizeBytes INT,
    @FilePath NVARCHAR(500),
    @UploadedBy UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.IncidentAttachment
        (IdAttachment, IdIncident, IdBuilding, FileName, ContentType, FileSizeBytes, FilePath, UploadedBy, UploadedOn)
    VALUES
        (@IdAttachment, @IdIncident, @IdBuilding, @FileName, @ContentType, @FileSizeBytes, @FilePath, @UploadedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_IncidentAttachmentsByIncident
    @IdIncident UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.IdAttachment, a.IdIncident, a.IdBuilding, a.FileName, a.ContentType, a.FileSizeBytes,
           a.FilePath, a.UploadedBy, a.UploadedOn,
           uploader.FirstName + ' ' + uploader.LastName AS UploadedByName
    FROM dbo.IncidentAttachment a
    LEFT JOIN dbo.Users uploader ON uploader.IdUser = a.UploadedBy
    WHERE a.IdIncident = @IdIncident
    ORDER BY a.UploadedOn ASC;
END
GO
