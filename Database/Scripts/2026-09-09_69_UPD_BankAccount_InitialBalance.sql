-- Crea el stored procedure UPD_BankAccount_InitialBalance -- excepción deliberada a
-- que InitialBalance sea fijo desde la creación (UPD_BankAccount lo excluye a propósito,
-- ver Database/Scripts/2026-09-05_51_BankAccount_InitialBalance.sql). El usuario pidió
-- una forma de corregirlo: en la pantalla de Conciliación (ReconciliationWorkspace.razor),
-- marcar UN movimiento como "este es el saldo inicial" y que eso alimente InitialBalance
-- directamente (Docs/Pendientes-Negocio-Migracion.md #3).
--
-- Separado de UPD_BankAccount a propósito: ese proc sigue sin tocar InitialBalance desde
-- el formulario de edición de la cuenta (BuildingPage.razor) -- éste es la única vía,
-- y sólo se invoca desde la acción explícita "Marcar como Saldo Inicial".
--
-- Idempotente: CREATE OR ALTER no falla si el proc ya existe.
--
-- Ejecutar contra la base de datos de la app (ver DEPLOY-Production.md §2).

CREATE OR ALTER PROCEDURE dbo.UPD_BankAccount_InitialBalance
    @IdBankAccount   UNIQUEIDENTIFIER,
    @InitialBalance  DECIMAL(18, 2)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.BankAccount
    SET InitialBalance = @InitialBalance
    WHERE IdBankAccount = @IdBankAccount;
END
GO
