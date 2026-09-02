-- =============================================================================
-- Paso 5 del plan en Docs/Design-Defaults-Sistema-Mixto.md §5.3: promoción/fusión
-- de duplicados entre edificios (ej. "Yape" agregado independientemente como
-- Método de Pago en 5 edificios distintos).
--
-- Mecanismo:
--   1) Columna nueva Parameter.ReplacedByIdTabla (NULL = no reemplazada). FK
--      auto-referenciada hacia Parameter.IdTabla -- segura de agregar, la columna
--      arranca NULL para todas las filas existentes, no puede haber huérfanos.
--   2) GET_MixtoParameterCandidates: todos los hijos Mixto ACTIVOS y todavía NO
--      globales (IdBuilding IS NOT NULL), de TODOS los edificios a la vez, con el
--      nombre de su grupo y de su edificio -- es la vista que un SysAdmin usa para
--      detectar a ojo los duplicados (la detección queda manual a propósito, ver
--      §5.3, "no se arma nada automático por ahora").
--   3) UPD_PromoteParameterToGlobal: saca a UNA fila de su edificio (IdBuilding ->
--      NULL) -- pasa a comportarse como un valor Sistema (GET_AllParameters ya trae
--      todo lo que tiene IdBuilding NULL), aunque el grupo siga siendo Mixto.
--   4) UPD_MergeParameterInto: la fila duplicada queda Inactivo (Estado = 0) y
--      apuntando a la canónica via ReplacedByIdTabla -- NUNCA se borra (ningún
--      Parameter se borra de verdad, ver §5.2), así que el histórico que la
--      referencia (Incident, Expense, etc.) sigue viéndose bien.
--
-- La UI (SpiderHood/Components/Pages/ParameterPages/ParameterPromotion.razor,
-- SysAdmin-only) llama primero a UPD_PromoteParameterToGlobal sobre la fila elegida
-- como canónica y después a UPD_MergeParameterInto una vez por cada duplicado --
-- ambos pasos son idempotentes (repetirlos no rompe nada), así que no hace falta
-- envolverlos en una transacción compartida entre llamadas.
-- =============================================================================

SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Parameter') AND name = 'ReplacedByIdTabla'
)
BEGIN
    ALTER TABLE dbo.Parameter ADD ReplacedByIdTabla INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parameter_ReplacedByIdTabla'
)
BEGIN
    ALTER TABLE dbo.Parameter WITH CHECK
    ADD CONSTRAINT FK_Parameter_ReplacedByIdTabla FOREIGN KEY (ReplacedByIdTabla) REFERENCES dbo.Parameter(IdTabla);
END
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
    ORDER BY r.ShortDescription, c.ShortDescription, b.Name;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_PromoteParameterToGlobal
    @IdTabla INT
AS
BEGIN
    UPDATE dbo.Parameter SET IdBuilding = NULL WHERE IdTabla = @IdTabla;
END
GO

CREATE OR ALTER PROCEDURE dbo.UPD_MergeParameterInto
    @OldIdTabla INT,
    @NewIdTabla INT
AS
BEGIN
    UPDATE dbo.Parameter
    SET Estado = 0, ReplacedByIdTabla = @NewIdTabla
    WHERE IdTabla = @OldIdTabla;
END
GO
