-- =============================================================================
-- Paso 3 (sub-pasos 1 y 2) del plan en Docs/Design-Defaults-Sistema-Mixto.md.
--
-- Mecanismo: Parameter.IdBuilding pasa a admitir NULL -- mismo patrón que ya usa
-- IdParent para marcar "esto es raíz" (NULL en la BD, 0 en C# hoy). Acá:
--   IdBuilding = NULL  -> valor de Sistema (global, todos los edificios)
--   IdBuilding = <guid> -> valor Mixto propio de ESE edificio
-- La raíz de un grupo (IdParent IS NULL) SIEMPRE queda con IdBuilding = NULL, sea
-- el grupo Sistema o Mixto -- es una sola fila global en los dos casos, sólo para
-- que el IdParent de sus hijos sea estable. Lo que varía por tipo de grupo es si
-- los HIJOS también quedan en NULL (Sistema) o si quedan atados a un edificio
-- (Mixto).
--
-- IsSystemDefault tiene doble sentido según el tipo de fila (evita agregar una
-- columna más sólo para esto):
--   - En la RAÍZ de un grupo (IdParent IS NULL): 1 = el grupo ENTERO es Sistema
--     (nadie agrega hijos nunca, ni siquiera SysAdmin vía /parameter), 0 = el
--     grupo es Mixto (SysAdmin define el template, el admin de cada edificio
--     puede agregar hijos propios). Hace falta esta marca aparte de
--     IdBuilding==NULL porque la raíz SIEMPRE está en NULL en los dos casos --
--     sin esto no hay forma de distinguir un grupo Sistema de un Mixto que
--     todavía no tiene ningún hijo clonado.
--   - En un HIJO de un grupo Mixto: 1 si vino clonado del Edificio Template al
--     crear el edificio, 0 si lo agregó el admin a mano.
-- Es informativo (para poder mostrarlo/filtrarlo en /parameter) -- no habilita
-- borrado real ni nada: NINGÚN Parameter se borra de verdad, sólo se inactiva
-- (no hay FK real hacia Parameter en ninguna tabla que lo consuma, así que no hay
-- forma barata de saber si un valor está en uso -- ver Docs/Design-Defaults-
-- Sistema-Mixto.md §5.2).
--
-- La migración de abajo clasifica los 13 grupos que ya existen hoy (todos con el
-- mismo IdBuilding) según lo acordado: 11 pasan a Sistema, 2 (Método de Pago y
-- Tipo de Incidente) quedan Mixto con sus hijos actuales marcados
-- IsSystemDefault=1 (son, de hecho, los defaults del único edificio real que hay
-- hoy). Escrita por ShortDescription, no por IdTabla -- así no depende de que los
-- IDs de este dump coincidan con los de la BD real. Idempotente: correrla dos
-- veces no hace nada la segunda vez (los WHERE excluyen lo ya migrado).
-- =============================================================================

SET NOCOUNT ON;
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Parameter') AND name = 'IdBuilding' AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.Parameter ALTER COLUMN IdBuilding UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Parameter') AND name = 'IsSystemDefault'
)
BEGIN
    ALTER TABLE dbo.Parameter ADD IsSystemDefault BIT NOT NULL DEFAULT 0;
END
GO

-- -----------------------------------------------------------------------------
-- Grupos que pasan a Sistema (raíz + hijos -> IdBuilding = NULL)
-- -----------------------------------------------------------------------------
DECLARE @gruposSistema TABLE (ShortDescription NVARCHAR(200));
INSERT INTO @gruposSistema (ShortDescription) VALUES
    (N'Estado'),
    (N'Tipo Unidad'),
    (N'Distribución'),
    (N'Tipo Doc'),
    (N'Estado Gasto'),
    (N'Conciliación'),
    (N'Cuenta Bancaria'),
    (N'Tipo Edificio'),
    (N'Frecuencia'),
    (N'Estado Presupuesto'),
    (N'Prioridad Incidente');

UPDATE p
SET IdBuilding = NULL,
    IsSystemDefault = 1
FROM dbo.Parameter p
JOIN @gruposSistema g ON g.ShortDescription = p.ShortDescription
WHERE p.IdParent IS NULL AND (p.IdBuilding IS NOT NULL OR p.IsSystemDefault = 0);

UPDATE c
SET IdBuilding = NULL
FROM dbo.Parameter c
JOIN dbo.Parameter r ON r.IdTabla = c.IdParent
JOIN @gruposSistema g ON g.ShortDescription = r.ShortDescription
WHERE r.IdParent IS NULL AND c.IdBuilding IS NOT NULL;
GO

-- -----------------------------------------------------------------------------
-- Grupos que quedan Mixto: sólo la raíz pasa a NULL, los hijos actuales
-- conservan su IdBuilding y quedan marcados como default (IsSystemDefault=1)
-- -----------------------------------------------------------------------------
UPDATE p
SET IdBuilding = NULL
FROM dbo.Parameter p
WHERE p.IdParent IS NULL
  AND p.ShortDescription IN (N'Metodo de Pago', N'Tipo de Incidente')
  AND p.IdBuilding IS NOT NULL;

UPDATE c
SET IsSystemDefault = 1
FROM dbo.Parameter c
JOIN dbo.Parameter r ON r.IdTabla = c.IdParent
WHERE r.IdParent IS NULL
  AND r.ShortDescription IN (N'Metodo de Pago', N'Tipo de Incidente')
  AND c.IsSystemDefault = 0;
GO
