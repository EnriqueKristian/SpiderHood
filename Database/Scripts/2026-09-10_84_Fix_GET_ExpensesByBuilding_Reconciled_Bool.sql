-- =============================================================================
-- Fix: GET_ExpensesByBuilding devolvía "Reconciled" como el STRING literal
-- 'TRUE'/'FALSE' (IIF(...) IS NOT NULL, 'TRUE', 'FALSE'), no un bit real.
-- ViewExpense.Reconciled es un bool de verdad -- EF/SqlClient no puede
-- convertir un string a bool, tira "Unable to cast object of type
-- 'System.String' to type 'System.Boolean'" apenas se lee la primera fila.
--
-- Este bug ya estaba en el SP original (antes de _82/_83) -- nunca se había
-- notado porque Classes/Expense.cs (lo que se usaba hasta ahora) no tiene
-- ninguna propiedad llamada "Reconciled" que EF intentara mapear ahí. Recién
-- se vuelve visible al usar ViewExpense (la clase correcta, ver _83), que sí
-- tiene esa propiedad.
--
-- De paso, se blindan con ISNULL las demás columnas NULLABLE en la tabla real
-- (AutoReconcile, Status, PaymentMethod) que se mapean a propiedades NO
-- nullable en ViewExpense.cs -- mismo tipo de bug, todavía no disparado
-- porque los datos de prueba no tienen NULLs ahí, pero más vale evitarlo
-- ahora que arreglar otro "Invalid cast" en la próxima vuelta.
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
    ISNULL(e.PaymentMethod, 6)                                          AS PaymentMethod,
    ISNULL(e.Status, 0)                                                 AS Status,
    CAST(IIF(e.IdStatementDetail IS NOT NULL, 1, 0) AS BIT)             AS Reconciled,
    ISNULL(e.IdStatementDetail,'00000000-0000-0000-0000-000000000000')  AS ReconciledTransactionId,
    e.Distribution,
    e.IdBuilding,
    ISNULL(e.Notes,'-')                                                 AS Notes,
    ISNULL(e.AutoReconcile, 1)                                          AS AutoReconcile,
    e.ExpenseDate,
    e.IncludeInQuota
    FROM Expense e
    LEFT JOIN Category c ON c.IdCategory = e.IdCategory
    WHERE e.IdBuilding = @IdBuilding
      AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)
END
GO
