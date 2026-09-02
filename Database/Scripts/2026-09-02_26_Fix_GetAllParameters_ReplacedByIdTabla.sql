-- =============================================================================
-- Fix inmediato: GET_AllParameters (Database/Scripts/2026-09-02_22_...) todavía no
-- traía la columna ReplacedByIdTabla agregada recién en
-- 2026-09-02_25_Parameter_Promotion.sql -- mismo síntoma que ya vimos varias veces
-- en esta sesión (IsTemplate, DefaultCategory/WaterReadingDefault,
-- IsSystemDefault): EF exige que TODA columna del modelo esté en el resultado de
-- cualquier FromSqlRaw<Parameter>, así que rompía con "InvalidOperationException:
-- The required column 'ReplacedByIdTabla' was not present" apenas se cargaba
-- CUALQUIER lista de parámetros (confirmado en vivo: rompía hasta el Dashboard).
--
-- ReplacedByIdTabla es genuinamente nullable en el modelo (int?), así que no hace
-- falta ISNULL acá (a diferencia de IdParent/IdBuilding, que sí necesitan coalesar
-- a un sentinel porque el modelo los tipa no-nullable) -- sólo agregar la columna.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_AllParameters
@IdBuilding UNIQUEIDENTIFIER
AS
BEGIN
    SELECT  IdTabla,
            Description,
            ShortDescription,
            ISNULL(Value, 0)           AS 'Value',
            ISNULL(Sort, 0)            AS 'Sort',
            ISNULL(IdParent, 0)        AS 'IdParent',
            Estado,
            ISNULL(IdBuilding, '00000000-0000-0000-0000-000000000000') AS 'IdBuilding',
            ISNULL(IsSystemDefault, 0) AS 'IsSystemDefault',
            ReplacedByIdTabla
    FROM    Parameter
    WHERE   IdBuilding = @IdBuilding OR IdBuilding IS NULL
    ORDER BY IdTabla, Sort
END
GO
