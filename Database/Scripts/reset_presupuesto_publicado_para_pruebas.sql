-- Revierte un presupuesto PUBLICADO (Active) de vuelta a Created, para poder repetir la
-- prueba del workflow de aprobación desde cero: borra las Installment que se generaron al
-- publicar, el snapshot de InstallmentExoneration, revierte el Status de la lectura de
-- agua que quedó marcada como "usada", y deja el BudgetHeader en Created.
--
-- Basado exactamente en lo que escribe SaveInstallment/SaveBudgetAsync al publicar
-- (Services/IBudgetService.cs:349-489):
--   1) INSERT en Installment por cada Installment de state.Installments (IdBudgetHeader).
--   2) UPDATE ServiceReading.Status = 2 en la lectura de agua usada para el cálculo.
--   3) INSERT de UNA fila en InstallmentExoneration (snapshot, IdBudgetHeader/IdBuilding).
--   4) UPDATE BudgetHeader.Status = Active (4).
--   5) Si había un presupuesto anterior en Active, ClosePastBudgets lo pasa a Closed (6)
--      — ver el bloque OPCIONAL al final si también querés revertir eso.
--
-- BudgetStatus (Models/enum.cs): Created=1, Check=2, Approved=3, Active=4, Rejected=5, Closed=6.
--
-- Los nombres de tabla (dbo.Installment, dbo.InstallmentExoneration, dbo.ServiceReading,
-- dbo.BudgetHeader) siguen el mismo patrón 1:1 con el nombre de la clase C# que ya
-- confirmamos en Category/BankAccount/BuildingConfiguration — pero no tengo forma de
-- verificarlos yo mismo (sin acceso a la BD). Corré los SELECT de conteo primero; si algún
-- nombre no existe, el error te va a decir cuál ajustar.
--
-- DESTRUCTIVO: revisá cada SELECT antes de avanzar. Termina con COMMIT/ROLLBACK manual,
-- mismo patrón que las otras migraciones de esta carpeta.

DECLARE @IdBudgetHeader UNIQUEIDENTIFIER = 'PEGAR-AQUI-EL-ID-DEL-PRESUPUESTO';

BEGIN TRAN;

-- 0) Ver el presupuesto y qué se va a borrar/cambiar ANTES de tocar nada.
SELECT * FROM dbo.BudgetHeader WHERE IdBudgetHeader = @IdBudgetHeader;

SELECT * FROM dbo.Installment WHERE IdBudgetHeader = @IdBudgetHeader;

SELECT * FROM dbo.InstallmentExoneration WHERE IdBudgetHeader = @IdBudgetHeader;

-- Lectura(s) de agua candidatas a revertir: mismo edificio + mismo mes que el presupuesto.
-- Confirmá visualmente cuál es antes de correr el UPDATE del paso 3 — si hay más de una
-- fila o ninguna, ajustá el WHERE a mano en vez de asumir.
SELECT sr.*
FROM dbo.ServiceReading sr
JOIN dbo.BudgetHeader bh ON bh.IdBuilding = sr.IdBuilding
WHERE bh.IdBudgetHeader = @IdBudgetHeader
  AND YEAR(sr.Period) = YEAR(bh.BudgetDate)
  AND MONTH(sr.Period) = MONTH(bh.BudgetDate);

-- 1) Borrar las cuotas (Installment) generadas al publicar.
DELETE FROM dbo.Installment WHERE IdBudgetHeader = @IdBudgetHeader;

-- 2) Borrar el snapshot de excepciones generado al publicar.
DELETE FROM dbo.InstallmentExoneration WHERE IdBudgetHeader = @IdBudgetHeader;

-- 3) Revertir el Status de la lectura de agua usada (2 = usada -> 1 = pendiente), la misma
--    que aparece en el SELECT de arriba. Si el SELECT mostró más de una fila o ninguna,
--    reemplazá el JOIN por el/los IdServiceReading correctos a mano.
UPDATE sr
SET sr.Status = 1
FROM dbo.ServiceReading sr
JOIN dbo.BudgetHeader bh ON bh.IdBuilding = sr.IdBuilding
WHERE bh.IdBudgetHeader = @IdBudgetHeader
  AND YEAR(sr.Period) = YEAR(bh.BudgetDate)
  AND MONTH(sr.Period) = MONTH(bh.BudgetDate);

-- 4) Devolver el presupuesto a Created (1).
UPDATE dbo.BudgetHeader
SET Status = 1
WHERE IdBudgetHeader = @IdBudgetHeader;

-- Verificar el resultado antes de confirmar.
SELECT * FROM dbo.BudgetHeader WHERE IdBudgetHeader = @IdBudgetHeader;
SELECT COUNT(*) AS InstallmentsRestantes FROM dbo.Installment WHERE IdBudgetHeader = @IdBudgetHeader;

-- Si los números y el Status se ven bien:
-- COMMIT;
-- Si algo no cuadra:
-- ROLLBACK;


-- =====================================================================================
-- OPCIONAL: si al publicar este presupuesto se cerró automáticamente el anterior
-- (UPD_ClosePastBudgets, ver Services/IBudgetService.cs:395-396 / BDLayout.Additional.cs)
-- y para la prueba también lo querés de vuelta en Active, identificalo primero — NO
-- reactives a ciegas el último Closed, podría estar cerrado por otro motivo legítimo:
-- =====================================================================================
-- SELECT TOP 5 IdBudgetHeader, BudgetDate, Status
-- FROM dbo.BudgetHeader
-- WHERE IdBuilding = (SELECT IdBuilding FROM dbo.BudgetHeader WHERE IdBudgetHeader = @IdBudgetHeader)
--   AND IdBudgetHeader <> @IdBudgetHeader
-- ORDER BY BudgetDate DESC;
--
-- Con el Id correcto (el que tenga la fecha inmediata anterior y Status = 6):
-- UPDATE dbo.BudgetHeader SET Status = 4 WHERE IdBudgetHeader = 'ID-DEL-PRESUPUESTO-ANTERIOR';
