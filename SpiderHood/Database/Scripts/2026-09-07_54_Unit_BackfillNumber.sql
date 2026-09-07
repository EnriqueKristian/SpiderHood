-- Corrige dbo.RealEstateUnit.Number para las filas ya existentes.
--
-- Number se agregó para poder ordenar la grilla numéricamente (UnitNumber es
-- texto, así que "1001" ordena antes que "101"), pero ningún flujo de alta
-- (ModalUnit.razor / UnitGroups.razor.GenerateBlockUnits) lo llenaba nunca --
-- INS_Unit siempre recibía 0 para ese parámetro. Eso ya se corrigió en código
-- (UnitGroups.razor ahora completa Number en cada alta/edición, ver SaveItem/
-- GenerateBlockUnits), pero las unidades creadas antes de ese fix quedaron con
-- Number = 0 en la base.
--
-- Este script rellena esas filas extrayendo los dígitos de UnitNumber (mismo
-- criterio que ExtractUnitNumber en UnitGroups.razor): "101" -> 101,
-- "1001" -> 1001, "E1" (Estacionamiento) -> 1. Sólo toca filas con
-- Number = 0 (o NULL) y sólo cuando UnitNumber tiene al menos un dígito --
-- idempotente, no pasa nada si se corre más de una vez.
--
-- Ejecutar contra la base de datos de la app (ver DEPLOY-Production.md §2).

;WITH Tally AS (
    SELECT TOP (50) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects
),
Digits AS (
    SELECT u.IdUnit,
           (
               SELECT SUBSTRING(u.UnitNumber, t.n, 1)
               FROM Tally t
               WHERE t.n <= LEN(u.UnitNumber)
                 AND SUBSTRING(u.UnitNumber, t.n, 1) LIKE '[0-9]'
               FOR XML PATH('')
           ) AS DigitsOnly
    FROM dbo.RealEstateUnit u
    WHERE ISNULL(u.Number, 0) = 0
)
UPDATE ru
SET ru.Number = TRY_CAST(d.DigitsOnly AS INT)
FROM dbo.RealEstateUnit ru
JOIN Digits d ON d.IdUnit = ru.IdUnit
WHERE d.DigitsOnly IS NOT NULL
  AND d.DigitsOnly <> ''
  AND TRY_CAST(d.DigitsOnly AS INT) IS NOT NULL;
