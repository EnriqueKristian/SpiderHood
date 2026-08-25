/*
    Corrige dbo.UPD_ClosePastBudgets (Presupuestos, regla de negocio: "solo puede
    existir un presupuesto activo, el nuevo debe cerrar el anterior abierto").

    Problema con la versión actual:
        UPDATE BudgetHeader
        SET Status = 6 -- CERRADO
        WHERE BudgetDate < @Period AND IdBuilding = @IdBuilding

    No filtra por Status, así que:
      - Reclasifica silenciosamente presupuestos Rechazados (Status = 5) a
        Cerrado (Status = 6), perdiendo el historial de que fueron rechazados.
      - Vuelve a tocar (UpdateDate = GETDATE()) presupuestos que ya estaban
        Cerrados cada vez que se ejecuta, sin necesidad.

    Este script agrega "AND Status NOT IN (5, 6)" para que solo cierre
    presupuestos que efectivamente estaban abiertos (Created/Check/Approved/
    Active), sin tocar los que ya están Cerrados o fueron Rechazados.

    Nota: el otro problema encontrado (el proc se llamaba demasiado temprano,
    desde el paso Created->Check en vez de recién al llegar a Active) ya se
    corrigió del lado de C# en Services/IBudgetService.cs — no requiere cambios
    de SQL.
*/

ALTER PROCEDURE dbo.UPD_ClosePastBudgets
    @Period     DATETIME,
    @IdBuilding UNIQUEIDENTIFIER
AS
    UPDATE  BudgetHeader
    SET     Status     = 6,  --CERRADO
            UpdateDate = GETDATE()
    WHERE   [BudgetDate] < @Period
            AND IdBuilding = @IdBuilding
            AND Status NOT IN (5, 6); -- no tocar Rechazados (5) ni ya Cerrados (6)
GO
