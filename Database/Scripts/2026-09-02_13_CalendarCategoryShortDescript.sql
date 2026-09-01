-- =============================================================================
-- Ajuste chico sobre 2026-09-02_12: usa Category.ShortDescript (no
-- Description) para CategoryName -- mismo criterio que ya usa el selector de
-- categoría de "Crear Gasto" (ReconciliationPages/CreateExpenseFromTransactionModal.razor),
-- que muestra ShortDescript tanto en el optgroup del padre como en las
-- opciones hijas. Design consistency: la UI de Calendario ahora reutiliza el
-- mismo select agrupado por categoría padre (Nivel 0) / categoría hija.
--
-- Solo toca los 2 SPs de lectura -- la tabla no cambia. Seguro de correr
-- después de 2026-09-02_12 (CREATE OR ALTER es idempotente).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemsByBuilding
    @IdBuilding UNIQUEIDENTIFIER,
    @From DATETIME2 = NULL,
    @To DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ci.IdCalendarItem, ci.IdBuilding, ci.Title, ci.Description, ci.Type, ci.IdCategory, ci.StartDate, ci.EndDate,
           ci.Location, ci.Responsible, ci.Cost, ci.Status,
           ci.Recurrence, ci.RecurrenceInterval, ci.RecurrenceEndDate, ci.IdRecurrenceGroup, ci.IsRecurrenceMaster,
           ci.CreatedBy, ci.CreatedOn, ci.ModifiedBy, ci.ModifiedOn,
           cat.ShortDescript AS CategoryName, cat.Icon AS CategoryIcon, cat.Color AS CategoryColor
    FROM dbo.CalendarItem ci
    LEFT JOIN dbo.Category cat ON cat.IdCategory = ci.IdCategory
    WHERE ci.IdBuilding = @IdBuilding
      AND (@From IS NULL OR ci.StartDate >= @From)
      AND (@To IS NULL OR ci.StartDate <= @To)
    ORDER BY ci.StartDate ASC;
END
GO

CREATE OR ALTER PROCEDURE dbo.GET_CalendarItemById
    @IdCalendarItem UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ci.IdCalendarItem, ci.IdBuilding, ci.Title, ci.Description, ci.Type, ci.IdCategory, ci.StartDate, ci.EndDate,
           ci.Location, ci.Responsible, ci.Cost, ci.Status,
           ci.Recurrence, ci.RecurrenceInterval, ci.RecurrenceEndDate, ci.IdRecurrenceGroup, ci.IsRecurrenceMaster,
           ci.CreatedBy, ci.CreatedOn, ci.ModifiedBy, ci.ModifiedOn,
           cat.ShortDescript AS CategoryName, cat.Icon AS CategoryIcon, cat.Color AS CategoryColor
    FROM dbo.CalendarItem ci
    LEFT JOIN dbo.Category cat ON cat.IdCategory = ci.IdCategory
    WHERE ci.IdCalendarItem = @IdCalendarItem;
END
GO
