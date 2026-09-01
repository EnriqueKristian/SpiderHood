-- =============================================================================
-- Siembra "Tipo Incidente" y "Prioridad Incidente" como grupos de Parámetros
-- (Configuración > Parámetros), reemplazando los enums fijos IncidentType/
-- IncidentPriority -- así un Administrador puede agregar/desactivar valores
-- sin desplegar código nuevo.
--
-- Usa dbo.INS_Parameter (el mismo SP que ya usa /parameter) por POSICIÓN, no
-- por nombre de parámetro: no tengo el CREATE PROCEDURE original a la vista
-- para confirmar cómo se llaman sus @parámetros, pero sí conozco el ORDEN
-- exacto porque es el mismo que arma BDLayout.Add.cs (IdTabla, Description,
-- ShortDescription, Value, Sort, IdParent, Estado). @IdTabla se manda en 0,
-- igual que hace la propia pantalla /parameter al crear uno nuevo (según lo
-- que ya probaste ahí) -- asumo que el SP ignora ese valor y la columna
-- autogenera el ID real.
--
-- Por eso, en vez de un ParamParent.IncidentType = <número fijo> (como los
-- existentes State/UnitType/ExpenseDistribution/DocumentType, que dependen
-- de conocer de antemano el IdTabla que les tocó), el código C# busca estos
-- dos grupos por ShortDescription en tiempo de ejecución -- ver
-- IncidentList.razor. Es más robusto: no importa qué IdTabla les asigne tu
-- base en particular.
--
-- @Estado se manda como 1 (activo) -- ParameterEstado.Activo es 1 en el
-- enum C# y Parameter.Estado no tiene HasConversion<string>() en
-- SpiderHoodContext, así que EF lo mapea como int por default.
--
-- NO es idempotente (mismo motivo que los otros seeds de este proyecto: no
-- puedo armar un IF NOT EXISTS confiable sin conocer el esquema real). Si lo
-- corrés dos veces vas a duplicar los grupos -- revisá /parameter antes de
-- repetirlo.
-- =============================================================================

SET NOCOUNT ON;

-- -----------------------------------------------------------------------------
-- Grupo "Tipo Incidente"
-- -----------------------------------------------------------------------------
EXEC dbo.INS_Parameter 0, N'Tipo de Incidente', N'Tipo Incidente', 0, 0, 0, 1;

DECLARE @IdTipoIncidente INT = (
    SELECT TOP 1 IdTabla FROM dbo.Parameter
    WHERE IdParent = 0 AND ShortDescription = N'Tipo Incidente'
    ORDER BY IdTabla DESC
);

EXEC dbo.INS_Parameter 0, N'Plomería', N'Plomería', 1, 1, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Eléctrico', N'Eléctrico', 2, 2, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Seguridad', N'Seguridad', 3, 3, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Ascensor', N'Ascensor', 4, 4, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Áreas Comunes', N'Áreas Comunes', 5, 5, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Ruido', N'Ruido', 6, 6, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Limpieza', N'Limpieza', 7, 7, @IdTipoIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Otro', N'Otro', 8, 8, @IdTipoIncidente, 1;

-- -----------------------------------------------------------------------------
-- Grupo "Prioridad Incidente"
-- -----------------------------------------------------------------------------
EXEC dbo.INS_Parameter 0, N'Prioridad de Incidente', N'Prioridad Incidente', 0, 0, 0, 1;

DECLARE @IdPrioridadIncidente INT = (
    SELECT TOP 1 IdTabla FROM dbo.Parameter
    WHERE IdParent = 0 AND ShortDescription = N'Prioridad Incidente'
    ORDER BY IdTabla DESC
);

EXEC dbo.INS_Parameter 0, N'Baja', N'Baja', 1, 1, @IdPrioridadIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Media', N'Media', 2, 2, @IdPrioridadIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Alta', N'Alta', 3, 3, @IdPrioridadIncidente, 1;
EXEC dbo.INS_Parameter 0, N'Urgente', N'Urgente', 4, 4, @IdPrioridadIncidente, 1;
