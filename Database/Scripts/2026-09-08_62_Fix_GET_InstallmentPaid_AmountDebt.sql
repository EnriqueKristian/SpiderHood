-- =============================================================================
-- Complementa 2026-09-08_60 (que agregó UnitName/OwnerName a GET_InstallmentPaid):
-- el modal "Cuotas asociadas al pago" (ReconciliationWorkspace.razor,
-- VerDetallesTransaccion) mostraba únicamente el monto de ESTE pago
-- (InstallmentPaid.Amount) sin ningún punto de referencia -- con un pago
-- parcial no había forma de saber cuánto era la cuota total ni cuánto
-- quedaba pendiente después de aplicarlo.
--
-- Se agregan dos columnas más, mismo patrón que GET_PendingInstallments
-- (JOIN a vw_SUM_InstallmentPaid para el saldo real, sumando TODOS los pagos
-- de la cuota, no solo este):
--   InstallmentAmount -- monto total de la cuota (Installment.Amount)
--   InstallmentDebt   -- saldo pendiente de la cuota A LA FECHA (Amount - AmountPaid)
--
-- Se preserva el resto del procedure tal cual está hoy en producción.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.GET_InstallmentPaid
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  id.*,
            i.UnitName,
            i.OwnerName,
            i.Amount                                AS InstallmentAmount,
            i.Amount - ISNULL(vi.AmountPaid, 0)     AS InstallmentDebt
    FROM    InstallmentPaid id
    JOIN    Installment i ON id.IdInstallment = i.IdInstallment
    JOIN    BudgetHeader bh ON bh.IdBudgetHeader = i.IdBudgetHeader
    LEFT JOIN vw_SUM_InstallmentPaid vi ON i.IdInstallment = vi.IdInstallment
    WHERE   bh.IdBuilding = @IdBuilding
END;
GO
