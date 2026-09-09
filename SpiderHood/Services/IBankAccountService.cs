using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IBankAccountService
    {
        Task AddBankAccount(BankAccount newbank);
        Task UpdateBankAccount(BankAccount bankaccount);
        Task SetInitialBalanceAsync(Guid idBankAccount, decimal initialBalance);
        Task<List<BankAccount>> ObtenerCuentasBancariasAsync(Guid IdBulding);
        Task<List<TransactionBankDetail>> ObtenerTransaccionesAsync(Guid cuentaId, DateTime desde, DateTime hasta);
        Task ConciliarTransaccionAsync(TransactionBankDetail transaccion, ViewExpense gasto);
        Task DesconciliarTransaccionAsync(TransactionBankDetail transaccion);
        // "Corregir" (Fase B) para Gastos -- a diferencia de DesconciliarTransaccionAsync
        // (nunca tocó la BD, ver comentario en su implementación), esta sí revierte de
        // verdad vía UPD_ExpenseDeReconcilied.
        Task DesconciliarGastoRealAsync(Guid idStatementDetail, Guid idExpense);
        Task MarcarTransaccionComoIgnoradaAsync(TransactionBankDetail transaccion);
        Task<Conciliacion?> ObtenerUltimaConciliacionAsync();
        Task GuardarConciliacionAsync(Conciliacion conciliacion);
        Task<List<TransactionBankDetail>> ProcesarArchivoEstadoCuentaAsync(IBrowserFile archivo, string formato);
        Task InstallmentConciliationAsync(TransactionBankDetail transaccion, Installment cuota);
        Task<List<TransactionBankHeader>> GetTransactionsByFileNameAsync(string filename, Guid IdBankAccount);
        Task<List<TransactionBankHeader>> GetMovementHeadersAsync(Guid idBuilding, Guid? idBankAccount);
        Task<List<AccountStatementDetailView>> GetStatementDetailsAsync(Guid idStatementHeader);
        Task<List<AccountStatementDetailView>> GetStatementDetailsByBuildingAsync(Guid idBuilding, Guid? idBankAccount);
        Task<List<MovDetKey>> GetTransactionsDetailsAsync(Guid IdBankAccount, DateTime minValue, DateTime maxValue);
        Task AddTransactionFromEECCAsync(TransactionBankDetail newtransaction);
        Task AddTransactionBankHeaderAsync(TransactionBankHeader newtransaction);
        Task CrearTransaccionSobranteAsync(TransactionBankDetail paidexcesc);
    }

    public class BankAccountService : IBankAccountService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        private readonly AuthService _authService;
        public BDLayout ec = default!;

        public BankAccountService(IDbContextFactory<SpiderHoodContext> contextFactory, HttpClient httpClient, ILocalStorageService localStorage, AuthService authService)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            ec = new BDLayout(contextFactory);
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
        }


        public async Task AddTransactionFromEECCAsync(TransactionBankDetail newtransaction)
        {
            try
            {
                await ec.AddNewRecordAsync(newtransaction);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la cuenta bancaria: {ex.Message} | Causa real: {ex.GetBaseException().Message}");
                throw;
            }
        }

        public async Task<List<MovDetKey>> GetTransactionsDetailsAsync(Guid IdBankAccount, DateTime minValue, DateTime maxValue)
        {
            try
            {
                return await ec.GetAllMovementDetailAsync(IdBankAccount, minValue, maxValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la obtener deatlle transacciones por cuenta : {ex.Message}");
                return [];
            }

        }

        public async Task<List<TransactionBankHeader>> GetTransactionsByFileNameAsync(string filename, Guid IdBankAccount)
        {
            try
            {
                return await ec.GetMovementByFileNameAsync(filename, IdBankAccount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener lista de cargas de transacciones : {ex.Message}");
                return [];
            }

        }

        public async Task<List<TransactionBankHeader>> GetMovementHeadersAsync(Guid idBuilding, Guid? idBankAccount)
        {
            try
            {
                return await ec.GetMovementHeadersAsync(idBuilding, idBankAccount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener los estados de cuenta cargados : {ex.Message}");
                return [];
            }
        }

        public async Task<List<AccountStatementDetailView>> GetStatementDetailsAsync(Guid idStatementHeader)
        {
            try
            {
                return await ec.GetAccountStatementDetailByHeaderAsync(idStatementHeader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el detalle del estado de cuenta : {ex.Message}");
                return [];
            }
        }

        // No existe un stored procedure que traiga el detalle ya filtrado por edificio/cuenta,
        // así que se agregan las cabeceras filtradas (mismo filtro que la pestaña "Cargas")
        // trayendo el detalle de cada una.
        public async Task<List<AccountStatementDetailView>> GetStatementDetailsByBuildingAsync(Guid idBuilding, Guid? idBankAccount)
        {
            try
            {
                var headers = await ec.GetMovementHeadersAsync(idBuilding, idBankAccount);
                var detallesPorHeader = await Task.WhenAll(
                    headers.Select(h => ec.GetAccountStatementDetailByHeaderAsync(h.IdStatementHeader)));

                return detallesPorHeader.SelectMany(d => d).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener el detalle de estados de cuenta del edificio : {ex.Message}");
                return [];
            }
        }

        public async Task AddTransactionBankHeaderAsync(TransactionBankHeader newtransaction)
        {
            try
            {
                await ec.AddNewRecordAsync(newtransaction);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la cabecera de la transaccion : {ex.Message} | Causa real: {ex.GetBaseException().Message}");
                throw;
            }

        }


        public async Task AddBankAccount(BankAccount newbankaccount)
        {
            try
            {
                // Sin este Trim, un número de cuenta cargado con un espacio de más al
                // inicio/fin desde la UI (BuildingPage.razor) se guardaba tal cual, y
                // cualquier comparación exacta después (p.ej. el importador de Estado de
                // Cuenta) fallaba con "la cuenta no existe" aunque fuera visualmente la
                // misma cuenta.
                newbankaccount.AccountNumber = newbankaccount.AccountNumber?.Trim() ?? "";
                newbankaccount.CCI = newbankaccount.CCI?.Trim() ?? "";
                await ec.AddNewRecordAsync(newbankaccount);
                await ec.StampAuditAsync(AuditableEntity.BankAccount, newbankaccount.IdBankAccount, await GetPerformedByAsync(), isCreate: true);
            }
            catch (Exception ex)
            {
                // Antes se atrapaba y sólo se logueaba acá -- el caller (BuildingPage.razor.cs,
                // SaveSection) también atrapaba todo en silencio, así que un error real (p.ej.
                // truncamiento de CCI, ver Database/Scripts/2026-09-05_50_BankAccount_Fixes.sql)
                // no se le mostraba nunca al usuario: "guardar" simplemente no hacía nada.
                Console.WriteLine($"Error al crear la cuenta bancaria: {ex.Message}");
                throw;
            }

        }

        public async Task UpdateBankAccount(BankAccount bankaccount)
        {
            try
            {
                bankaccount.AccountNumber = bankaccount.AccountNumber?.Trim() ?? "";
                bankaccount.CCI = bankaccount.CCI?.Trim() ?? "";
                await ec.UpdateRecordAsync(bankaccount);
                await ec.StampAuditAsync(AuditableEntity.BankAccount, bankaccount.IdBankAccount, await GetPerformedByAsync(), isCreate: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar la cuenta bancaria: {ex.Message}");
                throw;
            }

        }

        // Ver Docs/Pendientes-Negocio-Migracion.md #3 y BDLayout.SetBankAccountInitialBalanceAsync
        // -- única vía deliberada para tocar InitialBalance después de creada la cuenta,
        // desde la acción "Marcar como Saldo Inicial" en Conciliación.
        public async Task SetInitialBalanceAsync(Guid idBankAccount, decimal initialBalance)
        {
            try
            {
                await ec.SetBankAccountInitialBalanceAsync(idBankAccount, initialBalance);
                await ec.StampAuditAsync(AuditableEntity.BankAccount, idBankAccount, await GetPerformedByAsync(), isCreate: false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar el Saldo Inicial de la cuenta bancaria: {ex.Message}");
                throw;
            }
        }

        public async Task<List<BankAccount>> ObtenerCuentasBancariasAsync(Guid IdBulding)
        {
            try
            {
                // En una app real, esto vendría de una API
                return await ec.GetBankAccountsByBuildingAsync(IdBulding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener cuentas: {ex.Message}");
                return new List<BankAccount>();
            }
        }

        public async Task<List<TransactionBankDetail>> ObtenerTransaccionesAsync(Guid cuentaId, DateTime desde, DateTime hasta)
        {
            try
            {
                return await ec.GetBankTransactionsNoConciliedAsync(cuentaId, desde, hasta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener transacciones: {ex.Message}");
                throw;
            }
        }

        public async Task ConciliarTransaccionAsync(TransactionBankDetail transaccion, ViewExpense gasto)
        {
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} conciliada con gasto {gasto.IdExpense}");
            await ec.UpdateRecordAsync(transaccion);
        }

        public async Task InstallmentConciliationAsync(TransactionBankDetail transaccion, Installment cuota)
        {
            try
            {
                Console.WriteLine($"Transacción {transaccion.IdStatementDetail} conciliada con cuota {cuota.IdInstallment}");
                await ec.ConciliarInstallmentAsync(cuota, transaccion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al Conciliar Transaccion con Cuota: {ex.Message}");
                throw;
            }

        }

        public async Task DesconciliarTransaccionAsync(TransactionBankDetail transaccion)
        {
            await Task.Delay(200);
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} desconciliada");
        }

        public async Task DesconciliarGastoRealAsync(Guid idStatementDetail, Guid idExpense)
        {
            await ec.DesconciliarGastoAsync(idStatementDetail, idExpense);
        }

        public async Task MarcarTransaccionComoIgnoradaAsync(TransactionBankDetail transaccion)
        {
            await Task.Delay(200);
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} marcada como ignorada");
        }

        public async Task<Conciliacion?> ObtenerUltimaConciliacionAsync()
        {
            await Task.Delay(200);

            return new Conciliacion
            {
                Id = 1,
                CuentaBancariaId = new Guid("0201291D-D015-4D6E-9C71-635F76E04728"),
                FechaInicio = DateTime.Now.AddMonths(-1),
                FechaFin = DateTime.Now,
                TransaccionesProcesadas = 45,
                TransaccionesConciliadas = 42,
                Diferencia = 125.50m,
                Completada = true,
                Fecha = DateTime.Now.AddDays(-3),
                Usuario = "Admin Principal"
            };
        }

        public async Task GuardarConciliacionAsync(Conciliacion conciliacion)
        {
            await Task.Delay(300);
            Console.WriteLine($"Conciliación guardada: {conciliacion.Id}");
        }

        public async Task<List<TransactionBankDetail>> ProcesarArchivoEstadoCuentaAsync(IBrowserFile archivo, string formato)
        {
            // En una implementación real, esto procesaría el archivo
            await Task.Delay(1000);

            // Retornar transacciones de ejemplo
            return new List<TransactionBankDetail>();
        }

        public async Task CrearTransaccionSobranteAsync(TransactionBankDetail paidexcesc)
        {
            try
            {
                await ec.AddNewRecordAsync(paidexcesc);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener transacciones: {ex.Message}");
            }

        }
    }
}