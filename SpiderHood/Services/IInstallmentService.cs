using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IInstallmentService
    {
        //Installments
        Task<List<Models.Installment>> GetInstallmentsByBudgetAsync(Guid IdBudgetHeader);
        Task<List<Models.Installment>> GetPendingInstallmentsAsync(Guid IdBuilding);
        Task<Models.InstallmentPaid> AgregarPagoAsync(InstallmentPaid paid);
        Task<List<Models.InstallmentPaid>> GetInstallmentsPaidAsync(Guid IdBuilding);
        Task<int> BuscarCoincidencias(List<Installment> Installments, List<TransactionBankDetail> transacciones);
        Task ConciliarConCuota(List<Installment> filteredInstallments, List<TransactionBankDetail> transacciones, Services.IBankAccountService BankService, TransactionBankDetail transaccion, Installment cuota, bool automatico = false);
    }

    public class InstallmentService : IInstallmentService
    {
        public List<Installment> Installments { get; set; } = [];
        public Installment Installment { get; set; } = new();
        public List<InstallmentPaid> InstallmentPaids { get; set; } = [];
        public InstallmentPaid InstallmentPaid { get; set; } = new();

        private readonly ILogger<IBudgetService> _logger;
        private BDLayout ec { get; set; }

        public InstallmentService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<IBudgetService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Installment>> GetInstallmentsByBudgetAsync(Guid IdBudgetHeader)
        {
            try
            {
                return await ec.GetInstallmentsByBudgetAsync(IdBudgetHeader);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener los installments por budget: {ex.Message}");
                return new List<Installment>();
            }

        }

        public async Task<List<Installment>> GetPendingInstallmentsAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetPendingInstallmentsAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar Pago de Cuota: {ex.Message}");
                return new List<Installment>();
            }
        }

        public async Task<Models.InstallmentPaid> AgregarPagoAsync(InstallmentPaid paid)
        {
            try
            {
                return await ec.AddNewRecordAsync(paid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar Pago de Cuota: {ex.Message}");
                return new Models.InstallmentPaid();
            }
        }

        public async Task<List<InstallmentPaid>> GetInstallmentsPaidAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetInstallmentsPaidAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obtener los pagos de cuotas: {ex.Message}");
                return [];
            }
        }

        public async Task<int> BuscarCoincidencias(List<Installment> Installments, List<TransactionBankDetail> transacciones)
        {
            int totalCoincidencias = 0;

            // Validate collections first to avoid null reference issues
            if (Installments == null || transacciones == null)
            {
                // Log or handle null collections appropriately
                return 0;
            }

            // Filter once to avoid repeated Where calls
            var unreconciledInstallments = Installments.Where(t => !t.Reconciled).ToList();

            // Get all paid transaction IDs in one query for better performance
            var allPaidIds = unreconciledInstallments
                .SelectMany(i => i.Paids?.Select(p => p.IdTransaction) ?? Enumerable.Empty<Guid>())
                .Where(id => id != Guid.Empty) // Assuming IDs are positive
                .Distinct()
                .ToList();

            foreach (var installment in unreconciledInstallments)
            {
                // Clear previous matches to avoid duplicates
                installment.PosiblesMatches.Clear();
                installment.PreviousPaid.Clear();

                var montoDeuda = Math.Abs(installment.Debt);

                // 1. Add previously associated payments
                var paidIds = installment.Paids.Select(x => x.IdTransaction).ToList();
                var previosPaid = transacciones!
                    .Where(g => g.IdGroupUnit == installment.IdGroupUnit)
                    .Take(3)
                    .ToList();

                if (previosPaid.Any())
                {
                    installment.PreviousPaid = previosPaid;
                }

                // 2. Find payments with similar amount (±10%)
                // Using decimal for monetary calculations
                decimal tolerance = 0.1m;
                decimal lowerBound = montoDeuda * (1 - tolerance);
                decimal upperBound = montoDeuda * (1 + tolerance);

                var posiblesMatches = transacciones!
                    .Where(g => g.Balance >= lowerBound && g.Amount <= upperBound)
                    .Select(g => new
                    {
                        Transaction = g,
                        Difference = Math.Abs(g.Amount - montoDeuda)
                    })
                    .OrderBy(x => x.Difference)
                    .Take(3)
                    .Select(x => x.Transaction)
                    .ToList();

                if (posiblesMatches.Any())
                {
                    installment.PosiblesMatches = posiblesMatches;

                    // 3. Check for exact match (with tolerance for floating point)
                    var exactMatch = posiblesMatches
                        .FirstOrDefault(g => Math.Abs(g.Amount - montoDeuda) < 0.01m); // 0.01 tolerance for exact match

                    if (exactMatch != null)
                    {
                        // Uncomment if you want automatic reconciliation
                        // await ConciliarConPago(installment, exactMatch, automatico: true);
                    }
                }

                totalCoincidencias += posiblesMatches.Count;


            }
            return totalCoincidencias;
        }

        public async Task ConciliarConCuota(List<Installment> filteredInstallments, List<TransactionBankDetail> transacciones, Services.IBankAccountService BankService, TransactionBankDetail transaccion, Installment cuota, bool automatico = false)
        {
            try
            {
                // Validaciones iniciales
                if (transaccion == null) throw new ArgumentNullException(nameof(transaccion));
                if (cuota == null) throw new ArgumentNullException(nameof(cuota));

                // Calcular saldo pendiente de la cuota
                var saldoCuotaPendiente = cuota.Debt;
                var montoTransaccion = Math.Abs(transaccion.Balance);

                // Caso 1: Montos iguales - Conciliación total
                if (Math.Abs(montoTransaccion - saldoCuotaPendiente) < 0.01m)
                {
                    await ConciliarTotalmente(filteredInstallments!, transacciones!, BankService, transaccion, cuota, montoTransaccion, automatico);
                }
                // Caso 2: Transacción menor a cuota - Conciliación parcial
                else if (montoTransaccion < saldoCuotaPendiente)
                {
                    await ConciliarParcialmente(filteredInstallments!, transacciones!, BankService, transaccion, cuota, montoTransaccion, automatico);
                }
                // Caso 3: Transacción mayor a cuota - Conciliación con sobrante
                else
                {
                    await ConciliarConSobrante(filteredInstallments!, transacciones!, BankService, transaccion, cuota, saldoCuotaPendiente, automatico);
                }

            }
            catch (Exception ex)
            {
                //await ManejarErrorConciliacion(ex);
                throw new Exception(ex.Message);
            }
        }

        private async Task ConciliarTotalmente(List<Installment> filteredInstallments, List<TransactionBankDetail> transacciones, Services.IBankAccountService BankService, TransactionBankDetail transaccion, Installment cuota, decimal monto, bool automatico)
        {
            // Crear registro de pago
            var pago = new InstallmentPaid
            {
                IdInstallment = cuota.IdInstallment,
                Amount = monto,
                PaymentDate = DateTime.Now,
                IdTransaction = transaccion.IdStatementDetail,
                IsAutoReconcile = automatico
            };

            // Actualizar estado de la transacción
            transaccion.ReconciliationStatus = ConcilationType.Conciliada;
            transaccion.ReconciliationDate = DateTime.Now;

            // Actualizar estado de la cuota
            cuota.Reconciled = true;
            cuota.ReconciledTransactionId = transaccion.IdStatementDetail;
            cuota.AutoReconcile = automatico;
            cuota.Status = ConcilationType.Conciliada;

            // Guardar en base de datos
            await AgregarPagoAsync(pago);
            await BankService.InstallmentConciliationAsync(transaccion, cuota);

            var x = filteredInstallments!.Where(c => c.IdInstallment == cuota.IdInstallment).FirstOrDefault();
            x!.Status = cuota.Status;
            x!.Debt = cuota.Debt - monto;

            var y = transacciones!.Where(c => c.IdStatementDetail == transaccion.IdStatementDetail).FirstOrDefault();
            y!.Balance = y!.Amount - monto;
            y!.IdGroupUnit = cuota.IdGroupUnit;
            y!.ReconciliationStatus = transaccion.ReconciliationStatus;
        }

        private async Task ConciliarParcialmente(List<Installment> filteredInstallments, List<TransactionBankDetail> transacciones, Services.IBankAccountService BankService, TransactionBankDetail transaccion, Installment cuota, decimal monto, bool automatico)
        {
            // Crear registro de pago parcial
            var pagoParcial = new InstallmentPaid
            {
                IdPaid = Guid.NewGuid(),
                IdInstallment = cuota.IdInstallment,
                Amount = monto,
                PaymentDate = DateTime.Now,
                IdTransaction = transaccion.IdStatementDetail,
                IsAutoReconcile = automatico,
                IsPartialPayment = true,
                Status = ConcilationType.Conciliada
            };

            // Actualizar estado de la transacción (totalmente conciliada)
            transaccion.ReconciliationStatus = ConcilationType.Conciliada;
            transaccion.ReconciliationDate = DateTime.Now;

            // Cuota queda parcialmente pagada (no marcamos como Reconciled = true)
            cuota.LastPartialPaymentDate = DateTime.Now;
            cuota.AutoReconcile = automatico;
            cuota.Status = ConcilationType.Parcial;

            // Guardar en base de datos
            await AgregarPagoAsync(pagoParcial);
            await BankService.InstallmentConciliationAsync(transaccion, cuota);

            var x = filteredInstallments!.Where(c => c.IdInstallment == cuota.IdInstallment).FirstOrDefault();
            x!.Status = cuota.Status;
            x!.Debt = cuota.Debt - monto;

            var y = transacciones!.Where(c => c.IdStatementDetail == transaccion.IdStatementDetail).FirstOrDefault();
            y!.Balance = y!.Amount - monto;
            y!.IdGroupUnit = cuota.IdGroupUnit;
            y!.ReconciliationStatus = transaccion.ReconciliationStatus;
        }

        private async Task ConciliarConSobrante(List<Installment> filteredInstallments, List<TransactionBankDetail> transacciones, Services.IBankAccountService BankService, TransactionBankDetail transaccion, Installment cuota, decimal saldoCuota, bool automatico)
        {
            // 1. Conciliar el saldo pendiente de la cuota
            var pagoCompleto = new InstallmentPaid
            {
                IdPaid = Guid.NewGuid(),
                IdInstallment = cuota.IdInstallment,
                Amount = saldoCuota,
                PaymentDate = DateTime.Now,
                IdTransaction = transaccion.IdStatementDetail,
                IsAutoReconcile = automatico,
                Status = ConcilationType.Conciliada
            };

            // 2. Actualizar cuota como totalmente conciliada
            transaccion.ReconciliationStatus = ConcilationType.Parcial;
            transaccion.ReconciliationDate = DateTime.Now;

            cuota.Reconciled = true;
            cuota.ReconciledTransactionId = transaccion.IdStatementDetail;
            cuota.AutoReconcile = automatico;
            cuota.Status = ConcilationType.Conciliada;

            // 3. Guardar en base de datos
            await AgregarPagoAsync(pagoCompleto);
            await BankService.InstallmentConciliationAsync(transaccion, cuota);

            // 5. Agregar transacción sobrante a la lista pendiente
            var x = filteredInstallments!.Where(c => c.IdInstallment == cuota.IdInstallment).FirstOrDefault();
            x!.Status = cuota.Status;
            x!.Debt = cuota.Debt - saldoCuota;

            var y = transacciones!.Where(c => c.IdStatementDetail == transaccion.IdStatementDetail).FirstOrDefault();
            y!.Balance = y!.Amount - saldoCuota;
            y!.IdGroupUnit = cuota.IdGroupUnit;
            y!.ReconciliationStatus = transaccion.ReconciliationStatus;
        }
    }

}