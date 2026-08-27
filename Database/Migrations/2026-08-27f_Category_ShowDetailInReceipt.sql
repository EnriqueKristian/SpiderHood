-- Agrega un flag por categoría raíz (sección del recibo) para colapsar su detalle en el
-- PDF del recibo de mantenimiento: si está desactivado, esa sección se imprime solo con
-- su nombre y subtotal, sin desglosar cada ítem — el "Ver Detalle" en pantalla no cambia,
-- siempre muestra el desglose completo (ese comportamiento vive solo en
-- InstallmentExportService, en Models/Utilities.cs).
--
-- Editable desde Administración de Categorías (/category), solo para categorías raíz.
--
-- Basado en el texto vigente de INS_Category/UPD_Category/GET_Categories provisto por el
-- usuario. Los parámetros nuevos van AL FINAL de cada procedure: BDLayout.Add.cs/
-- BDLayout.Update.cs llaman a estos SPs con parámetros posicionales, así que el orden
-- importa y el código en C# ya se actualizó para pasarlos en este mismo orden.
--
-- Ejecutar en orden, contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

-- 1) Nueva columna. NOT NULL con default 1 (mostrar detalle), para no cambiar el
--    comportamiento de ninguna categoría existente.
ALTER TABLE dbo.Category
    ADD ShowDetailInReceipt BIT NOT NULL CONSTRAINT DF_Category_ShowDetailInReceipt DEFAULT (1);
GO

-- 2) INS_Category
ALTER PROCEDURE dbo.INS_Category
@IdCategory			UNIQUEIDENTIFIER,
@Description		NVARCHAR(100),
@ShortDescript		NVARCHAR(50),
@Icon				NVARCHAR(50),
@Color				NVARCHAR(10),
@Distribution		INT,
@Parent_Id			UNIQUEIDENTIFIER,
@IdBuilding			UNIQUEIDENTIFIER,
@Sort				INT,
@ShowDetailInReceipt BIT = 1
AS
BEGIN
	INSERT INTO dbo.Category (IdCategory, Description, ShortDescript, Icon, Color, Distribution, Parent_Id, IdBuilding, Sort, ShowDetailInReceipt )
	VALUES (@IdCategory, @Description, @ShortDescript, @Icon, @Color, @Distribution, @Parent_Id, @IdBuilding, @Sort, @ShowDetailInReceipt )
END
GO

-- 3) UPD_Category
ALTER PROCEDURE dbo.UPD_Category
@IdCategory		UNIQUEIDENTIFIER,
@Description	NVARCHAR(100),
@ShortDescript	NVARCHAR(50),
@Color			NVARCHAR(10),
@Icon			NVARCHAR(30),
@Distribution	INT,
@ShowDetailInReceipt BIT = 1
AS
BEGIN
	UPDATE	Category
	SET		Description = @Description,
			ShortDescript = @ShortDescript,
			Color = @Color,
			Icon = @Icon,
			Distribution = @Distribution,
			ShowDetailInReceipt = @ShowDetailInReceipt
	WHERE	Idcategory = @IdCategory
END
GO

-- 4) GET_Categories: se agrega la columna nueva en las dos ramas del CTE recursivo (raíz
--    e hijos) para que EF Core (FromSqlRaw<Category>) la encuentre en el resultado.
ALTER PROCEDURE GET_Categories
@IdBuilding UNIQUEIDENTIFIER
AS
-- Usamos CTE recursivo para recorrer la jerarquía
WITH CategoriaJerarquia AS (
    -- Nivel raíz
    SELECT
        Idcategory,
        Description,
        ShortDescript,
        icon,
        color,
        ISNULL(parent_id, '00000000-0000-0000-0000-000000000000') AS ParentId,
        0 AS nivel,
        CAST(Description AS NVARCHAR(MAX)) AS ruta,
        IdBuilding,
        Description AS ParentName,
        Sort,
--        CAST(Sort AS DECIMAL(10,2)) AS ItemNumber,
        Distribution,
        ShowDetailInReceipt
    FROM Category
    WHERE parent_id IS NULL AND IdBuilding = @IdBuilding

    UNION ALL

    -- Niveles hijos
    SELECT
        c.Idcategory,
        c.Description,
        c.ShortDescript,
        c.icon,
        c.color,
        c.parent_id AS ParentId,
        cj.nivel + 1,
        CAST(cj.ruta + ' > ' + c.Description AS NVARCHAR(MAX)),
        @IdBuilding,
        cj.Description AS ParentName,
        c.Sort,
--        CAST(cj.ItemNumber AS DECIMAL(10,2)) + 0.01,
        c.Distribution,
        c.ShowDetailInReceipt
    FROM Category c
    INNER JOIN CategoriaJerarquia cj ON c.parent_id = cj.Idcategory
    WHERE c.IdBuilding = @IdBuilding
)
SELECT *
FROM CategoriaJerarquia
ORDER BY nivel, Sort;
GO
