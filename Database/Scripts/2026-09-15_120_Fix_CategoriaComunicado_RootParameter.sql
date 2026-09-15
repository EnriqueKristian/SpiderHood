-- =============================================================================
-- Fix: 2026-09-11_97_Comunicado.sql sembraba el grupo raíz "Categoría
-- Comunicado" con @IdParent = 0 -- ese INS_Parameter falla con
-- "Msg 547 ... FK_Parametros_Parent" porque no existe ninguna fila con
-- IdTabla = 0 (los grupos raíz existentes, ej. "Tipo Edificio", usan
-- IdParent = NULL, no 0 -- confirmado contra la BD en vivo al intentar
-- re-sembrar este grupo durante el renombrado de Comunicados a inglés).
-- Como el primer INS_Parameter (el grupo) fallaba, @IdCategoriaComunicado
-- quedaba NULL y los 4 hijos (Mantenimiento Programado, Corte de Servicio,
-- Convocatoria de Reunión, Aviso General) se insertaban sueltos, sin padre.
--
-- Este script es idempotente: sólo actúa si el grupo "Categoría Comunicado"
-- no existe todavía (con IdParent NULL, el patrón correcto), y si encuentra
-- hijos sueltos (IdParent NULL) los cuelga del grupo recién creado.
-- =============================================================================

SET NOCOUNT ON;
GO

DECLARE @IdBuilding UNIQUEIDENTIFIER = (SELECT TOP 1 IdBuilding FROM dbo.Building);

IF @IdBuilding IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Parameter WHERE IdParent IS NULL AND ShortDescription = N'Categoría Comunicado' AND IdBuilding = @IdBuilding)
BEGIN
    EXEC dbo.INS_Parameter @Description = N'Categoría de Comunicado', @ShortDescription = N'Categoría Comunicado',
        @Value = 0, @Sort = 0, @IdParent = NULL, @Estado = 1, @IdBuilding = @IdBuilding;

    DECLARE @IdCategoriaComunicado INT = (SELECT TOP 1 IdTabla FROM dbo.Parameter
        WHERE IdParent IS NULL AND ShortDescription = N'Categoría Comunicado' AND IdBuilding = @IdBuilding ORDER BY IdTabla DESC);

    -- Cuelga del grupo recién creado a los 4 hijos si ya existen sueltos
    -- (IdParent NULL), o los crea si tampoco existen.
    IF EXISTS (SELECT 1 FROM dbo.Parameter WHERE IdParent IS NULL AND IdBuilding = @IdBuilding
               AND ShortDescription IN (N'Mantenimiento Programado', N'Corte de Servicio', N'Convocatoria de Reunión', N'Aviso General'))
        UPDATE dbo.Parameter SET IdParent = @IdCategoriaComunicado
        WHERE IdParent IS NULL AND IdBuilding = @IdBuilding
          AND ShortDescription IN (N'Mantenimiento Programado', N'Corte de Servicio', N'Convocatoria de Reunión', N'Aviso General');
    ELSE
    BEGIN
        EXEC dbo.INS_Parameter @Description = N'Mantenimiento Programado', @ShortDescription = N'Mantenimiento Programado',
            @Value = 1, @Sort = 1, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;
        EXEC dbo.INS_Parameter @Description = N'Corte de Servicio', @ShortDescription = N'Corte de Servicio',
            @Value = 2, @Sort = 2, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;
        EXEC dbo.INS_Parameter @Description = N'Convocatoria de Reunión', @ShortDescription = N'Convocatoria de Reunión',
            @Value = 3, @Sort = 3, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;
        EXEC dbo.INS_Parameter @Description = N'Aviso General', @ShortDescription = N'Aviso General',
            @Value = 4, @Sort = 4, @IdParent = @IdCategoriaComunicado, @Estado = 1, @IdBuilding = @IdBuilding;
    END
END
GO
