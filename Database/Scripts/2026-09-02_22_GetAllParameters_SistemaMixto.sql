-- =============================================================================
-- Paso 3 (sub-paso 3) del plan en Docs/Design-Defaults-Sistema-Mixto.md.
--
-- Texto real de GET_AllParameters (confirmado por el usuario, sp_helptext):
--
--   CREATE PROCEDURE GET_AllParameters
--   @IdBuilding UNIQUEIDENTIFIER
--   AS
--   BEGIN
--       SELECT  IdTabla, Description, ShortDescription,
--               ISNULL(Value, 0) AS 'Value', ISNULL(Sort, 0) AS 'Sort',
--               ISNULL(IdParent, 0) AS 'IdParent', Estado, IdBuilding
--       FROM    Parameter
--       WHERE   IdBuilding = @IdBuilding
--       ORDER BY IdTabla, Sort
--   END
--
-- Dos problemas para lo que necesitamos ahora:
--   1) IdBuilding se devuelve crudo, sin ISNULL -- apenas exista una fila
--      Sistema (IdBuilding NULL en 2026-09-02_21), esto revienta con el mismo
--      SqlNullValueException que ya vimos con BuildingConfiguration.DefaultCategory
--      en el Paso 1 (Models.Parameter.IdBuilding es Guid no-nullable). Se
--      coalesa a Guid.Empty, mismo patrón que ya usa esta misma consulta para
--      IdParent (ISNULL(IdParent, 0)) -- Guid.Empty pasa a significar "valor de
--      Sistema", igual que 0 ya significa "es raíz".
--   2) WHERE IdBuilding = @IdBuilding nunca trae las filas de Sistema (NULL no
--      matchea nada por igualdad en SQL) -- se cambia a
--      "IdBuilding = @IdBuilding OR IdBuilding IS NULL", así un edificio ve sus
--      propios valores Mixto MÁS todos los valores de Sistema.
--
-- De paso se agrega IsSystemDefault al SELECT (columna nueva del script
-- anterior) -- si no, esto vuelve a romper apenas Models.Parameter tenga esa
-- propiedad (ver Classes/Parameter.cs).
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
            ISNULL(IsSystemDefault, 0) AS 'IsSystemDefault'
    FROM    Parameter
    WHERE   IdBuilding = @IdBuilding OR IdBuilding IS NULL
    ORDER BY IdTabla, Sort
END
GO
