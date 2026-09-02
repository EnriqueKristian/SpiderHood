-- =============================================================================
-- Paso 4 (2/3): FK real IdCategory -> Category.IdCategory en las 4 tablas que la
-- consumen (Docs/Design-Defaults-Sistema-Mixto.md §6). Hoy la relación es "lógica"
-- (sin constraint física en ningún script del repo) -- confirmado además por el
-- propio código: BudgetService.SaveCategoriesAsync ya menciona en un comentario un
-- "FK_BudgetDetail_Category" que hacía fallar el INSERT cuando la Category
-- referenciada no existía, así que puede que ya exista un FK con ese nombre creado
-- a mano en tu BD -- por eso cada tabla se trata de forma independiente y se salta
-- sola si ya tiene uno.
--
-- Por tabla:
--   - Si ya existe un FK sobre IdCategory -> Category, se saltea (no rompe nada,
--     sólo lo informa).
--   - Si hay filas con IdCategory que no existe en Category (huérfanas), TAMBIÉN
--     se saltea esa tabla puntual con un aviso -- no aborta el script entero, cada
--     tabla es independiente. Revisá los PRINT: cualquier tabla que diga "OMITIDA"
--     quedó sin protección real y hay que limpiar esos datos antes de reintentar
--     este mismo script (es re-ejecutable).
--
-- ON DELETE/UPDATE: sin acción explícita (default NO ACTION) a propósito -- borrar
-- una Categoría todavía en uso en Gastos/Presupuesto/Exoneraciones/Calendario tiene
-- que fallar, no arrastrar el borrado en cascada. La UI
-- (CategoryService.DeleteCategoryAsync) ya distingue esta violación puntual (SQL
-- error 547) para mostrar un mensaje claro en vez del error crudo de SQL Server.
--
-- No hace falta transacción envolvente: cada ALTER TABLE es independiente y el
-- objetivo es justamente que una tabla con datos sucios no bloquee a las otras 3.
-- =============================================================================

SET NOCOUNT ON;
GO

PRINT '--- Diagnóstico: filas huérfanas por tabla (antes de crear cualquier FK) ---';
SELECT 'Expense' AS Tabla, COUNT(*) AS Huerfanos
FROM dbo.Expense e
WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory)
UNION ALL
SELECT 'Exoneration', COUNT(*)
FROM dbo.Exoneration e
WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory)
UNION ALL
SELECT 'BudgetDetail', COUNT(*)
FROM dbo.BudgetDetail e
WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory)
UNION ALL
SELECT 'CalendarItem', COUNT(*)
FROM dbo.CalendarItem e
WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory);
GO

-- ---------------------------------------------------------------------------
-- Expense
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.Expense')
      AND fk.referenced_object_id = OBJECT_ID('dbo.Category')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdCategory'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Expense e WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory))
        PRINT 'Expense: OMITIDA -- tiene IdCategory huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.Expense WITH CHECK ADD CONSTRAINT FK_Expense_Category FOREIGN KEY (IdCategory) REFERENCES dbo.Category(IdCategory);
        PRINT 'Expense: FK_Expense_Category creado.';
    END
END
ELSE
    PRINT 'Expense: ya tenía un FK sobre IdCategory -> Category, sin cambios.';
GO

-- ---------------------------------------------------------------------------
-- Exoneration
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.Exoneration')
      AND fk.referenced_object_id = OBJECT_ID('dbo.Category')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdCategory'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.Exoneration e WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory))
        PRINT 'Exoneration: OMITIDA -- tiene IdCategory huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.Exoneration WITH CHECK ADD CONSTRAINT FK_Exoneration_Category FOREIGN KEY (IdCategory) REFERENCES dbo.Category(IdCategory);
        PRINT 'Exoneration: FK_Exoneration_Category creado.';
    END
END
ELSE
    PRINT 'Exoneration: ya tenía un FK sobre IdCategory -> Category, sin cambios.';
GO

-- ---------------------------------------------------------------------------
-- BudgetDetail
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.BudgetDetail')
      AND fk.referenced_object_id = OBJECT_ID('dbo.Category')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdCategory'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.BudgetDetail e WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory))
        PRINT 'BudgetDetail: OMITIDA -- tiene IdCategory huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.BudgetDetail WITH CHECK ADD CONSTRAINT FK_BudgetDetail_Category FOREIGN KEY (IdCategory) REFERENCES dbo.Category(IdCategory);
        PRINT 'BudgetDetail: FK_BudgetDetail_Category creado.';
    END
END
ELSE
    PRINT 'BudgetDetail: ya tenía un FK sobre IdCategory -> Category, sin cambios.';
GO

-- ---------------------------------------------------------------------------
-- CalendarItem (IdCategory nullable -- ver Classes/Calendar/CalendarItem.cs)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
    WHERE fk.parent_object_id = OBJECT_ID('dbo.CalendarItem')
      AND fk.referenced_object_id = OBJECT_ID('dbo.Category')
      AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'IdCategory'
)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.CalendarItem e WHERE e.IdCategory IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Category c WHERE c.IdCategory = e.IdCategory))
        PRINT 'CalendarItem: OMITIDA -- tiene IdCategory huérfanos, limpiar antes de reintentar.';
    ELSE
    BEGIN
        ALTER TABLE dbo.CalendarItem WITH CHECK ADD CONSTRAINT FK_CalendarItem_Category FOREIGN KEY (IdCategory) REFERENCES dbo.Category(IdCategory);
        PRINT 'CalendarItem: FK_CalendarItem_Category creado.';
    END
END
ELSE
    PRINT 'CalendarItem: ya tenía un FK sobre IdCategory -> Category, sin cambios.';
GO
