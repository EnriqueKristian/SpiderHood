/*
    Stored procedures faltantes para Períodos (/periods).

    PeriodService.UpdatePeriodAsync / DeletePeriodAsync / SetAsCurrentPeriodAsync
    eran stubs que siempre devolvían false porque no existía ningún stored
    procedure para esas operaciones (solo estaban INS_Periods,
    GET_PeriodsByBuilding y UPD_UnsetOtherCurrentPeriods).

    Este script agrega los que faltan, siguiendo la misma convención de
    nombres/columnas que INS_Periods (ver BDLayout.Add.cs) y reutilizando
    UPD_UnsetOtherCurrentPeriods (ya existente) para la mitad del flujo de
    "marcar como actual".

    Nombre de tabla y columnas inferidos de Models/Expense.cs (clase Period) y
    del orden exacto de parámetros con el que el código ya llama a
    INS_Periods. Si el nombre real de la tabla difiere de "Periods", ajustalo
    antes de ejecutar.

    Ejecutar contra la misma base que usa la app (ver connection string
    "SpiderHoodContext" en appsettings.json).
*/

CREATE OR ALTER PROCEDURE UPD_Period
    @IdPeriod        UNIQUEIDENTIFIER,
    @Name            NVARCHAR(200),
    @PeriodType      INT,
    @StartDate       DATETIME2,
    @EndDate         DATETIME2,
    @ClosingDate     DATETIME2,
    @Status          INT,
    @IsCurrentPeriod BIT,
    @Description     NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Periods
    SET
        Name            = @Name,
        PeriodType      = @PeriodType,
        StartDate       = @StartDate,
        EndDate         = @EndDate,
        ClosingDate     = @ClosingDate,
        Status          = @Status,
        IsCurrentPeriod = @IsCurrentPeriod,
        Description     = @Description
    WHERE IdPeriod = @IdPeriod;
END
GO

CREATE OR ALTER PROCEDURE DEL_Period
    @IdPeriod UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM Periods
    WHERE IdPeriod = @IdPeriod;
END
GO

-- Complementa a UPD_UnsetOtherCurrentPeriods (que ya desmarca a todos los
-- demás períodos del edificio): esta solo marca al elegido como el actual.
CREATE OR ALTER PROCEDURE UPD_SetPeriodAsCurrent
    @IdPeriod UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Periods
    SET IsCurrentPeriod = 1,
        Status = 1
    WHERE IdPeriod = @IdPeriod;
END
GO
