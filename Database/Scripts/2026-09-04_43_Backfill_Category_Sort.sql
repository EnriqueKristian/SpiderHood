-- =============================================================================
-- Backfill de datos -- no toca esquema/procs. BDLayout.Add.cs mandaba
-- category.Nivel en el lugar de @Sort al llamar INS_Category (bug de código,
-- corregido aparte). Como el Nivel de una categoría raíz siempre es 0, TODA
-- categoría raíz creada hasta ahora quedó con Sort = 0 en la tabla Category
-- (confirmado por Enrique: SELECT Description, nivel, Sort, ParentId FROM
-- view_categories -- Sort = 0 en las 25 filas, tanto raíces como hijos).
--
-- view_categories propaga cj.Sort (el de la categoría RAÍZ) a todos sus
-- descendientes en la recursión, así que con todas las raíces en Sort = 0,
-- GET_BudgetDetailDefault (usado por "Cargar Plantilla" en Presupuesto) las
-- ve a todas como una sola sección -- de ahí que apareciera todo agrupado
-- bajo "Suministros Diversos" (la primera raíz por orden de lectura).
--
-- Este script le asigna a cada categoría RAÍZ (parent_id IS NULL) un Sort
-- único dentro de su edificio (1, 2, 3... por edificio, ordenadas por su Sort
-- actual -- todas 0 -- y luego por Description, así el orden queda
-- alfabético y estable). No hace falta tocar las subcategorías: su columna
-- Sort propia no la lee nadie (view_categories usa la del padre heredada por
-- la recursión).
--
-- Correr el SELECT primero para revisar antes de aplicar el UPDATE.
-- =============================================================================

;WITH RaicesConNuevoSort AS (
    SELECT
        Idcategory,
        IdBuilding,
        Description,
        Sort AS SortActual,
        ROW_NUMBER() OVER (PARTITION BY IdBuilding ORDER BY Sort, Description) AS NuevoSort
    FROM Category
    WHERE parent_id IS NULL
)
SELECT * FROM RaicesConNuevoSort ORDER BY IdBuilding, NuevoSort;

-- Si el resultado de arriba se ve bien (un NuevoSort 1..N distinto por cada
-- categoría raíz de cada edificio), descomentar y correr esto:

/*
;WITH RaicesConNuevoSort AS (
    SELECT
        Idcategory,
        ROW_NUMBER() OVER (PARTITION BY IdBuilding ORDER BY Sort, Description) AS NuevoSort
    FROM Category
    WHERE parent_id IS NULL
)
UPDATE c
SET c.Sort = r.NuevoSort
FROM Category c
JOIN RaicesConNuevoSort r ON r.Idcategory = c.Idcategory;
*/
