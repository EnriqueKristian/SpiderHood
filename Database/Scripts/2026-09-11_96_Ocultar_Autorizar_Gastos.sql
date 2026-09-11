-- El fix de la Fase 1 (2026-09-10_91) buscó el título exacto 'Autorizar gastos'/
-- 'Autorizar Pagos' y no le achuntó -- sigue visible en el menú de Junta apuntando
-- a una URL que no existe. Esta vez con LIKE, y mostrando antes qué hay para no
-- ocultar algo por error.
DECLARE @IdJunta UNIQUEIDENTIFIER = '467674F2-BD3B-41C7-A4FB-B13B5152B914';

SELECT IdMenu, Title, Url, IsVisible FROM dbo.MenuItems
WHERE IdParent = @IdJunta AND Title LIKE '%utorizar%gasto%';

UPDATE dbo.MenuItems
SET IsVisible = 0
WHERE IdParent = @IdJunta AND Title LIKE '%utorizar%gasto%';

SELECT Title, Url, IsVisible FROM dbo.MenuItems WHERE IdParent = @IdJunta ORDER BY DisplayOrder;
