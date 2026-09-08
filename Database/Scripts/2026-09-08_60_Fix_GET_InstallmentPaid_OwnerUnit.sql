-- =============================================================================
-- Fix: el modal "Cuotas asociadas al pago" (ReconciliationWorkspace.razor,
-- VerDetallesTransaccion) busca el propietario/unidad de cada cuota pagada en
-- cuotaspendientes (GetPendingInstallmentsAsync -> GET_PendingInstallments),
-- que solo trae cuotas CON SALDO PENDIENTE (Status <> 1). En cuanto una cuota
-- queda totalmente pagada, desaparece de esa lista, así que el lookup falla y
-- el modal cae al fallback "Cuota {IdInstallment}" (el GUID crudo) en vez del
-- nombre real -- y sin propietario no queda claro tampoco si fue un pago
-- total o parcial (aunque ese dato sí venía bien, solo que sin contexto era
-- ilegible).
--
-- UnitName y OwnerName ya son columnas propias de Installment (no vienen de
-- un JOIN a otra tabla -- GET_PendingInstallments las trae con SELECT i.*),
-- así que basta con agregarlas al SELECT de GET_InstallmentPaid vía el JOIN
-- a Installment que el procedure ya hace, sin tocar ninguna otra tabla.
--
-- Se preserva el resto del procedure tal cual está hoy en producción.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.GET_InstallmentPaid
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  id.*,
            i.UnitName,
            i.OwnerName
    FROM    InstallmentPaid id
    JOIN    Installment i ON id.IdInstallment = i.IdInstallment
    JOIN    BudgetHeader bh ON bh.IdBudgetHeader = i.IdBudgetHeader
    WHERE   bh.IdBuilding = @IdBuilding
END;
GO
