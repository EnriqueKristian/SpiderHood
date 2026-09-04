-- =============================================================================
-- El script 37 asumía que RealEstateUnit tenía una columna TypeGroupUnit --
-- basado en que BDLayout.Get.cs (GetUnitsByBuildingAsync) lee
-- row["TypeGroupUnit"] con ADO.NET crudo sin tirar excepción, di por sentado que
-- venía de una columna real de la tabla. Mal supuesto: nunca vi el texto de
-- GET_UnitsByBuilding, así que no tenía forma de saber si ese valor sale de la
-- tabla o de una columna calculada/JOIN en el SELECT de esa SP. La captura real
-- de RealEstateUnit que pasó Enrique lo confirma -- no existe la columna, sólo
-- IdGroupUnit (uniqueidentifier, NOT NULL -- por eso el sentinel de "sin grupo"
-- es Guid.Empty, no NULL, coincide con como ya lo trata el código C# en todos
-- lados: `IdGroupOwner == Guid.Empty`).
--
-- Se saca TypeGroupUnit del UPDATE. El parámetro @TypeGroupUnit se deja
-- declarado (sin usar) para no tener que tocar el call site en
-- BDLayout.Add.cs, que todavía manda los 3 parámetros.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_GroupUnitOwner
    @IdUnit         UNIQUEIDENTIFIER,
    @IdGroupOwner   UNIQUEIDENTIFIER,
    @TypeGroupUnit  INT
AS
BEGIN
    UPDATE RealEstateUnit
    SET IdGroupUnit = @IdGroupOwner
    WHERE IdUnit = @IdUnit;
END
GO
