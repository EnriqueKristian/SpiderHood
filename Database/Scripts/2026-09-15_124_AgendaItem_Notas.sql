-- =============================================================================
-- Feedback del usuario tras probar Meetings en vivo: los puntos de agenda solo
-- tenían Título/Descripción (fijados al crear el punto, antes de la reunión) --
-- no había forma de que quien dirige la reunión fuera dejando notas de lo
-- conversado en cada punto, para que el Acta reflejara la discusión real y no
-- solo el título + el resultado de la votación.
--
-- Columna nueva NVARCHAR NULL -- ALTER TABLE, no DROP+CREATE, para no perder
-- las reuniones/puntos de agenda ya cargados en la BD de pruebas.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.AgendaItem') AND name = 'Notas'
)
BEGIN
    ALTER TABLE dbo.AgendaItem ADD Notas NVARCHAR(2000) NULL;
END
GO

-- Update angosto, separado de UPD_AgendaItem (ese sigue restringido a Estado=Convocada
-- por IMeetingService.EditarAgendaItemAsync) -- las notas se cargan DURANTE/DESPUÉS de
-- la reunión, cuando el punto ya no es editable por esa vía. Mismo patrón que
-- UPD_AgendaItemEstado.
CREATE OR ALTER PROCEDURE dbo.UPD_AgendaItemNotas
    @IdAgendaItem UNIQUEIDENTIFIER,
    @Notas NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AgendaItem SET Notas = @Notas WHERE IdAgendaItem = @IdAgendaItem;
END
GO
