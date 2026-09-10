-- =============================================================================
-- Fix grande, en dos partes, encontrado probando /expense con datos reales:
--
-- 1) UPD_Expense referenciaba columnas que NO EXISTEN en dbo.Expense
--    (TotalAmount, IsInstallment, TypeDistribution) -- las reales son
--    Amount, IncludeInQuota, Distribution (confirmado con
--    INFORMATION_SCHEMA.COLUMNS). Cualquier intento de editar un gasto desde
--    /expense fallaba con "Invalid column name" apenas se guardaba -- nunca
--    funcionó. De paso se agrega @ExpenseDate (el modal tiene un campo
--    "Fecha" que hoy no se persiste al editar).
--
-- 2) GET_ExpensesByBuilding (ya arreglado en el script _82 para
--    IdStatementDetail) no devolvía el NOMBRE de la categoría -- sólo
--    IdCategory. Classes/Budget/ViewExpense.cs (la clase correcta para esta
--    tabla, ver hallazgo completo en la sesión) tiene una propiedad Category
--    (string) que EF exige que venga en el resultado. Se agrega un JOIN a
--    Category para traer ShortDescript AS Category. Este script reemplaza
--    por completo al _82 (incluye ese mismo fix de IdStatementDetail) --
--    alcanza con correr este.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_ExpensesByBuilding
    @IdBuilding     UNIQUEIDENTIFIER,
    @StartDate      DATE = NULL,
    @EndDate        DATE = NULL
AS
BEGIN
    SELECT
    e.IdExpense,
    e.Description,
    e.Amount,
    e.IdCategory,
    ISNULL(c.ShortDescript,'')                                          AS Category,
    ISNULL(e.Supplier,'')                                               AS Supplier,
    e.PaymentMethod,
    e.Status,
    IIF(e.IdStatementDetail IS NOT NULL, 'TRUE', 'FALSE')               AS Reconciled,
    ISNULL(e.IdStatementDetail,'00000000-0000-0000-0000-000000000000')  AS ReconciledTransactionId,
    e.Distribution,
    e.IdBuilding,
    ISNULL(e.Notes,'-')                                                 AS Notes,
    e.AutoReconcile,
    e.ExpenseDate,
    e.IncludeInQuota
    FROM Expense e
    LEFT JOIN Category c ON c.IdCategory = e.IdCategory
    WHERE e.IdBuilding = @IdBuilding
      AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Expense
    @IdExpense       UNIQUEIDENTIFIER,
    @Description     VARCHAR(255),
    @Amount          DECIMAL(10,2),
    @Distribution    INT,
    @IncludeInQuota  BIT,
    @IdCategory      UNIQUEIDENTIFIER,
    @ExpenseDate     DATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Expense
    SET Description = @Description,
        Amount = @Amount,
        Distribution = @Distribution,
        IncludeInQuota = @IncludeInQuota,
        IdCategory = @IdCategory,
        ExpenseDate = @ExpenseDate
    WHERE IdExpense = @IdExpense;
END;
GO
