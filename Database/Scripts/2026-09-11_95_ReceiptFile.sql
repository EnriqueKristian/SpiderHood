-- =============================================================================
-- Storage de recibos PDF (Docs/Pendientes-Negocio-Consolidado.md #18b) -- hoy
-- InstallmentExportService.GenerateReceipt/GenerateAllReceiptsZip
-- (Classes/Utilities.cs) genera el PDF 100% en memoria, desde cero, CADA VEZ
-- que alguien lo pide (MyReceipts.razor, InstallmentTable.razor,
-- InstallmentList.razor, BudgetGenerator.razor al publicar) -- nunca se
-- persiste. Peor: ComposeFooter arma el pie con la configuración VIGENTE del
-- edificio (cuenta bancaria, texto del pie, contacto del Administrador) al
-- momento de la descarga, no con la que era real cuando la cuota se emitió --
-- un recibo viejo cambia de contenido si el edificio cambia de banco o de
-- administrador. Bug de integridad en un documento financiero, no solo de
-- performance.
--
-- Diseño: el PDF generado se guarda como archivo en disco/storage (fuera de
-- la BD -- ver IFileStorageService), y esta tabla guarda sólo la RUTA +
-- metadatos, ligada 1:1 a la cuota que representa. IX_ReceiptFile_Installment
-- es UNIQUE a propósito: una vez generado un recibo, es INMUTABLE -- nunca se
-- vuelve a generar con datos nuevos, sólo se sirve el archivo ya guardado
-- (por eso no hay UPD_ReceiptFile, sólo INS_/GET_).
--
-- Tabla y SPs 100% nuevos -- no toca nada existente.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceiptFile')
BEGIN
    CREATE TABLE dbo.ReceiptFile
    (
        IdReceiptFile   UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdInstallment   UNIQUEIDENTIFIER NOT NULL,
        IdBuilding      UNIQUEIDENTIFIER NOT NULL,
        FilePath        NVARCHAR(500)    NOT NULL,
        FileSizeBytes   INT              NOT NULL,
        GeneratedBy     NVARCHAR(256)    NOT NULL,
        GeneratedOn     DATETIME2        NOT NULL
    );

    -- UNIQUE (no solo índice): un recibo por cuota, para siempre -- ver diseño
    -- arriba. INS_ReceiptFile confía en que la BD rechace un segundo INSERT
    -- (dos pestañas/doble click generando el mismo recibo a la vez).
    CREATE UNIQUE INDEX IX_ReceiptFile_Installment
        ON dbo.ReceiptFile (IdInstallment);

    CREATE INDEX IX_ReceiptFile_Building
        ON dbo.ReceiptFile (IdBuilding);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_ReceiptFile
    @IdReceiptFile UNIQUEIDENTIFIER,
    @IdInstallment UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @FilePath NVARCHAR(500),
    @FileSizeBytes INT,
    @GeneratedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ReceiptFile
        (IdReceiptFile, IdInstallment, IdBuilding, FilePath, FileSizeBytes, GeneratedBy, GeneratedOn)
    VALUES
        (@IdReceiptFile, @IdInstallment, @IdBuilding, @FilePath, @FileSizeBytes, @GeneratedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ReceiptFileByInstallment
    @IdInstallment UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdReceiptFile, IdInstallment, IdBuilding, FilePath, FileSizeBytes, GeneratedBy, GeneratedOn
    FROM dbo.ReceiptFile
    WHERE IdInstallment = @IdInstallment;
END
GO
