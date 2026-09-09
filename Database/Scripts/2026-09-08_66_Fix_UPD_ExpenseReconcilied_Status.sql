-- =============================================================================
-- Fix: UPD_ExpenseReconcilied pisaba Expense.Status (estado de APROBACIÓN del
-- gasto -- Pending/Approved/Rejected, ver ViewExpense.Status/StatusExpense y
-- GET_ExpensesByBuilding/GET_PendingConciliationExpenses, que lo devuelven tal
-- cual) con el valor de @ReconciliationStatus (un ConcilationType --
-- NoConciliada/Conciliada/Parcial/Pendiente). Son dos conceptos completamente
-- distintos que comparten nombre de columna por casualidad.
--
-- La conciliación de un gasto NO se rastrea con Expense.Status -- se rastrea
-- con Expense.IdStatementDetail (NULL = no conciliado; confirmado por
-- GET_PendingConciliationExpenses, que filtra "WHERE e.IdStatementDetail IS
-- NULL"), que este mismo procedure ya actualiza correctamente en la misma
-- sentencia. Sólo se quita la línea que tocaba Status.
--
-- Nunca se ejecutó en producción con datos reales (el único caller,
-- ConciliarTransaccionAsync, estaba comentado en ReconciliationWorkspace.razor
-- -- se conecta recién en este mismo cambio), así que no hay que reparar
-- ningún Expense.Status ya corrompido.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.UPD_ExpenseReconcilied
    @IdStatementDetail      UNIQUEIDENTIFIER,
    @ReconciliationStatus   INT,
    @ReconciliationDate     DATE,
    @IdExpense              UNIQUEIDENTIFIER,
    @AutoReconciliate       BIT
AS
BEGIN
    UPDATE  AccountStatementDetail
    SET     ReconciliationStatus = @ReconciliationStatus,
            ReconciliationDate = @ReconciliationDate
    WHERE   IdStatementDetail = @IdStatementDetail;

    UPDATE  Expense
    SET     IdStatementDetail = @IdStatementDetail,
            AutoReconcile = @AutoReconciliate
    WHERE   IdExpense = @IdExpense
END;
GO
