-- =============================================================================
-- Congela, por categoría de presupuesto (BudgetDetail), cuántas unidades
-- Depto/Oficina realmente dividieron esa categoría (después de exoneraciones)
-- en el momento en que se calculó/publicó el presupuesto.
--
-- Hoy Ver Detalle (/cuotas) y el PDF de un presupuesto YA PUBLICADO recalculan
-- ese divisor EN VIVO con la composición ACTUAL del edificio (unidades y
-- exoneraciones vigentes HOY) en vez de con la que existía al momento real de
-- cálculo -- si el edificio cambia después (nueva unidad, nueva/quitada
-- exoneración), el desglose que se muestra puede dejar de coincidir con lo que
-- realmente se cobró, aunque el monto real (Installment.Amount) nunca cambie.
--
-- BudgetDetail.NroApartments guarda ese divisor ya congelado. Se calcula en
-- BudgetCalculator.CalculateQuota() (Classes/BudgetState.cs) cada vez que un
-- presupuesto en estado Created se recalcula/guarda, y deja de tocarse en
-- cuanto el presupuesto pasa a Check/Approved/Active/Closed (mismo ciclo de
-- vida que ya tiene MonthlyAmount/AnnualAmount en esta misma tabla).
--
-- También se aprovecha para que la categoría de Agua Áreas Comunes respete
-- exoneraciones en su divisor -- hoy NO lo hacía (siempre dividía entre el
-- total de unidades sin restar excepciones), a pedido explícito del negocio:
-- "si en algún caso alguien exonera esa categoría debería respetarlo".
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BudgetDetail') AND name = 'NroApartments'
)
BEGIN
    ALTER TABLE dbo.BudgetDetail ADD NroApartments INT NULL;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[INS_BudgetDetail]
@IdBudgetDetail     UNIQUEIDENTIFIER,
@IdCategory         UNIQUEIDENTIFIER,
@IdSection          INT,
@ItemNumber         DECIMAL(10,2),
@Description        NVARCHAR(200),
@MonthlyAmount      DECIMAL(18,2) = NULL,
@AnnualAmount       DECIMAL(18,2) = NULL,
@Frequency          INT,
@Type               INT,
@IsHeader           BIT,
@IdBudgetHeader     UNIQUEIDENTIFIER,
@NroApartments      INT = NULL
AS
BEGIN
    INSERT INTO BudgetDetail (IdBudgetDetail, IdCategory, IdSection, ItemNumber, Description, MonthlyAmount, AnnualAmount, Frequency, Type, IsHeader, IdBudgetHeader, NroApartments )
    VALUES ( @IdBudgetDetail, @IdCategory, @IdSection, @ItemNumber, @Description, @MonthlyAmount, @AnnualAmount, @Frequency, @Type, @IsHeader, @IdBudgetHeader, @NroApartments  );
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[UPD_BudgetDetail]
@IdBudgetDetail     UNIQUEIDENTIFIER,
@IdSection          INT,
@ItemNumber         DECIMAL(10,2),
@Description        NVARCHAR(200),
@MonthlyAmount      DECIMAL(18,2) = NULL,
@AnnualAmount       DECIMAL(18,2) = NULL,
@Frequency          INT,
@Type               INT,
@IsHeader           BIT,
@NroApartments      INT = NULL
AS
BEGIN
    UPDATE dbo.BudgetDetail
    SET
        IdSection = @IdSection,
        ItemNumber = @ItemNumber,
        Description = @Description,
        MonthlyAmount = @MonthlyAmount,
        AnnualAmount = @AnnualAmount,
        Frequency = @Frequency,
        Type = @Type,
        IsHeader = @IsHeader,
        NroApartments = @NroApartments
    WHERE IdBudgetDetail = @IdBudgetDetail;
END;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_List_BudgetDetail]
@IdBudgetHeader	UNIQUEIDENTIFIER
AS

DECLARE	@isNewItem	BIT
SET		@isNewItem = 0

SELECT	bd.IdBudgetDetail,
		bd.IdCategory,
		bd.IdSection,
		bd.ItemNumber,
		bd.Description,
		bd.Description			AS ShortDescription,
		bd.MonthlyAmount,
		bd.AnnualAmount,
		bd.Frequency,
		bd.Type,
		bd.IsHeader,
		bd.IdBudgetHeader,
		@isNewItem				AS isNewItem,
		ISNULL(c.parent_id,'00000000-0000-0000-0000-000000000000')	AS IdParent,
		bd.NroApartments
FROM	BudgetDetail bd
JOIN	Category c ON bd.IdCategory = c.Idcategory
WHERE	IdBudgetHeader = @IdBudgetHeader
ORDER BY IdSection, IsHeader DESC, ItemNumber
GO
