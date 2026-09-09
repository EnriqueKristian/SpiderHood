-- =============================================================================
-- Mismo bug que 2026-09-08_66 (UPD_ExpenseReconcilied), acá en su contraparte de
-- "desconciliar": pisaba Expense.Status (estado de APROBACIÓN del gasto --
-- Pending/Approved/Rejected) con @ReconciliationStatus (un ConcilationType).
-- Al llamarse siempre con @ReconciliationStatus = NoConciliada (0), esto
-- resetearía el estado de aprobación de CUALQUIER gasto a lo que sea que
-- StatusExpense mapee a 0, sin importar si estaba Approved o Rejected.
--
-- La conciliación de un gasto se rastrea con Expense.IdStatementDetail (NULL =
-- no conciliado, confirmado por GET_PendingConciliationExpenses), que este
-- procedure ya pone en NULL correctamente. Solo se quita la línea que tocaba
-- Status.
--
-- Se conecta como parte de "Corregir" (Fase B) -- antes tampoco se llamaba
-- desde ningún lado, así que no hay datos que reparar.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.UPD_ExpenseDeReconcilied
    @IdStatementDetail      UNIQUEIDENTIFIER,
    @ReconciliationStatus   INT,
    @IdExpense              UNIQUEIDENTIFIER,
    @AutoReconciliate       BIT
AS
BEGIN
    UPDATE  AccountStatementDetail
    SET     ReconciliationStatus = @ReconciliationStatus,
            ReconciliationDate = NULL
    WHERE   IdStatementDetail = @IdStatementDetail;

    UPDATE  Expense
    SET     IdStatementDetail = NULL,
            AutoReconcile = @AutoReconciliate
    WHERE   IdExpense = @IdExpense
END;
GO
