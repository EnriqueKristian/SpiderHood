-- =============================================================================
-- Paso 1 del plan en Docs/Design-Defaults-Sistema-Mixto.md: hoy crear un Building
-- desde /buildings NO persiste nada -- BuildingPage.razor.cs SaveBuilding() sólo
-- agrega el objeto a una lista en memoria (Buildings.Add(...)), no hay ningún
-- INS_Building ni AddNewRecordAsync(Building) en el código. Este script agrega el
-- procedimiento que falta.
--
-- IMPORTANTE -- supuestos de schema, NO confirmados contra un CREATE TABLE real:
-- la tabla dbo.Building no está en ningún script de este repo (existe de antes),
-- así que las columnas de abajo se infieren de dos fuentes:
--   1) La clase Models.Building (Classes/Building.cs): IdBuilding, Number, Name,
--      Location, Type, Floors, Basements, Apartments, Parkings, Deposits, Others,
--      TotalArea, IsActive.
--   2) El único INS/UPD que ya existía (BDLayout.Update.cs, UpdateRecordAsync
--      (Building)), que sólo tocaba 4: IdBuilding, Name, Location, TotalArea --
--      eso confirma que esos 4 nombres de columna son correctos, pero no dice
--      nada de los demás.
-- Si algún nombre/tipo no matchea la tabla real, correlo igual (CREATE OR ALTER no
-- rompe nada existente) y avisame el error real de SQL Server para corregirlo --
-- mismo patrón que con INS_Parameter/UPD_Parameter en esta misma sesión.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Building
    @IdBuilding     UNIQUEIDENTIFIER,
    @Number         INT,
    @Name           NVARCHAR(200),
    @Location       NVARCHAR(300),
    @Type           INT,
    @Floors         INT,
    @Basements      INT,
    @Apartments     INT,
    @Parkings       INT,
    @Deposits       INT,
    @Others         INT,
    @TotalArea      DECIMAL(18, 2),
    @IsActive       BIT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.Building
        (IdBuilding, Number, Name, Location, Type, Floors, Basements, Apartments,
         Parkings, Deposits, Others, TotalArea, IsActive)
    VALUES
        (@IdBuilding, @Number, @Name, @Location, @Type, @Floors, @Basements,
         @Apartments, @Parkings, @Deposits, @Others, @TotalArea, @IsActive);
END
GO

-- Reemplaza el UPD_Building anterior (sólo actualizaba Name/Location/TotalArea) --
-- el modal de edición en BuildingPage.razor también edita Type/Floors/Basements/
-- Apartments/Parkings/Deposits/Others/IsActive, que se perdían en cada guardado.
CREATE OR ALTER PROCEDURE dbo.UPD_Building
    @IdBuilding     UNIQUEIDENTIFIER,
    @Number         INT,
    @Name           NVARCHAR(200),
    @Location       NVARCHAR(300),
    @Type           INT,
    @Floors         INT,
    @Basements      INT,
    @Apartments     INT,
    @Parkings       INT,
    @Deposits       INT,
    @Others         INT,
    @TotalArea      DECIMAL(18, 2),
    @IsActive       BIT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Building
    SET Number = @Number,
        Name = @Name,
        Location = @Location,
        Type = @Type,
        Floors = @Floors,
        Basements = @Basements,
        Apartments = @Apartments,
        Parkings = @Parkings,
        Deposits = @Deposits,
        Others = @Others,
        TotalArea = @TotalArea,
        IsActive = @IsActive
    WHERE IdBuilding = @IdBuilding;
END
GO
