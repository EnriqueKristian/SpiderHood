-- =============================================================================
-- "Guardar como plantilla para transacciones similares" (Docs/Pendientes-Negocio-
-- Conciliacion.md #5) -- el checkbox existía en CreateExpenseFromTransactionModal
-- pero no tenía ninguna funcionalidad detrás; se había sacado por eso. El usuario
-- pidió implementarlo de verdad.
--
-- Diseño acordado con el usuario:
-- - Match por PREFIJO de la descripción del banco (DescriptionPattern), no exacto
--   ni fuzzy -- simple y predecible. Al buscar, se toma el patrón MÁS LARGO que
--   matchee (más específico), para desempatar si hay más de uno.
-- - Sólo PRE-LLENA el formulario de "Crear Gasto desde Transacción" -- nunca crea
--   ni concilia nada solo. El usuario sigue revisando y confirmando a mano.
-- - v1 sin pantalla de gestión: guardar el checkbox hace upsert por
--   (IdBuilding, DescriptionPattern) -- volver a guardar la misma descripción con
--   otra categoría/distribución actualiza la plantilla existente en vez de
--   duplicarla.
--
-- Tabla y SPs 100% nuevos -- no toca nada existente.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ExpenseTemplate')
BEGIN
    CREATE TABLE dbo.ExpenseTemplate
    (
        IdExpenseTemplate  UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        IdBuilding         UNIQUEIDENTIFIER NOT NULL,
        DescriptionPattern NVARCHAR(500)    NOT NULL,
        IdCategory         UNIQUEIDENTIFIER NOT NULL,
        Distribution       INT              NOT NULL,
        Supplier           NVARCHAR(256)    NULL,
        CreatedBy          NVARCHAR(256)    NOT NULL,
        CreatedOn          DATETIME2        NOT NULL,
        ModifiedBy         NVARCHAR(256)    NULL,
        ModifiedOn         DATETIME2        NULL
    );

    CREATE INDEX IX_ExpenseTemplate_Building
        ON dbo.ExpenseTemplate (IdBuilding);
END
GO

CREATE OR ALTER PROCEDURE dbo.INS_ExpenseTemplate
    @IdExpenseTemplate UNIQUEIDENTIFIER,
    @IdBuilding UNIQUEIDENTIFIER,
    @DescriptionPattern NVARCHAR(500),
    @IdCategory UNIQUEIDENTIFIER,
    @Distribution INT,
    @Supplier NVARCHAR(256) = NULL,
    @CreatedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ExpenseTemplate
        (IdExpenseTemplate, IdBuilding, DescriptionPattern, IdCategory, Distribution, Supplier, CreatedBy, CreatedOn)
    VALUES
        (@IdExpenseTemplate, @IdBuilding, @DescriptionPattern, @IdCategory, @Distribution, @Supplier, @CreatedBy, SYSUTCDATETIME());
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_ExpenseTemplate
    @IdExpenseTemplate UNIQUEIDENTIFIER,
    @IdCategory UNIQUEIDENTIFIER,
    @Distribution INT,
    @Supplier NVARCHAR(256) = NULL,
    @ModifiedBy NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ExpenseTemplate
    SET IdCategory = @IdCategory,
        Distribution = @Distribution,
        Supplier = @Supplier,
        ModifiedBy = @ModifiedBy,
        ModifiedOn = SYSUTCDATETIME()
    WHERE IdExpenseTemplate = @IdExpenseTemplate;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_ExpenseTemplatesByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT IdExpenseTemplate, IdBuilding, DescriptionPattern, IdCategory, Distribution, Supplier, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
    FROM dbo.ExpenseTemplate
    WHERE IdBuilding = @IdBuilding;
END
GO
