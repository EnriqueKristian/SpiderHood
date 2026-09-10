-- SOLO LECTURA. Corré esto en la MISMA conexión/pestaña donde acabás de
-- ejecutar 2026-09-08_66 y _67, para descartar que la verificación esté
-- apuntando a otra base de datos.

SELECT DB_NAME() AS BaseActual, @@SERVERNAME AS ServidorActual;

-- Texto real, tal cual quedó guardado en el servidor, de los dos procedures.
-- Si esto muestra "SET     IdStatementDetail = ..." (y NO una línea con
-- "Status = @ReconciliationStatus" dentro del UPDATE Expense), el fix SÍ está
-- aplicado y el problema es 100% del script de verificación (que ya se
-- corrigió) o de que apuntaba a otra base.
SELECT 'UPD_ExpenseReconcilied' AS Proc, OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseReconcilied')) AS Definicion
UNION ALL
SELECT 'UPD_ExpenseDeReconcilied', OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseDeReconcilied'));

-- Chequeo booleano corregido (mismo que ya actualicé en el script 78)
SELECT
    'UPD_ExpenseReconcilied' AS Proc,
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseReconcilied')) LIKE '%[^a-zA-Z]Status[^a-zA-Z]%=%@ReconciliationStatus%'
         THEN 'FALTA CORRER' ELSE 'YA APLICADO' END AS Estado
UNION ALL
SELECT
    'UPD_ExpenseDeReconcilied',
    CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseDeReconcilied')) LIKE '%[^a-zA-Z]Status[^a-zA-Z]%=%@ReconciliationStatus%'
         THEN 'FALTA CORRER' ELSE 'YA APLICADO' END;
