-- =============================================================================
-- Cierra la cadena de "asignar la primera unidad a un propietario". Los 4 SPs
-- involucrados apuntaban todos a tablas de una generación anterior del esquema
-- que ya no existe (GroupOwner, OwnerGroupOwner) o directamente no existían
-- (INS_GroupOwner/INS_OwnerGroupOwner -- sólo existían con una errata,
-- "INS_GroupOnwer"/"INS_OwnerGroupOnwer", apuntando también a las tablas viejas).
-- Las tablas reales, confirmadas por Enrique (capturas + VW_OwnerUnit), son:
--   GroupUnit(IdGroupUnit PK, TotalArea, GroupNumber)
--   OwnerGroupRole(IdGroupOwnerRol PK, IdOwner FK, Role, IdGroupUnit FK)
--   RealEstateUnit ya tiene sus propias columnas IdGroupUnit y TypeGroupUnit
--   (confirmado: BDLayout.Get.cs las lee con ADO.NET crudo sin tirar excepción)
--   -- osea que "asignar una unidad a un grupo" es un UPDATE sobre
--   RealEstateUnit, no un INSERT en otra tabla (que era justo el bug de
--   INS_GroupUnitOwner: insertaba a ciegas 3 valores posicionales en GroupUnit,
--   que sólo tiene 3 columnas pero ninguna es IdUnit/TypeGroupUnit).
--
-- Los nombres de los 4 procs se mantienen igual (los que ya llama el código C#,
-- BDLayout.Add.cs/Update.cs) -- sólo cambia el cuerpo, para no tener que tocar
-- StoredProcedures.* en el código.
-- =============================================================================

SET NOCOUNT ON;
GO

-- Crea el grupo (GroupUnit) + el rol de titular (OwnerGroupRole) en un solo paso
-- -- reemplaza al INS_GroupOwner que nunca existió con ese nombre exacto.
-- @Name se recibe pero no se guarda: GroupUnit no tiene columna de nombre, sólo
-- GroupNumber (int) -- ver @GroupNumber más abajo, que sí se guarda.
CREATE OR ALTER PROCEDURE dbo.INS_GroupOwner
    @IdGroupOwner   UNIQUEIDENTIFIER,
    @IdOwner        UNIQUEIDENTIFIER,
    @Name           NVARCHAR(20),
    @AreaTotal      DECIMAL(18,2),
    @TypeOwner      INT,
    @GroupNumber    INT = NULL
AS
BEGIN
    INSERT INTO GroupUnit (IdGroupUnit, TotalArea, GroupNumber)
    VALUES (@IdGroupOwner, @AreaTotal, @GroupNumber);

    INSERT INTO OwnerGroupRole (IdGroupOwnerRol, IdOwner, Role, IdGroupUnit)
    VALUES (NEWID(), @IdOwner, @TypeOwner, @IdGroupOwner);
END
GO

-- Agrega un copropietario/residente a un grupo YA existente (sin crear grupo
-- nuevo) -- lo usa "Agregar residente". Mismo problema de origen que
-- INS_GroupOwner (el nombre correcto nunca existió, sólo la versión con
-- errata apuntando a la tabla vieja).
CREATE OR ALTER PROCEDURE dbo.INS_OwnerGroupOwner
    @IdGroupOwner   UNIQUEIDENTIFIER,
    @IdOwner        UNIQUEIDENTIFIER,
    @TypeOwner      INT
AS
BEGIN
    INSERT INTO OwnerGroupRole (IdGroupOwnerRol, IdOwner, Role, IdGroupUnit)
    VALUES (NEWID(), @IdOwner, @TypeOwner, @IdGroupOwner);
END
GO

-- Vincula una unidad física a un grupo -- el nombre dice "INS_" (histórico) pero
-- ahora es un UPDATE sobre RealEstateUnit, que es donde vive de verdad el FK
-- IdGroupUnit. No se renombra el proc para no tener que tocar
-- StoredProcedures.INS_GroupUnitOwner en el código.
CREATE OR ALTER PROCEDURE dbo.INS_GroupUnitOwner
    @IdUnit         UNIQUEIDENTIFIER,
    @IdGroupOwner   UNIQUEIDENTIFIER,
    @TypeGroupUnit  INT
AS
BEGIN
    UPDATE RealEstateUnit
    SET IdGroupUnit = @IdGroupOwner,
        TypeGroupUnit = @TypeGroupUnit
    WHERE IdUnit = @IdUnit;
END
GO

-- Actualiza el área total del grupo (se llama al final de armar un grupo nuevo,
-- con la suma real de las unidades ya asignadas) -- apuntaba a una tabla
-- "GroupOwner" que no existe; la real es GroupUnit.
CREATE OR ALTER PROCEDURE dbo.UPD_GroupOwner
    @IdGroupOwner   UNIQUEIDENTIFIER,
    @AreaTotal      DECIMAL(18,2)
AS
BEGIN
    UPDATE GroupUnit
    SET TotalArea = @AreaTotal
    WHERE IdGroupUnit = @IdGroupOwner;
END
GO
