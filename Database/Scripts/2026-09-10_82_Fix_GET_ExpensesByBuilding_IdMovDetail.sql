-- =============================================================================
-- Fix: GET_ExpensesByBuilding tiraba "Invalid column name 'IdMovDetail'" al
-- entrar a /expense.
--
-- Causa: el SP referenciaba dbo.Expense.IdMovDetail, columna que no existe en
-- la tabla real. La columna real de reconciliación es IdStatementDetail --
-- confirmado por UPD_ExpenseReconcilied/UPD_ExpenseDeReconcilied (que hacen
-- SET IdStatementDetail = @IdStatementDetail) y por GET_PendingConciliationExpenses
-- (que filtra "WHERE e.IdStatementDetail IS NULL"), ambos ya versionados/
-- confirmados en sesiones anteriores. Se corrige el nombre de columna, sin
-- tocar el resto de la lógica del SP.
--
-- IMPORTANTE (sin corregir en este script, requiere más info): el resto de
-- las columnas que devuelve este SP (Description, Amount, Supplier,
-- PaymentMethod, Status, Notes, AutoReconcile, ExpenseDate, IncludeInQuota)
-- coinciden con la forma de Classes/Budget/ViewExpense.cs, NO con
-- Classes/Expense.cs (que espera ExpenseDescription, TotalAmount, Provider,
-- DueDate, SubCategory, IdSubCategory, IdDistribution, Pagado, Observaciones,
-- IsReconciled) -- pero BDLayout.GetExpensesByBuildingAsync mapea el
-- resultado a List<Expense>, no List<ViewExpense>. Es probable que después de
-- este fix aparezca un nuevo error de EF tipo "required column ... was not
-- present" para alguna de esas columnas -- ver Docs/Pendientes-Negocio-Reportes.md
-- o el hilo de esta sesión para el diagnóstico completo antes de tocar nada más.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_ExpensesByBuilding
    @IdBuilding     UNIQUEIDENTIFIER,
    @StartDate      DATE = NULL,             -- opcional
    @EndDate        DATE = NULL              -- opcional
AS
BEGIN
    SELECT
    e.IdExpense,
    e.Description,
    e.Amount,
    e.IdCategory,
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
    WHERE e.IdBuilding = @IdBuilding
      AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)
END
GO
