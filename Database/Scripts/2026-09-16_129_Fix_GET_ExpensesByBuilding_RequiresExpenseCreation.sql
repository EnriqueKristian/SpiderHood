-- =============================================================================
-- Fix: GET_ExpensesByBuilding (usado por ExpensePage.razor vía
-- IExpenseService.GetExpensesByBuildingAsync -- la pantalla /expense) no traía
-- la columna RequiresExpenseCreation que se agregó a ViewExpense en
-- Database/Scripts/2026-09-14_100_SinCategorizar_Category_GastosBanco.sql --
-- FromSqlRaw<ViewExpense> exige TODAS las columnas mapeadas de la entidad, así
-- que CUALQUIER llamada a este SP tira "The required column
-- 'RequiresExpenseCreation' was not present in the results of a 'FromSql'
-- operation" -- /expense no carga para ningún edificio hoy.
--
-- El propio comentario de Database/Scripts/2026-09-14_101_Fix_GET_
-- PendingConciliationExpenses_RequiresExpenseCreation.sql ya avisaba de este
-- caso pendiente: "Esta SP sólo lee de dbo.Expense (nunca de
-- AccountStatementDetail) ... siempre 0/false, igual que la rama real de
-- GET_ExpensesByBuilding" -- ese fix nunca se replicó acá. Mismo criterio:
-- GET_ExpensesByBuilding también sólo lee dbo.Expense (nunca la fila
-- "virtual" de un egreso bancario sin categorizar), así que
-- RequiresExpenseCreation es siempre 0/false acá también.
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
    e.IncludeInQuota,
    CAST(0 AS BIT)                                                      AS RequiresExpenseCreation
  FROM Expense e
    LEFT JOIN Category c ON c.IdCategory = e.IdCategory
    WHERE e.IdBuilding = @IdBuilding
      AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)
END
GO
