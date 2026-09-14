-- =============================================================================
-- Fix: GET_PendingConciliationExpenses (usado por IBudgetService para el panel
-- de "Gastos Pendientes" del presupuesto, ver GetPendingExpensesToApplyAsync)
-- no traía la columna RequiresExpenseCreation que se agregó a ViewExpense en
-- Database/Scripts/2026-09-14_100_SinCategorizar_Category_GastosBanco.sql --
-- FromSqlRaw<ViewExpense> exige TODAS las columnas mapeadas de la entidad,
-- así que cualquier llamada a este SP tiraba "The required column
-- 'RequiresExpenseCreation' was not present in the results of a 'FromSql'
-- operation" -- rompía por completo la pantalla de Presupuesto (BudgetGenerator),
-- no sólo el flujo de Sin Categorizar que agregó la columna.
--
-- Esta SP sólo lee de dbo.Expense (nunca de AccountStatementDetail), así que
-- ninguna fila acá es la fila "virtual" de un egreso bancario sin categorizar
-- -- siempre 0/false, igual que la rama real de GET_ExpensesByBuilding.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_PendingConciliationExpenses]
@IdBuilding     UNIQUEIDENTIFIER,
@StartDate      DATE = NULL,             -- opcional
@EndDate        DATE = NULL              -- opcional
AS
BEGIN
    DECLARE @False BIT
    DECLARE @True BIT
    SET @False = 0
    SET @True = 1

    SELECT
    e.IdExpense,
    e.Description,
    e.Amount,
    e.IdCategory,
    c.shortDescript                                                  AS Category,
    ISNULL(e.Supplier,'')                                               AS Supplier,
    e.PaymentMethod,
    e.Status,
    IIF(e.IdStatementDetail IS NOT NULL, @True, @False)                     AS Reconciled,
    ISNULL(e.IdStatementDetail,'00000000-0000-0000-0000-000000000000')        AS ReconciledTransactionId,
    e.Distribution,
    e.IdBuilding,
    ISNULL(e.Notes,'-')                                                 AS Notes,
    e.AutoReconcile,
    e.ExpenseDate,
    e.IncludeInQuota,
    @False                                                              AS RequiresExpenseCreation
    FROM Expense e
    JOIN    Category c ON e.IdCategory = c.Idcategory
    WHERE e.IdBuilding = @IdBuilding AND e.IdStatementDetail IS NULL
      --AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      --AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)
END
GO
