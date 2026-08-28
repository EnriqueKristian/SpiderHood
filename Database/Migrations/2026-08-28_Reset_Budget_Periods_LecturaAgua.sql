-- =====================================================================================
-- Reset de datos de prueba: Presupuestos (Budget), Periodos y Lecturas de Agua
-- =====================================================================================
-- Borra TODO lo generado por el ciclo de Presupuesto/Cuotas — incluye las Cuotas
-- Extraordinarias y los Cargos de Multas/Mora, que también viven como BudgetHeader
-- (BudgetType = 'Extraordinario'/'Cargos') — más las Lecturas de Agua, para volver a
-- probar el sistema desde cero.
--
-- SE MANTIENE (esta corrida no toca nada de esto):
--   Owner, GroupOwner, OwnerGroupOwner (Propietarios)
--   GroupUnitOwner, GroupUnit, Unit (Residentes/Unidades/Grupos de Unidad)
--   Category (Categorías)
--   Parameter y tablas de parámetros (Parámetros)
--   MenuItem, MenuItemPermission (Menús)
--   Role, RolePermissions, UserRole (Permisos)
--   Building, BuildingConfiguration, BankAccount, Contact
--   Exoneration (la excepción MAESTRA configurada por unidad/categoría — NO confundir
--   con InstallmentExoneration, que sí se borra: es el histórico de qué exoneraciones
--   se consideraron en cada cálculo de presupuesto, no la configuración en sí)
--   TransactionBankDetail / estados de cuenta bancarios importados (ver nota al final)
--
-- RECOMENDADO: backup completo antes de correr esto.
--   BACKUP DATABASE [NombreDeTuBD] TO DISK = N'C:\Backups\SpiderHood_antes_reset.bak';
--
-- Nota sobre nombres de tabla: Installment y BudgetHeader ya se verificaron contra la
-- base real (sp_helptext, migración anterior). BudgetDetail, InstallmentPaid,
-- InstallmentExoneration, Period, ServiceReading y ServiceReadingDetail siguen la misma
-- convención (nombre de tabla = nombre de la clase C#) pero no se verificaron una por
-- una — si algún nombre no coincide con tu base, ese DELETE puntual falla con "Invalid
-- object name" y no se borra nada de esa tabla ni de las que siguen (todo está dentro
-- de la misma transacción), así que no hay riesgo de borrado parcial silencioso.
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 1) Vista previa — conteos de lo que se va a borrar. Correr primero y revisar.
-- -------------------------------------------------------------------------------------
SELECT 'BudgetHeader' AS Tabla, COUNT(*) AS Filas FROM dbo.BudgetHeader
UNION ALL SELECT 'BudgetDetail', COUNT(*) FROM dbo.BudgetDetail
UNION ALL SELECT 'Installment', COUNT(*) FROM dbo.Installment
UNION ALL SELECT 'InstallmentPaid', COUNT(*) FROM dbo.InstallmentPaid
UNION ALL SELECT 'InstallmentExoneration', COUNT(*) FROM dbo.InstallmentExoneration
UNION ALL SELECT 'Period', COUNT(*) FROM dbo.Period
UNION ALL SELECT 'ServiceReading', COUNT(*) FROM dbo.ServiceReading
UNION ALL SELECT 'ServiceReadingDetail', COUNT(*) FROM dbo.ServiceReadingDetail;

-- -------------------------------------------------------------------------------------
-- 2) Borrado — envuelto en transacción, hijos antes que padres. Revisa los conteos de
--    verificación al final (todos deben dar 0) antes de decidir COMMIT o ROLLBACK.
-- -------------------------------------------------------------------------------------
BEGIN TRAN;

    -- Pagos de cuotas (de cualquier Type: Ordinaria, Extraordinaria, Multa, Mora)
    DELETE FROM dbo.InstallmentPaid;

    -- Histórico de exoneraciones consideradas en cada cálculo de presupuesto
    DELETE FROM dbo.InstallmentExoneration;

    -- Todas las cuotas: Ordinarias del ciclo mensual + Extraordinarias/Multas/Mora de prueba
    DELETE FROM dbo.Installment;

    -- Líneas de presupuesto
    DELETE FROM dbo.BudgetDetail;

    -- Lecturas de agua: detalle por unidad y luego la cabecera
    DELETE FROM dbo.ServiceReadingDetail;
    DELETE FROM dbo.ServiceReading;

    -- Cabeceras de presupuesto (Ordinario + Extraordinario + Cargos) — después de sus hijos
    DELETE FROM dbo.BudgetHeader;

    -- Periodos — después de BudgetHeader, que los referencia (IdPeriod)
    DELETE FROM dbo.Period;

    -- Verificación — deben dar 0 todas:
    SELECT COUNT(*) AS InstallmentPaidRestantes       FROM dbo.InstallmentPaid;
    SELECT COUNT(*) AS InstallmentExonerationRestantes FROM dbo.InstallmentExoneration;
    SELECT COUNT(*) AS InstallmentRestantes            FROM dbo.Installment;
    SELECT COUNT(*) AS BudgetDetailRestantes           FROM dbo.BudgetDetail;
    SELECT COUNT(*) AS ServiceReadingDetailRestantes   FROM dbo.ServiceReadingDetail;
    SELECT COUNT(*) AS ServiceReadingRestantes         FROM dbo.ServiceReading;
    SELECT COUNT(*) AS BudgetHeaderRestantes           FROM dbo.BudgetHeader;
    SELECT COUNT(*) AS PeriodRestantes                 FROM dbo.Period;

-- Si todos los conteos de arriba dieron 0 y no hubo errores:
-- COMMIT;

-- Si algo se ve mal:
-- ROLLBACK;


-- =====================================================================================
-- OPCIONAL — no se ejecuta salvo que lo destapes a propósito.
-- =====================================================================================
-- Los pagos conciliados (InstallmentPaid) ya se borraron arriba, pero la transacción
-- bancaria (TransactionBankDetail / estado de cuenta importado) en sí NO se toca — no
-- es dato de Budget/Periodo/Agua. Eso significa que una transacción que estaba
-- conciliada contra una cuota ya borrada puede quedar marcada como Conciliada/Parcial
-- con un IdGroupUnit asignado, aunque el pago que la respaldaba ya no exista. Si además
-- quieres dejar el estado de cuenta como "recién importado, sin conciliar" para volver
-- a probar la conciliación desde cero, descomenta y corre esto (nombre de columnas sin
-- verificar contra la base real — ajusta si no coincide):
--
-- UPDATE dbo.TransactionBankDetail
-- SET ReconciliationStatus = 0, ReconciliationDate = NULL, IdGroupUnit = NULL, Balance = 0;
