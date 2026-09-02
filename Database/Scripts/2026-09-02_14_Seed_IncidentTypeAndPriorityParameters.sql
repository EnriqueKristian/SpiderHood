-- =============================================================================
-- Siembra "Tipo Incidente" y "Prioridad Incidente" como grupos de Parámetros
-- (Configuración > Parámetros), reemplazando los enums fijos IncidentType/
-- IncidentPriority -- así un Administrador puede agregar/desactivar valores
-- sin desplegar código nuevo.
--
-- FIX sobre la versión anterior de este script: asumí mal la firma de
-- dbo.INS_Parameter (le agregué un @IdTabla que no existe y le faltó
-- @IdBuilding, que sí es obligatorio -- Parametros está scopeado por
-- edificio). Firma real (la compartiste vos):
--   INS_Parameter(@Description, @ShortDescription, @Value, @Sort,
--                 @IdParent = NULL, @Estado = 1, @IdBuilding)
-- Ahora se llama por nombre de parámetro, no por posición, para no repetir
-- el mismo tipo de error.
--
-- @IdBuilding: como no puedo saber cuál es el tuyo desde acá, se toma el
-- primer Building que encuentre. Si administrás más de un edificio en esta
-- base, AJUSTÁ la variable @IdBuilding de abajo antes de correrlo (y si
-- necesitás los mismos Tipos/Prioridades en más de un edificio, corré el
-- script una vez por cada uno, cambiando esa variable).
--
-- IdTabla es IDENTITY (se autogenera) -- por eso, para poder ligar los hijos
-- a su padre recién creado, se busca el padre de nuevo por Description +
-- IdBuilding inmediatamente después de insertarlo, en vez de asumir un ID.
--
-- NO es idempotente. Si lo corrés dos veces vas a duplicar los grupos --
-- revisá /parameter antes de repetirlo.
-- =============================================================================

SET NOCOUNT ON;

DECLARE @IdBuilding UNIQUEIDENTIFIER = (SELECT TOP 1 IdBuilding FROM dbo.Building);

IF @IdBuilding IS NULL
BEGIN
    RAISERROR('No se encontró ningún Building en dbo.Building -- ajustá @IdBuilding a mano antes de correr este script.', 16, 1);
    RETURN;
END

-- -----------------------------------------------------------------------------
-- Grupo "Tipo Incidente"
-- -----------------------------------------------------------------------------
EXEC dbo.INS_Parameter
    @Description = N'Tipo de Incidente',
    @ShortDescription = N'Tipo Incidente',
    @Value = 0,
    @Sort = 0,
    @IdParent = 0,
    @Estado = 1,
    @IdBuilding = @IdBuilding;

DECLARE @IdTipoIncidente INT = (
    SELECT TOP 1 IdTabla FROM dbo.Parameter
    WHERE IdParent = 0 AND ShortDescription = N'Tipo Incidente' AND IdBuilding = @IdBuilding
    ORDER BY IdTabla DESC
);

EXEC dbo.INS_Parameter @Description = N'Plomería', @ShortDescription = N'Plomería', @Value = 1, @Sort = 1, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Eléctrico', @ShortDescription = N'Eléctrico', @Value = 2, @Sort = 2, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Seguridad', @ShortDescription = N'Seguridad', @Value = 3, @Sort = 3, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Ascensor', @ShortDescription = N'Ascensor', @Value = 4, @Sort = 4, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Áreas Comunes', @ShortDescription = N'Áreas Comunes', @Value = 5, @Sort = 5, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Ruido', @ShortDescription = N'Ruido', @Value = 6, @Sort = 6, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Limpieza', @ShortDescription = N'Limpieza', @Value = 7, @Sort = 7, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Otro', @ShortDescription = N'Otro', @Value = 8, @Sort = 8, @IdParent = @IdTipoIncidente, @Estado = 1, @IdBuilding = @IdBuilding;

-- -----------------------------------------------------------------------------
-- Grupo "Prioridad Incidente"
-- -----------------------------------------------------------------------------
EXEC dbo.INS_Parameter
    @Description = N'Prioridad de Incidente',
    @ShortDescription = N'Prioridad Incidente',
    @Value = 0,
    @Sort = 0,
    @IdParent = 0,
    @Estado = 1,
    @IdBuilding = @IdBuilding;

DECLARE @IdPrioridadIncidente INT = (
    SELECT TOP 1 IdTabla FROM dbo.Parameter
    WHERE IdParent = 0 AND ShortDescription = N'Prioridad Incidente' AND IdBuilding = @IdBuilding
    ORDER BY IdTabla DESC
);

EXEC dbo.INS_Parameter @Description = N'Baja', @ShortDescription = N'Baja', @Value = 1, @Sort = 1, @IdParent = @IdPrioridadIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Media', @ShortDescription = N'Media', @Value = 2, @Sort = 2, @IdParent = @IdPrioridadIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Alta', @ShortDescription = N'Alta', @Value = 3, @Sort = 3, @IdParent = @IdPrioridadIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
EXEC dbo.INS_Parameter @Description = N'Urgente', @ShortDescription = N'Urgente', @Value = 4, @Sort = 4, @IdParent = @IdPrioridadIncidente, @Estado = 1, @IdBuilding = @IdBuilding;
