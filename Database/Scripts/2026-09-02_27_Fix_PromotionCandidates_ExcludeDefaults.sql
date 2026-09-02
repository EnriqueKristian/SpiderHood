-- =============================================================================
-- Fix a GET_MixtoParameterCandidates (2026-09-02_25_Parameter_Promotion.sql):
-- estaba trayendo TODOS los hijos Mixto activos de cada edificio, incluidos los
-- que vinieron clonados del Edificio Template al crear el edificio (Método de
-- Pago, Tipo de Incidente por default). Esos NO son duplicados reales -- son la
-- misma fila clonada N veces a propósito (una copia por edificio, ver §5.2), así
-- que nunca deberían aparecer como candidatos a fusionar. Confirmado en vivo por
-- el usuario: "Cheque"/"Efectivo"/"Tarj. de Débito"/"Tarjeta de Crédito" (los 4
-- defaults de Método de Pago) aparecían como "2 edificios" en cada edificio nuevo
-- creado, sólo porque el template y el edificio nuevo tienen cada uno su propia
-- copia clonada -- no porque un admin los haya agregado dos veces por separado.
--
-- La marca para distinguirlos ya existe (Parameter.IsSystemDefault en un HIJO: 1 =
-- vino clonado del template, 0 = lo agregó el admin a mano -- ver
-- Docs/Design-Defaults-Sistema-Mixto.md §5.2) -- sólo faltaba usarla acá. Con el
-- filtro nuevo, la lista sólo muestra valores que un Administrador agregó de
-- verdad por su cuenta (candidatos reales a duplicado entre edificios).
-- =============================================================================

SET NOCOUNT ON;
GO

CREATE OR ALTER PROCEDURE dbo.GET_MixtoParameterCandidates
AS
BEGIN
    SELECT
        c.IdTabla,
        c.ShortDescription,
        c.Description,
        c.Value,
        c.IdParent,
        r.ShortDescription AS GroupName,
        c.IdBuilding,
        b.Name             AS BuildingName,
        c.ReplacedByIdTabla
    FROM    dbo.Parameter c
    JOIN    dbo.Parameter r ON r.IdTabla = c.IdParent
    JOIN    dbo.Building  b ON b.IdBuilding = c.IdBuilding
    WHERE   r.IdParent IS NULL
      AND   r.IsSystemDefault = 0   -- sólo grupos Mixto -- Sistema no tiene copias por edificio que fusionar
      AND   c.IdBuilding IS NOT NULL -- ya globales quedan afuera, no son "duplicado" de nada
      AND   c.Estado = 1             -- sólo activos -- los ya fusionados quedan Inactivo y no vuelven a aparecer
      AND   c.IsSystemDefault = 0    -- NUEVO: excluye los clonados del template -- no son duplicados reales, sólo lo agregado a mano por un admin
    ORDER BY r.ShortDescription, c.ShortDescription, b.Name;
END
GO
