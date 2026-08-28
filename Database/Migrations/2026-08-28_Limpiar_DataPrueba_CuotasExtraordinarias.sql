-- =====================================================================================
-- Limpieza de datos de PRUEBA de Cuotas Extraordinarias / Multas / Mora
-- =====================================================================================
-- Borra únicamente lo que generan las páginas /cuotaextraordinaria y /multasymora:
--   - Installment con Type <> 0 (1=Extraordinaria, 2=Multa, 3=Mora)
--   - Sus pagos (InstallmentPaid), si llegaste a probar conciliación sobre ellas
--   - Los BudgetHeader que las agrupan (BudgetType = 'Extraordinario' o 'Cargos')
--
-- NO toca ninguna cuota Ordinaria ni ningún BudgetHeader del ciclo mensual normal —
-- ese BudgetType siempre queda en '' para las cuotas Ordinarias, así que el filtro
-- BudgetType IN ('Extraordinario','Cargos') no les pega.
--
-- Esto NO revierte el esquema (columnas Type/Concept/SourceInstallmentId, proc
-- INS_Installment) — para eso está el bloque DOWN del otro script
-- (2026-08-28_CuotasExtraordinarias_MultasMora.sql). Este script asume que quieres
-- QUEDARTE con el esquema y solo vaciar la data de prueba para volver a generar.
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1) Vista previa — correr esto primero y revisar los conteos/filas antes de borrar.
-- -------------------------------------------------------------------------------------
SELECT i.IdInstallment, i.UnitName, i.OwnerName, i.Amount, i.[Type], i.Concept, i.IdBudgetHeader
FROM   dbo.Installment i
WHERE  i.[Type] <> 0
ORDER BY i.CreationDate DESC;

SELECT bh.IdBudgetHeader, bh.BudgetName, bh.BudgetType, bh.Amount, bh.CreatedOn
FROM   dbo.BudgetHeader bh
WHERE  bh.BudgetType IN ('Extraordinario', 'Cargos');

-- -------------------------------------------------------------------------------------
-- 2) Borrado — envuelto en transacción: revisa los SELECT de abajo (deberían dar 0)
--    antes de decidir COMMIT o ROLLBACK.
-- -------------------------------------------------------------------------------------
BEGIN TRAN;

    DELETE ip
    FROM   dbo.InstallmentPaid ip
    JOIN   dbo.Installment i ON i.IdInstallment = ip.IdInstallment
    WHERE  i.[Type] <> 0;

    DELETE FROM dbo.Installment
    WHERE  [Type] <> 0;

    DELETE FROM dbo.BudgetHeader
    WHERE  BudgetType IN ('Extraordinario', 'Cargos');

    -- Deben dar 0 filas las dos:
    SELECT COUNT(*) AS InstallmentsRestantes FROM dbo.Installment WHERE [Type] <> 0;
    SELECT COUNT(*) AS BudgetHeadersRestantes FROM dbo.BudgetHeader WHERE BudgetType IN ('Extraordinario', 'Cargos');

-- Si los conteos de arriba dan 0 y no hubo errores:
-- COMMIT;

-- Si algo se ve mal:
-- ROLLBACK;
