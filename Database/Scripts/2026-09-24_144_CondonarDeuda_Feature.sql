-- =============================================================================
-- Feature "Condonar Deuda": permite marcar una cuota pendiente como perdonada
-- (Installment.Status = 4 / Condonada, ver enum ReconciliationType) sin crear
-- ningún InstallmentPaid ni asociar ninguna transacción bancaria real. El
-- motivo/quién/cuándo quedan en WorkflowAuditEntry (WorkflowAction.Waived), no
-- en la cuota misma -- mismo patrón que ya usa la reversión de conciliación.
--
-- Además crea el permiso "revertir_conciliacion_masivo" para poder deshacer la
-- conciliación de varias cuotas a la vez (todo un archivo cargado, o las de las
-- unidades elegidas) -- hasta ahora RevertPaymentAsync solo se podía invocar de
-- a una transacción por vez.
--
-- Ambos permisos son nuevos y quedan sin asignar a ningún rol por defecto
-- (mismo patrón que 2026-09-22_137_Seed_EditAccountPermission.sql y
-- 2026-09-11_92_Seed_ApproveExpensesPermission.sql) -- asignarlos a los roles
-- correspondientes (típicamente Administrador y SysAdmin) sigue siendo manual
-- desde /roles (Configuración > Roles y Permisos), a propósito: son acciones
-- financieras sensibles que no deberían heredarse automáticamente.
--
-- Idempotente: se puede correr más de una vez.
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'condonar_deuda')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'condonar_deuda', 'Condonar Deuda', 'Marcar una cuota pendiente como condonada (perdonada), sin registrar un pago real', 'reconciliation');

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionKey = 'revertir_conciliacion_masivo')
INSERT INTO dbo.Permissions (PermissionId, PermissionKey, Name, Description, [Group])
VALUES (NEWID(), 'revertir_conciliacion_masivo', 'Deshacer Conciliación en Masa', 'Deshacer la conciliación de varias cuotas a la vez -- todo un archivo cargado o las unidades elegidas', 'reconciliation');
GO

-- SET QUOTED_IDENTIFIER ON antes del CREATE -- SQL Server graba este setting al crear
-- el procedure, no al ejecutarlo; sin esto el UPDATE fallaba con Msg 1934 (Installment
-- tiene una vista indexada/columna computada que lo exige), aunque el resto del script
-- corra bien. Mismo motivo que ya se documentó para INSERTs directos contra esta tabla.
SET QUOTED_IDENTIFIER ON;
GO

-- Nuevo SP: condona una cuota. A diferencia de UPD_InstallmentState, no recibe
-- ninguna transacción bancaria -- condonar es, por definición, no cobrar nada.
CREATE OR ALTER PROCEDURE dbo.UPD_InstallmentCondonar
    @IdInstallment UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Installment
    SET Status = 4,
        UpdatedDate = GETDATE()
    WHERE IdInstallment = @IdInstallment;
END
GO

-- GET_PendingInstallments: una cuota condonada no debe seguir contando como
-- deuda pendiente en ningún lado -- Listado de Cuotas (Deuda Total), Dashboard
-- (Pendiente de Pago / Morosidad), Reporte de Morosidad y "Deudas Anteriores"
-- del detalle/recibo consumen todos esta misma consulta, así que el fix acá
-- cubre todo de una vez. Antes: WHERE i.Status <> 1. Ahora: tampoco Status = 4.
ALTER PROCEDURE [dbo].[GET_PendingInstallments]
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
    WHERE   i.Status NOT IN (1, 4) AND BudgetHeader.IdBuilding = @IdBuilding
    ORDER BY BudgetHeader.BudgetDate, i.number
END
GO
