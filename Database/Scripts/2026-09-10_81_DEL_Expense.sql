-- =============================================================================
-- Nuevo: DEL_Expense -- borrado real de un Gasto (dbo.Expense).
--
-- Pedido por el usuario: el botón "Eliminar" de /expense (Resumen de Gastos)
-- no tenía ningún SP de borrado detrás (IExpenseService.DeleteExpenseAsync no
-- existía) -- el click no hacía absolutamente nada.
--
-- Mismo patrón que DEL_Category (2026-09-02_24_Category_RealFK.sql): un DELETE
-- simple por Id, sin intentar adivinar ni tocar relaciones que no se pudieron
-- confirmar desde este entorno (sin acceso a BD). Si dbo.Expense tiene alguna
-- FK real apuntándole desde otra tabla, este DELETE falla con error SQL 547
-- (atrapado en ExpenseService.DeleteExpenseAsync) en vez de dejar datos
-- huérfanos -- no hace falta que este script sepa cuáles son esas tablas.
--
-- Guard adicional del lado de la aplicación (no acá, a propósito): la UI
-- (ExpensePage.razor) ya bloquea el intento de borrado si Expense.IsReconciled
-- es true, antes de siquiera llamar a este SP -- se evitó agregar la misma
-- condición acá porque no se pudo confirmar contra el schema real si la
-- columna que alimenta ese flag en GET_ExpensesByBuilding es la misma que
-- usan UPD_ExpenseReconcilied/UPD_ExpenseDeReconcilied (ahí el campo se llama
-- IdStatementDetail; Classes/Expense.cs expone un IdMovDetail que podría ser
-- un mapeo distinto o ya en desuso) -- mismo cuidado de siempre: no tocar un
-- SP/columna existente a ciegas.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.DEL_Expense
    @IdExpense UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Expense WHERE IdExpense = @IdExpense;
END
GO
