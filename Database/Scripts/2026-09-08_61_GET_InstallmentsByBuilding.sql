-- =============================================================================
-- Fix: "Listado de Cuotas" (InstallmentList.razor, /cuotas) carga su data con
-- GetPendingInstallmentsAsync -> GET_PendingInstallments, que trae SOLO cuotas
-- con Status <> 1 (i.e. no Conciliada) -- el filtro "Pagadas" del combo
-- "Todos los estados" es puramente client-side (OnFiltroEstadoChanged, línea
-- ~452 de InstallmentList.razor), así que nunca tiene nada que mostrar: las
-- cuotas pagadas ni siquiera llegan al browser. Lo mismo afecta los 4
-- indicadores de arriba (Unidades/Total Cuotas/Total Pagado/Deuda Total),
-- todos calculados sobre esa misma lista incompleta.
--
-- Se agrega un procedure nuevo (no se toca GET_PendingInstallments, que sigue
-- usándose tal cual para Home.razor.cs y ExtraChargeService, donde sí se
-- quiere solo pendientes) con el mismo SELECT pero sin el filtro de Status,
-- para que /cuotas pueda listar y filtrar por cualquier estado.
-- =============================================================================

CREATE OR ALTER PROCEDURE dbo.GET_InstallmentsByBuilding
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  i.*,
            BudgetHeader.BudgetDate                 AS [Period],
            ISNULL(vi.AmountPaid,0)                 AS [AmountPaid],
		    i.Amount - ISNULL(vi.AmountPaid,0)      AS [Debt]
    FROM    Installment i
    JOIN    BudgetHeader ON i.IdBudgetHeader = BudgetHeader.IdBudgetHeader
    LEFT JOIN vw_SUM_InstallmentPaid vi ON i.IdInstallment = vi.IdInstallment
    WHERE   BudgetHeader.IdBuilding = @IdBuilding
    ORDER BY BudgetHeader.BudgetDate, i.number
END;
GO
