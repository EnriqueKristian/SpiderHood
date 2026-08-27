-- GET_BankAccountsByBuilding lista columnas explícitas (no SELECT *), así que la
-- columna CCI agregada por 2026-08-27d_BankAccount_CCI.sql no aparecía en el resultado
-- y EF Core (FromSqlRaw<BankAccount>) truena con:
--   "The required column 'CCI' was not present in the results of a 'FromSql' operation."
--
-- Se agrega CCI al SELECT, en base al texto vigente de la procedure provisto por el usuario.
--
-- Ejecutar contra la base de datos de SpiderHood (SSMS / Azure Data Studio / sqlcmd).

ALTER PROCEDURE dbo.GET_BankAccountsByBuilding
@IdBuilding	UNIQUEIDENTIFIER
AS
BEGIN
	SELECT	IdBankAccount,
			AccountName,
			AccountNumber,
			BankName,
			AccountType,
			CurrentBalance,
			ReconciledBalance,
			LastReconciliation,
			IdBuilding,
			Status,
			CCI
	FROM	BankAccount b
    	WHERE	IdBuilding = @IdBuilding
END
GO
