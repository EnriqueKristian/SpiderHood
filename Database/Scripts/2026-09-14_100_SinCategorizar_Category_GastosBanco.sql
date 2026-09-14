-- =============================================================================
-- "Sin Categorizar": categoría del sistema (una por edificio, protegida) que
-- agrupa visualmente en el reporte de Gastos los egresos del estado de cuenta
-- que YA salieron del banco pero todavía no tienen un Gasto real creado.
--
-- Criterio de negocio confirmado con el cliente:
--   - Un egreso bancario sin Gasto asociado CUENTA en el reporte (el dinero ya
--     salió), aparece "No Conciliado" bajo "Sin Categorizar".
--   - Un Gasto creado a mano que todavía no se concilió con el banco NO cuenta
--     en el total (puede que ese dinero nunca haya salido de verdad) -- sigue
--     apareciendo en la lista, marcado "No Conciliado", con su propia
--     categoría, pero no suma.
--   - "Sin Categorizar" queda protegida: no se puede editar ni eliminar.
--
-- Piezas de este script:
--   1) Columna Category.IsSystemCategory (protección)
--   2) INS_Category / UPD_Category / DEL_Category / GET_Categories actualizados
--   3) GET_TransactionBankDetailById (nuevo) -- para el botón "Crear Gasto"
--      inline en /expense, reutilizando CreateExpenseFromTransactionModal.
--   4) GET_ExpensesByBuilding: UNION con los egresos sin conciliar, marcados
--      RequiresExpenseCreation=1
--   5) Backfill: "Sin Categorizar" para todo edificio que todavía no tenga una
-- =============================================================================

SET NOCOUNT ON;
GO

-- 1) Columna de protección -----------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Category') AND name = 'IsSystemCategory')
BEGIN
    ALTER TABLE dbo.Category ADD IsSystemCategory BIT NOT NULL CONSTRAINT DF_Category_IsSystemCategory DEFAULT 0;
END
GO

-- 2) INS_Category: nuevo parámetro opcional, sólo se pasa en 1 desde el
--    backfill de este script -- cualquier categoría creada desde la UI
--    (CategoryService.AddCategoryAsync) sigue quedando en 0.
CREATE OR ALTER PROCEDURE [dbo].[INS_Category]
@IdCategory			UNIQUEIDENTIFIER,
@Description		NVARCHAR(100),
@ShortDescript		NVARCHAR(50),
@Icon				NVARCHAR(50),
@Color				NVARCHAR(10),
@Distribution		INT,
@Parent_Id			UNIQUEIDENTIFIER = NULL,
@IdBuilding			UNIQUEIDENTIFIER,
@Sort				INT,
@ShowDetailInReceipt BIT = 1,
@IsSystemCategory	BIT = 0
AS
BEGIN
	INSERT INTO dbo.Category (IdCategory, Description, ShortDescript, Icon, Color, Distribution, Parent_Id, IdBuilding, Sort, ShowDetailInReceipt, IsSystemCategory)
	VALUES (@IdCategory, @Description, @ShortDescript, @Icon, @Color, @Distribution, @Parent_Id, @IdBuilding, @Sort, @ShowDetailInReceipt, @IsSystemCategory)
END
GO

-- 3) UPD_Category: una categoría de sistema no se puede renombrar/editar --
--    protección de fondo (UI ya oculta el botón "Editar" para estas filas).
CREATE OR ALTER PROCEDURE [dbo].[UPD_Category]
@IdCategory		UNIQUEIDENTIFIER,
@Description	NVARCHAR(100),
@ShortDescript	NVARCHAR(50),
@Color			NVARCHAR(10),
@Icon			NVARCHAR(30),
@Distribution	INT,
@ShowDetailInReceipt BIT = 1
AS
BEGIN
	IF EXISTS (SELECT 1 FROM dbo.Category WHERE Idcategory = @IdCategory AND IsSystemCategory = 1)
	BEGIN
		RAISERROR('No se puede editar la categoría "Sin Categorizar" del sistema.', 16, 1);
		RETURN;
	END

	UPDATE
	Category
	SET		Description = @Description,
			ShortDescript = @ShortDescript,
			Color = @Color,
			Icon = @Icon,
			Distribution = @Distribution,
			ShowDetailInReceipt = @ShowDetailInReceipt
	WHERE	Idcategory = @IdCategory
END
GO

-- 4) DEL_Category: idem, no se puede eliminar -- CategoryService.DeleteCategoryAsync
--    ya sabe mostrar el mensaje de RAISERROR como error de negocio (mismo patrón
--    que el 547 de FK), no hace falta tocar el código C#.
CREATE OR ALTER PROCEDURE [dbo].[DEL_Category]
@IdCategory	UNIQUEIDENTIFIER
AS
BEGIN
	IF EXISTS (SELECT 1 FROM dbo.Category WHERE Idcategory = @IdCategory AND IsSystemCategory = 1)
	BEGIN
		RAISERROR('No se puede eliminar la categoría "Sin Categorizar" del sistema.', 16, 1);
		RETURN;
	END

	DELETE Category
	WHERE Idcategory = @IdCategory
END
GO

-- 5) GET_Categories: agrega IsSystemCategory a las dos ramas del CTE (raíz e
--    hijos) para que FromSqlRaw<Category> (que exige TODAS las columnas
--    mapeadas de la entidad) encuentre la columna nueva.
CREATE OR ALTER PROCEDURE [dbo].[GET_Categories]
@IdBuilding UNIQUEIDENTIFIER
AS
WITH CategoriaJerarquia AS (
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
        Distribution,
        ShowDetailInReceipt,
        IsSystemCategory
    FROM Category
    WHERE parent_id IS NULL AND IdBuilding = @IdBuilding

    UNION ALL

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
        c.Distribution,
        c.ShowDetailInReceipt,
        c.IsSystemCategory
    FROM Category c
    INNER JOIN CategoriaJerarquia cj ON c.parent_id = cj.Idcategory
    WHERE c.IdBuilding = @IdBuilding
)
SELECT *
FROM CategoriaJerarquia
ORDER BY nivel, Sort;
GO

-- 6) GET_TransactionBankDetailById: trae UNA transacción completa por Id --
--    hace falta para el botón "Crear Gasto" inline de /expense (Gastos), que
--    reutiliza CreateExpenseFromTransactionModal.razor (el mismo formulario que
--    usa Conciliación) pero recibe sólo el IdStatementDetail desde la fila
--    "virtual" que arma GET_ExpensesByBuilding más abajo. A diferencia de
--    GET_BankTransactionsNoConcilied (que no trae Ignored/IgnoredReason/
--    IgnoredType -- FromSqlRaw<TransactionBankDetail> exige TODAS las columnas
--    mapeadas), acá se listan explícitamente para no repetir ese problema.
CREATE OR ALTER PROCEDURE dbo.GET_TransactionBankDetailById
    @IdStatementDetail UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  md.IdStatementDetail,
            mh.IdBankAccount,
            md.IdStatementHeader,
            ISNULL(md.IdParent, '00000000-0000-0000-0000-000000000000')      AS IdParent,
            ISNULL(id.IdGroupUnit, '00000000-0000-0000-0000-000000000000')   AS IdGroupUnit,
            md.StatementDate,
            md.Description,
            md.Amount,
            md.SequenceNumber,
            md.OriginalReference,
            md.Currency,
            md.Origen,
            md.ReconciliationStatus,
            md.ReconciliationDate,
            ISNULL(id.AmountPaid, 0)                                        AS AmountPaid,
            md.Amount - ISNULL(id.AmountPaid, 0)                            AS Balance,
            md.Ignored,
            md.IgnoredReason,
            md.IgnoredType
    FROM    AccountStatementDetail md
    JOIN    AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
    LEFT JOIN VW_SUM_InstallmentPaidTransaction id ON md.IdStatementDetail = id.IdTransaction
    WHERE   md.IdStatementDetail = @IdStatementDetail
END
GO

-- 7) GET_ExpensesByBuilding: se agrega una segunda rama (UNION ALL) con los
--    egresos del estado de cuenta que ya salieron del banco pero todavía no
--    tienen un Gasto real vinculado -- se muestran como fila "virtual" (su
--    IdExpense es en realidad el IdStatementDetail de la transacción) bajo la
--    categoría del sistema "Sin Categorizar", con RequiresExpenseCreation=1
--    para que la UI sepa que necesita el botón "Crear Gasto" en vez de
--    Editar/Eliminar. Reconciled queda en 0 a propósito (no hay Gasto real
--    todavía) -- lo que cambia es que estas filas SÍ cuentan en el total de
--    /expense (ver ExpensePage.EsGastoConfirmado), a diferencia de un Gasto
--    manual sin conciliar.
CREATE OR ALTER PROCEDURE dbo.GET_ExpensesByBuilding
    @IdBuilding     UNIQUEIDENTIFIER,
    @StartDate      DATE = NULL,
    @EndDate        DATE = NULL
AS
BEGIN
    DECLARE @IdSinCategorizar UNIQUEIDENTIFIER, @SinCategorizarNombre NVARCHAR(50)

    SELECT TOP 1 @IdSinCategorizar = IdCategory, @SinCategorizarNombre = ShortDescript
    FROM Category
    WHERE IdBuilding = @IdBuilding AND IsSystemCategory = 1

    SELECT
    e.IdExpense,
    e.Description,
    e.Amount,
    e.IdCategory,
    ISNULL(c.ShortDescript,'')                                          AS Category,
    ISNULL(e.Supplier,'')                                               AS Supplier,
    ISNULL(e.PaymentMethod, 6)                                          AS PaymentMethod,
    ISNULL(e.Status, 0)                                                 AS Status,
    CAST(IIF(e.IdStatementDetail IS NOT NULL, 1, 0) AS BIT)             AS Reconciled,
    ISNULL(e.IdStatementDetail,'00000000-0000-0000-0000-000000000000')  AS ReconciledTransactionId,
    e.Distribution,
    e.IdBuilding,
    ISNULL(e.Notes,'-')                                                 AS Notes,
    ISNULL(e.AutoReconcile, 1)                                          AS AutoReconcile,
    e.ExpenseDate,
    e.IncludeInQuota,
    CAST(0 AS BIT)                                                      AS RequiresExpenseCreation
    FROM Expense e
    LEFT JOIN Category c ON c.IdCategory = e.IdCategory
    WHERE e.IdBuilding = @IdBuilding
      AND (@StartDate IS NULL OR e.ExpenseDate >= @StartDate)
      AND (@EndDate IS NULL OR e.ExpenseDate <= @EndDate)

    UNION ALL

    SELECT
    md.IdStatementDetail                                                AS IdExpense,
    md.Description                                                      AS Description,
    ABS(md.Amount)                                                      AS Amount,
    ISNULL(@IdSinCategorizar, '00000000-0000-0000-0000-000000000000')   AS IdCategory,
    ISNULL(@SinCategorizarNombre, 'Sin Categorizar')                    AS Category,
    ''                                                                  AS Supplier,
    6                                                                   AS PaymentMethod,
    1                                                                   AS Status, /* StatusExpense.Approved */
    CAST(0 AS BIT)                                                      AS Reconciled,
    md.IdStatementDetail                                                AS ReconciledTransactionId,
    CAST(NULL AS INT)                                                   AS Distribution,
    @IdBuilding                                                         AS IdBuilding,
    '-'                                                                 AS Notes,
    CAST(1 AS BIT)                                                      AS AutoReconcile,
    CAST(md.StatementDate AS DATE)                                      AS ExpenseDate,
    CAST(0 AS BIT)                                                      AS IncludeInQuota,
    CAST(1 AS BIT)                                                      AS RequiresExpenseCreation
    FROM AccountStatementDetail md
    JOIN AccountStatementHeader mh ON mh.IdStatementHeader = md.IdStatementHeader
    JOIN BankAccount ba ON ba.IdBankAccount = mh.IdBankAccount
    WHERE ba.IdBuilding = @IdBuilding
      AND md.Amount < 0
      AND md.Ignored = 0
      AND md.ReconciliationStatus <> 1 /* Conciliada */
      AND NOT EXISTS (SELECT 1 FROM Expense e2 WHERE e2.IdStatementDetail = md.IdStatementDetail)
      AND (@StartDate IS NULL OR md.StatementDate >= @StartDate)
      AND (@EndDate IS NULL OR md.StatementDate <= @EndDate)
END
GO

-- 8) Backfill: toda categoría raíz "Sin Categorizar" que falte, para cada
--    edificio que ya existe hoy. Sort=9999 para que siempre quede al final del
--    listado de categorías (no compite con el Sort real de las categorías del
--    administrador).
INSERT INTO dbo.Category (IdCategory, Description, ShortDescript, Icon, Color, Distribution, Parent_Id, IdBuilding, Sort, ShowDetailInReceipt, IsSystemCategory)
SELECT
    NEWID(),
    'Sin Categorizar',
    'Sin Categorizar',
    'bi bi-question-circle',
    '#9CA3AF',
    1, /* TypeDistribution.Fija */
    NULL,
    b.IdBuilding,
    9999,
    1,
    1
FROM Building b
WHERE NOT EXISTS (
    SELECT 1 FROM Category c WHERE c.IdBuilding = b.IdBuilding AND c.IsSystemCategory = 1
);
GO
