-- =============================================================================
-- SOLO LECTURA -- no modifica nada. Corre esto y pega el resultado para saber
-- qué scripts de Database/Scripts ya están aplicados en esta base y cuáles
-- todavía faltan correr.
--
-- Cubre las dos tandas de migraciones "sueltas" (no hay tabla de versionado
-- en este proyecto, cada script se corre a mano):
--   - 2026-09-08_55 a _67 (sesión "fixes al aplicativo")
--   - 2026-09-09_68 a _77 (sesión "lista de pendientes")
-- =============================================================================

SET NOCOUNT ON;

CREATE TABLE #Check (Script NVARCHAR(200), Aplicado BIT, Detalle NVARCHAR(200));

-- 55: BudgetHeader.CreatedBy ensanchado a NVARCHAR(256)
INSERT INTO #Check
SELECT '55_BudgetHeader_CreatedBy_Widen',
       CASE WHEN (SELECT max_length FROM sys.columns
                  WHERE object_id = OBJECT_ID('dbo.BudgetHeader') AND name = 'CreatedBy') >= 512 THEN 1 ELSE 0 END,
       'CreatedBy debe ser NVARCHAR(256) = 512 bytes';

-- 56: columna OriginalReference + 2 SPs
INSERT INTO #Check
SELECT '56_TransactionBankDetail_OriginalReference',
       CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'OriginalReference')
             AND OBJECT_ID('dbo.UPD_TransactionBankDetail_OriginalReference') IS NOT NULL
             AND OBJECT_ID('dbo.GET_TransactionBankDetail_ByOriginalReference') IS NOT NULL
            THEN 1 ELSE 0 END, NULL;

-- 57: FKs reales Contact->BuildingConfiguration y Parameter->Building
INSERT INTO #Check
SELECT '57_Contact_Parameter_RealFK',
       CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Contact_BuildingConfiguration')
             AND EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Parameter_Building')
            THEN 1 ELSE 0 END, NULL;

-- 58: GET_AllContacts filtra por @IdRelatedEntity
INSERT INTO #Check
SELECT '58_Fix_GET_AllContacts_SinFiltro',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_AllContacts')) LIKE '%WHERE%IdRelatedEntity%' THEN 1 ELSE 0 END,
       'Debe tener WHERE IdRelatedEntity = @IdRelatedEntity';

-- 59: GET_BankTransactionsNoConcilied incluye OriginalReference en el SELECT
INSERT INTO #Check
SELECT '59_Fix_GET_BankTransactionsNoConcilied_OriginalReference',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_BankTransactionsNoConcilied')) LIKE '%OriginalReference%' THEN 1 ELSE 0 END, NULL;

-- 60/62/63: GET_InstallmentPaid acumulativo -- basta chequear la version final (63)
INSERT INTO #Check
SELECT '60_62_63_Fix_GET_InstallmentPaid (OwnerUnit+AmountDebt+Period)',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_InstallmentPaid')) LIKE '%InstallmentDebt%'
             AND OBJECT_DEFINITION(OBJECT_ID('dbo.GET_InstallmentPaid')) LIKE '%AS Period%'
            THEN 1 ELSE 0 END,
       'Si da 0 pero el proc existe, puede tener sólo 60 o 60+62 aplicado, no 63';

-- 61: GET_InstallmentsByBuilding existe
INSERT INTO #Check
SELECT '61_GET_InstallmentsByBuilding',
       CASE WHEN OBJECT_ID('dbo.GET_InstallmentsByBuilding') IS NOT NULL THEN 1 ELSE 0 END, NULL;

-- 64: INS_AccountStatementDetail calcula SequenceNumber (no lo recibe fijo en 0)
INSERT INTO #Check
SELECT '64_Fix_INS_AccountStatementDetail_SequenceNumber',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.INS_AccountStatementDetail')) LIKE '%MAX(SequenceNumber)%'
             OR OBJECT_DEFINITION(OBJECT_ID('dbo.INS_AccountStatementDetail')) LIKE '%ISNULL(MAX%'
            THEN 1 ELSE 0 END,
       'Revisar a mano si da 0 -- el patrón exacto puede variar';

-- 65: backfill -- ya no deben quedar filas en SequenceNumber = 0
INSERT INTO #Check
SELECT '65_Backfill_SequenceNumber_Historico',
       CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.AccountStatementDetail WHERE SequenceNumber = 0) THEN 1 ELSE 0 END,
       'Si da 0, puede ser que nunca hubo filas en 0 (no concluyente) o que falta correr el backfill';

-- 66/67: UPD_ExpenseReconcilied / UPD_ExpenseDeReconcilied ya no pisan Expense.Status con ReconciliationStatus
INSERT INTO #Check
SELECT '66_Fix_UPD_ExpenseReconcilied_Status',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseReconcilied')) LIKE '%SET%Status%=%ReconciliationStatus%'
            THEN 0 ELSE 1 END,
       'Si da 0, el SP todavía pisa Expense.Status con @ReconciliationStatus';
INSERT INTO #Check
SELECT '67_Fix_UPD_ExpenseDeReconcilied_Status',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.UPD_ExpenseDeReconcilied')) LIKE '%SET%Status%=%ReconciliationStatus%'
            THEN 0 ELSE 1 END,
       'Si da 0, el SP todavía pisa Expense.Status con @ReconciliationStatus';

-- 68: SP DEL_Building
INSERT INTO #Check
SELECT '68_DEL_Building_Procedure', CASE WHEN OBJECT_ID('dbo.DEL_Building') IS NOT NULL THEN 1 ELSE 0 END, NULL;

-- 69: SP UPD_BankAccount_InitialBalance
INSERT INTO #Check
SELECT '69_UPD_BankAccount_InitialBalance', CASE WHEN OBJECT_ID('dbo.UPD_BankAccount_InitialBalance') IS NOT NULL THEN 1 ELSE 0 END, NULL;

-- 70: columnas Ignored/IgnoredReason/IgnoredType + SP UPD_AccountStatementDetail_Ignored
INSERT INTO #Check
SELECT '70_AccountStatementDetail_Ignored',
       CASE WHEN EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'Ignored')
             AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'IgnoredReason')
             AND EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AccountStatementDetail') AND name = 'IgnoredType')
             AND OBJECT_ID('dbo.UPD_AccountStatementDetail_Ignored') IS NOT NULL
            THEN 1 ELSE 0 END, NULL;

-- 71: tabla ReconciliationSession + SPs
INSERT INTO #Check
SELECT '71_ReconciliationSession',
       CASE WHEN OBJECT_ID('dbo.ReconciliationSession') IS NOT NULL
             AND OBJECT_ID('dbo.INS_ReconciliationSession') IS NOT NULL
             AND OBJECT_ID('dbo.GET_LastReconciliationSession') IS NOT NULL
            THEN 1 ELSE 0 END, NULL;

-- 72: fix de nombre de columna en GET_LastReconciliationSession (alias CuentaBancariaId)
INSERT INTO #Check
SELECT '72_Fix_GET_LastReconciliationSession_ColumnName',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_LastReconciliationSession')) LIKE '%AS CuentaBancariaId%' THEN 1 ELSE 0 END, NULL;

-- 73: GET_ServiceReadingDetailList sin duplicados (colapsa GroupUnit por MIN)
INSERT INTO #Check
SELECT '73_Fix_GET_ServiceReadingDetailList_Duplicates',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_ServiceReadingDetailList')) LIKE '%MIN(GroupNumber)%' THEN 1 ELSE 0 END, NULL;

-- 74: GET_ServiceReadingDetailList filtra por @IdBuilding
INSERT INTO #Check
SELECT '74_GET_ServiceReadingDetailList_FiltraPorEdificio',
       CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.GET_ServiceReadingDetailList')) LIKE '%@IdBuilding%' THEN 1 ELSE 0 END, NULL;

-- 75: tabla ExpenseTemplate + SPs
INSERT INTO #Check
SELECT '75_ExpenseTemplate',
       CASE WHEN OBJECT_ID('dbo.ExpenseTemplate') IS NOT NULL
             AND OBJECT_ID('dbo.INS_ExpenseTemplate') IS NOT NULL
             AND OBJECT_ID('dbo.UPD_ExpenseTemplate') IS NOT NULL
             AND OBJECT_ID('dbo.GET_ExpenseTemplatesByBuilding') IS NOT NULL
            THEN 1 ELSE 0 END, NULL;

-- 76: 4 permisos sembrados
INSERT INTO #Check
SELECT '76_Seed_ReportPermissions',
       CASE WHEN (SELECT COUNT(*) FROM dbo.Permissions
                  WHERE PermissionKey IN ('view_budget_execution','view_delinquency','view_consumption_report','view_income_expense_report')) = 4
            THEN 1 ELSE 0 END,
       'Cuenta cuántos de los 4 existen -- ver detalle abajo';

-- 77: SPs INS_Permission / UPD_Permission
INSERT INTO #Check
SELECT '77_Permission_CRUD',
       CASE WHEN OBJECT_ID('dbo.INS_Permission') IS NOT NULL AND OBJECT_ID('dbo.UPD_Permission') IS NOT NULL THEN 1 ELSE 0 END, NULL;

SELECT Script,
       CASE Aplicado WHEN 1 THEN 'YA APLICADO' ELSE 'FALTA CORRER' END AS Estado,
       Detalle
FROM #Check
ORDER BY Script;

-- Detalle de los 4 permisos del punto 76, uno por uno
SELECT PermissionKey, 'existe' AS Estado FROM dbo.Permissions
WHERE PermissionKey IN ('view_budget_execution','view_delinquency','view_consumption_report','view_income_expense_report');

DROP TABLE #Check;
