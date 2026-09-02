-- =============================================================================
-- Fix sobre 2026-09-02_17_Persist_BuildingCreation.sql: asumí que Number era una
-- columna normal seteable por la app -- es IDENTITY (autogenerada por SQL Server).
-- Confirmado por el error real al probarlo:
--   Msg 8102, Level 16, State 1, Procedure UPD_Building, Line 22
--   Cannot update identity column 'Number'.
-- Se saca @Number de ambos procedimientos (ni se inserta ni se actualiza -- una
-- columna IDENTITY no se puede tocar desde INSERT/UPDATE sin
-- SET IDENTITY_INSERT, que no aplica acá).
--
-- NOTA para BuildingPage.razor.cs: el Number que la app calcula al mostrar
-- ShowCreateModal (Buildings.Count + 1) queda como valor local sólo para mostrar
-- en el modal antes de guardar -- el real lo asigna SQL Server y puede no
-- coincidir exactamente (por ejemplo si hay edificios inactivos de por medio). No
-- se lee de vuelta el valor real generado en este paso; si en algún momento se
-- necesita mostrarlo con precisión inmediatamente después de crear, hay que
-- recargar el edificio desde la BD o agregar un OUTPUT a INS_Building.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.INS_Building
    @IdBuilding     UNIQUEIDENTIFIER,
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
        (IdBuilding, Name, Location, Type, Floors, Basements, Apartments,
         Parkings, Deposits, Others, TotalArea, IsActive)
    VALUES
        (@IdBuilding, @Name, @Location, @Type, @Floors, @Basements,
         @Apartments, @Parkings, @Deposits, @Others, @TotalArea, @IsActive);
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_Building
    @IdBuilding     UNIQUEIDENTIFIER,
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
    SET Name = @Name,
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
