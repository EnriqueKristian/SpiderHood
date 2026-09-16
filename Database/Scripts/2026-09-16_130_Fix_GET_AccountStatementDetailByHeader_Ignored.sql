-- =============================================================================
-- Fix: reportes financieros ("Ingresos y Egresos" y el gráfico del Dashboard)
-- suman transacciones marcadas como "Ignorado" en Conciliación (ej. un error
-- bancario revertido) -- Docs/Pendientes-Negocio-Consolidado.md #2.
--
-- GET_AccountStatementDetailByHeader (SP no versionado en el repo hasta ahora
-- -- este script trae su texto real, obtenido de la BD, como punto de
-- partida) no seleccionaba dbo.AccountStatementDetail.Ignored, así que
-- AccountStatementDetailView no tenía forma de excluir esas filas.
-- IncomeExpenseReport.razor y Home.razor.cs (RecalcularGraficoIngresos) filtran
-- ahora por !d.Ignored del lado C# -- ver esos commits.
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE [dbo].[GET_AccountStatementDetailByHeader]
    @IdStatementHeader UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.IdStatementDetail,
            d.IdStatementHeader,
            d.StatementDate,
            d.Description,
            d.Currency,
            d.Amount,
            d.AmountInReportingCurrency,
            d.SequenceNumber,
            d.ReconciliationStatus,
            d.ReconciliationDate,
            d.Ignored
    FROM    dbo.AccountStatementDetail d
    WHERE   d.IdStatementHeader = @IdStatementHeader
    ORDER BY d.StatementDate, d.SequenceNumber;
END
GO
