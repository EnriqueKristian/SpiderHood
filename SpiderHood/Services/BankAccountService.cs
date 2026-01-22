using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Forms;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Net.Http.Json;

namespace SpiderHood.Services
{
    public interface IBankAccountService
    {
        Task<List<BankAccount>> ObtenerCuentasBancariasAsync(BDLayout ec ,Guid IdBulding);
        Task<List<TransactionBankView>> ObtenerTransaccionesAsync(BDLayout ec, Guid cuentaId, DateTime desde, DateTime hasta);
        Task ConciliarTransaccionAsync(BDLayout ec, TransactionBankView transaccion, ViewExpense gasto);
        Task DesconciliarTransaccionAsync(BDLayout ec, TransactionBankView transaccion);
        Task MarcarTransaccionComoIgnoradaAsync(TransactionBankView transaccion);
        Task<Conciliacion?> ObtenerUltimaConciliacionAsync();
        Task GuardarConciliacionAsync(Conciliacion conciliacion);
        Task<List<TransactionBankView>> ProcesarArchivoEstadoCuentaAsync(IBrowserFile archivo, string formato);
    }

    public class BankAccountService : IBankAccountService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalStorageService _localStorage;
        //public BDLayout ec = default!;

        public BankAccountService(HttpClient httpClient, ILocalStorageService localStorage)
        {
            _httpClient = httpClient;
            _localStorage = localStorage;
        }

        public async Task<List<BankAccount>> ObtenerCuentasBancariasAsync(BDLayout ec , Guid IdBulding)
        {
            try
            {
                // En una app real, esto vendría de una API
                return await ec.GetBankAccountsByBuilding(IdBulding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener cuentas: {ex.Message}");
                return new List<BankAccount>();
            }
        }

        public async Task<List<TransactionBankView>> ObtenerTransaccionesAsync(BDLayout ec, Guid cuentaId, DateTime desde, DateTime hasta)
        {
            try
            {
                // Generar transacciones de ejemplo
                return ec.GetBankTransactionsNoConcilied(cuentaId, desde, hasta);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener transacciones: {ex.Message}");
                return new List<TransactionBankView>();
            }
        }

        public async Task ConciliarTransaccionAsync(BDLayout ec, TransactionBankView transaccion, ViewExpense gasto)
        {
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} conciliada con gasto {gasto.IdExpense}");
            await ec.UpdateRecordAsync(transaccion);
        }

        public async Task DesconciliarTransaccionAsync(BDLayout ec, TransactionBankView transaccion)
        {
            await Task.Delay(200);
            Console.WriteLine($"Transacción {transaccion.IdStatementDetail} desconciliada");
        }

        public async Task MarcarTransaccionComoIgnoradaAsync(TransactionBankView transaccion)
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

        public async Task<List<TransactionBankView>> ProcesarArchivoEstadoCuentaAsync(IBrowserFile archivo, string formato)
        {
            // En una implementación real, esto procesaría el archivo
            await Task.Delay(1000);

            // Retornar transacciones de ejemplo
            return new List<TransactionBankView>();
        }
    }
}