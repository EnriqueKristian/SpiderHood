/*
    Fixes GET_AllParameters crashing whenever a parameter row has no Value
    (Value NULL in the DB) — which is the normal, expected shape for a
    top-level "group" parameter row (Models.Parameter groups children under
    a parent with no Value of its own; only the children carry a Value).

    Models.Parameter.Value and .Sort are declared as non-nullable int in C#,
    but the Value and Sort columns both allow NULL in the DB. EF's raw-SQL
    materialization (BDLayout.ExecuteQueryListAsync<Parameter>) throws
    SqlNullValueException the moment it hits a NULL Value/Sort, which
    aborts GetParametersByBuildingAsync for the WHOLE building — not just
    that one row. Any page depending on ParameterService.ListParameters
    (Home, the Parameter admin page, UnitGroups' unit-type dropdown, etc.)
    then fails silently or shows no data, however many valid rows exist.

    IdParent already gets this same ISNULL(...) treatment further down in
    this same procedure; Value and Sort were just missed. This mirrors that
    existing pattern instead of touching the non-nullable Parameter model
    (which would ripple through every place Value/Sort is used).

    Run this against the SpiderHoodContext database.
*/

IF OBJECT_ID('dbo.GET_AllParameters', 'P') IS NOT NULL DROP PROCEDURE dbo.GET_AllParameters;
GO
CREATE PROCEDURE [dbo].[GET_AllParameters]
    @IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdTabla,
            Description,
            ShortDescription,
            ISNULL(Value, 0)       AS 'Value',
            ISNULL(Sort, 0)        AS 'Sort',
            ISNULL(IdParent, 0)    AS 'IdParent',
            Estado,
            IdBuilding
    FROM    Parameter
    WHERE   IdBuilding = @IdBuilding
    ORDER BY IdTabla, Sort
END
GO
