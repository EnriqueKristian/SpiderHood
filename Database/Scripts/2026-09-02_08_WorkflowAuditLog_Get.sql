-- =============================================================================
-- Lectura del historial de WorkflowAuditLog por módulo+entidad (para mostrar la
-- línea de tiempo en el detalle de Incidentes -- ver
-- Database/Scripts/2026-09-01_02_WorkflowAuditLog.sql, hasta ahora solo se
-- insertaba, nunca se leía de vuelta).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_WorkflowAuditLog
    @Module NVARCHAR(50),
    @EntityId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Module, EntityId, Action, PerformedBy, PerformedOn, Comment, IdBuilding
    FROM dbo.WorkflowAuditLog
    WHERE Module = @Module AND EntityId = @EntityId
    ORDER BY PerformedOn ASC;
END
GO
