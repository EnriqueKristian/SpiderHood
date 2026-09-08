-- =============================================================================
-- Complementa 2026-09-08_60/_62: el modal "Cuotas asociadas al pago" ahora
-- muestra el monto de la cuota y el saldo pendiente, pero con varias cuotas
-- de la misma unidad (atrasos acumulados) seguía sin poder distinguir a qué
-- PERIODO correspondía cada una -- mismo problema que ya se resolvió para
-- ReconcilePaymentModal.razor (2026-09-08, commit "modal Conciliar Pago con
-- Cuota(s) muestra el periodo"), pero ahí faltaba en el modal de sólo lectura.
--
-- Se agrega BudgetHeader.BudgetDate AS Period, igual que ya lo hace
-- GET_PendingInstallments para Installment.Period.
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
            i.Amount - ISNULL(vi.AmountPaid, 0)     AS InstallmentDebt,
            bh.BudgetDate                            AS Period
    FROM    InstallmentPaid id
    JOIN    Installment i ON id.IdInstallment = i.IdInstallment
    JOIN    BudgetHeader bh ON bh.IdBudgetHeader = i.IdBudgetHeader
    LEFT JOIN vw_SUM_InstallmentPaid vi ON i.IdInstallment = vi.IdInstallment
    WHERE   bh.IdBuilding = @IdBuilding
END;
GO
