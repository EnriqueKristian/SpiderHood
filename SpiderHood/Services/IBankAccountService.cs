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
        Task<List<BankAccount>> ObtenerCuentasBancariasAsync(Guid IdBulding);
        Task<List<TransactionBankDetail>> ObtenerTransaccionesAsync(Guid cuentaId, DateTime desde, DateTime hasta);
        Task ConciliarTransaccionAsync(TransactionBankDetail transaccion, ViewExpense gasto);
        Task DesconciliarTransaccionAsync(TransactionBankDetail transaccion);
        Task MarcarTransaccionComoIgnoradaAsync(TransactionBankDetail transaccion);
        Task<Conciliacion?> ObtenerUltimaConciliacionAsync();
        Task GuardarConciliacionAsync(Conciliacion conciliacion);
        Task<List<TransactionBankDetail>> ProcesarArchivoEstadoCuentaAsync(IBrowserFile archivo, string formato);
        Task InstallmentConciliationAsync(TransactionBankDetail transaccion, Installment cuota);
        Task<List<TransactionBankHeader>> GetTransactionsByFileNameAsync(string filename, Guid IdBankAccount);
        Task<List<TransactionBankHeader>> GetMovementHeadersAsync(Guid idBuilding, Guid? idBankAccount);
        Task<List<MovDetKey>> GetTransactionsDetailsAsync(Guid IdBankAccount, DateTime minValue, DateTime maxValue);
        Task AddTransactionFromEECCAsync(TransactionBankDetail newtransaction);
        Task AddTransactionBankHeaderAsync(TransactionBankHeader newtransaction);
        Task CrearTransaccionSobranteAsync(TransactionBankDetail paidexcesc);
    }

    public class BankAccountService : IBankAccountService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        public BDLayout ec = default!;

        public BankAccountService(IDbContextFactory<SpiderHoodContext> contextFactory, HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
            ec = new BDLayout(contextFactory);
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
                await ec.AddNewRecordAsync(newbankaccount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la cuenta bancaria: {ex.Message}");
            }

        }

        public async Task UpdateBankAccount(BankAccount bankaccount)
        {
            try
            {
                await ec.UpdateRecordAsync(bankaccount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la cuenta bancaria: {ex.Message}");
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
                // Generar transacciones de ejemplo
                return await ec.GetBankTransactionsNoConciliedAsync(cuentaId, desde, hasta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener transacciones: {ex.Message}");
                return new List<TransactionBankDetail>();
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
            }

        }

        public async Task DesconciliarTransaccionAsync(TransactionBankDetail transaccion)
        {
            await Task.Delay(200);
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} desconciliada");
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