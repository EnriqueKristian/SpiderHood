-- SOLO LECTURA. Imprime el texto COMPLETO de ambos procedures (sin el
-- truncado a ~250 caracteres que hace el grid de resultados de SSMS) --
-- mirar la pestaña "Messages"/"Mensajes" de SSMS para verlo, no "Results".
-- Además ubica cada aparición de la palabra suelta "Status" (no como parte de
-- "ReconciliationStatus"/"IdStatementDetail"/etc.) para revisar el bug a ojo,
-- sin depender de ningún LIKE.

DECLARE @def1 NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseReconcilied'));
DECLARE @def2 NVARCHAR(MAX) = OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseDeReconcilied'));

PRINT '=========== UPD_ExpenseReconcilied (texto completo) ===========';
PRINT @def1;
PRINT '';
PRINT '=========== UPD_ExpenseDeReconcilied (texto completo) ===========';
PRINT @def2;

-- Ubica "Status" como palabra suelta (no pegada a otra letra antes/después)
-- y muestra 40 caracteres de contexto alrededor de cada aparición. Usa
-- sys.messages como tabla de números (siempre tiene miles de filas) en vez
-- de un CROSS JOIN cuyo tamaño no se puede garantizar de antemano.
;WITH Nums AS (
    SELECT n FROM (SELECT ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n FROM sys.messages) t
    WHERE n <= 4000
)
SELECT 'UPD_ExpenseReconcilied' AS ProcName, n AS Posicion,
       SUBSTRING(@def1, CASE WHEN n > 20 THEN n - 20 ELSE 1 END, 60) AS Contexto
FROM Nums
WHERE n <= LEN(@def1)
  AND SUBSTRING(@def1, n, 6) = 'Status'
  AND (n = 1 OR SUBSTRING(@def1, n - 1, 1) NOT LIKE '[a-zA-Z]')
  AND (n + 6 > LEN(@def1) OR SUBSTRING(@def1, n + 6, 1) NOT LIKE '[a-zA-Z]')
UNION ALL
SELECT 'UPD_ExpenseDeReconcilied', n,
       SUBSTRING(@def2, CASE WHEN n > 20 THEN n - 20 ELSE 1 END, 60)
FROM Nums
WHERE n <= LEN(@def2)
  AND SUBSTRING(@def2, n, 6) = 'Status'
  AND (n = 1 OR SUBSTRING(@def2, n - 1, 1) NOT LIKE '[a-zA-Z]')
  AND (n + 6 > LEN(@def2) OR SUBSTRING(@def2, n + 6, 1) NOT LIKE '[a-zA-Z]');
