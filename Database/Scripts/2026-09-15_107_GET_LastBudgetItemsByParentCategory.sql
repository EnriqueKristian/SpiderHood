-- =============================================================================
-- Al agregar una Sección (que matchea una Categoría existente) o un Item nuevo
-- a una sección vacía en Generar Presupuesto, hoy no se sugiere nada de lo que
-- esa categoría ya tuvo en presupuestos reales anteriores del edificio -- sólo
-- existía el "esqueleto" de la Plantilla (GET_BudgetDetailDefault), que trae
-- las categorías del propio edificio pero SIEMPRE con MonthlyAmount = 0.00
-- (nunca los montos reales que se usaron antes), y sólo si el Administrador
-- clickeó "Cargar Plantilla" en esa sesión -- con "Sin plantilla" no hay nada
-- que ofrecer.
--
-- Este SP trae, para una categoría (ej. "Mantenimientos"), los items reales
-- (Description/MonthlyAmount/AnnualAmount/Frequency/Type) que tuvieron sus
-- categorías HIJAS la última vez que aparecieron en un presupuesto real del
-- edificio -- uno por categoría hija, el más reciente si aparece en más de un
-- presupuesto histórico (no todos del mismo presupuesto necesariamente, así
-- una categoría agregada hace poco igual aparece con su valor más reciente).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_LastBudgetItemsByParentCategory
    @IdBuilding UNIQUEIDENTIFIER,
    @IdParentCategory UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Ranked AS (
        SELECT  bd.IdCategory,
                bd.Description,
                bd.MonthlyAmount,
                bd.AnnualAmount,
                bd.Frequency,
                bd.Type,
                ROW_NUMBER() OVER (PARTITION BY bd.IdCategory ORDER BY bh.BudgetDate DESC) AS rn
        FROM    dbo.BudgetDetail bd
        JOIN    dbo.BudgetHeader bh ON bh.IdBudgetHeader = bd.IdBudgetHeader
        JOIN    dbo.Category c ON c.Idcategory = bd.IdCategory
        WHERE   bh.IdBuilding = @IdBuilding
                AND c.parent_id = @IdParentCategory
                AND bd.IsHeader = 0
    )
    SELECT  NEWID() AS IdBudgetDetail,
            IdCategory,
            0 AS IdSection,
            0.00 AS ItemNumber,
            Description,
            Description AS ShortDescrition,
            MonthlyAmount,
            AnnualAmount,
            Frequency,
            Type,
            CAST(0 AS BIT) AS IsHeader,
            @IdParentCategory AS IdParent
    FROM    Ranked
    WHERE   rn = 1
    ORDER BY Description;
END
GO
