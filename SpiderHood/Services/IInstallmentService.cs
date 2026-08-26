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

        // Aplica un pago (transacción bancaria) contra una o varias cuotas seleccionadas.
        // Reemplaza a ConciliarConCuota/ConciliarTotalmente/ConciliarParcialmente/ConciliarConSobrante
        // como el único camino para conciliar Ingresos: cubre pago menor, igual y mayor (incluso
        // cubriendo varias cuotas) con la misma lógica, sin duplicarla en cada página que la usa.
        Task AplicarPagoAsync(TransactionBankDetail pago, List<Installment> cuotasSeleccionadas, Services.IBankAccountService BankService, bool automatico = false);

        // Deshace AplicarPagoAsync: borra los InstallmentPaid ligados a este pago y devuelve
        // tanto el pago como las cuotas afectadas a NoConciliada.
        Task RevertirPagoAsync(TransactionBankDetail pago, List<InstallmentPaid> pagosDeEstaTransaccion, Services.IBankAccountService BankService);
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
                throw;
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

        public async Task AplicarPagoAsync(TransactionBankDetail pago, List<Installment> cuotasSeleccionadas, Services.IBankAccountService BankService, bool automatico = false)
        {
            if (pago == null) throw new ArgumentNullException(nameof(pago));
            if (cuotasSeleccionadas == null || !cuotasSeleccionadas.Any())
                throw new ArgumentException("Debe seleccionar al menos una cuota para conciliar el pago.");

            // Regla: un pago solo puede asociarse a un propietario o Grupo de Unidad.
            var idGroupUnit = cuotasSeleccionadas.First().IdGroupUnit;
            if (cuotasSeleccionadas.Any(c => c.IdGroupUnit != idGroupUnit))
                throw new InvalidOperationException("Todas las cuotas seleccionadas deben pertenecer al mismo propietario o grupo de unidad.");
            if (pago.IdGroupUnit != Guid.Empty && pago.IdGroupUnit != idGroupUnit)
                throw new InvalidOperationException("Este pago ya fue asociado a otro propietario o grupo de unidad.");

            // Saldo disponible del pago: si ya se le aplicó algo antes, Balance ya refleja lo
            // restante; si es la primera aplicación, Balance todavía no se ha inicializado.
            var saldoRestante = pago.Balance != 0 ? Math.Abs(pago.Balance) : Math.Abs(pago.Amount);

            foreach (var cuota in cuotasSeleccionadas.OrderBy(c => c.DueDate).ThenBy(c => c.Number))
            {
                if (saldoRestante <= 0) break;
                if (cuota.Debt <= 0) continue;

                var montoAplicado = Math.Min(cuota.Debt, saldoRestante);
                var esPagoParcialDeCuota = montoAplicado < cuota.Debt;

                var pagoCuota = new InstallmentPaid
                {
                    IdPaid = Guid.NewGuid(),
                    IdInstallment = cuota.IdInstallment,
                    Amount = montoAplicado,
                    PaymentDate = DateTime.Now,
                    IdTransaction = pago.IdStatementDetail,
                    IsAutoReconcile = automatico,
                    IsPartialPayment = esPagoParcialDeCuota,
                    Status = ConcilationType.Conciliada
                };

                // Cuota: Conciliada si el pago cubrió toda su deuda, Parcial si quedó un resto.
                cuota.Status = esPagoParcialDeCuota ? ConcilationType.Parcial : ConcilationType.Conciliada;
                cuota.Debt -= montoAplicado;
                cuota.AmountPaid += montoAplicado;
                cuota.Reconciled = !esPagoParcialDeCuota;
                cuota.ReconciledTransactionId = pago.IdStatementDetail;
                cuota.AutoReconcile = automatico;
                if (esPagoParcialDeCuota) cuota.LastPartialPaymentDate = DateTime.Now;

                saldoRestante -= montoAplicado;

                // El pago queda ligado a este propietario/grupo de unidad; mientras tenga saldo
                // disponible puede seguir cubriendo más cuotas del mismo grupo más adelante.
                // Esto se actualiza ANTES de persistir: InstallmentConciliationAsync escribe
                // pago.ReconciliationStatus tal cual está en este momento, así que si se deja
                // el valor viejo (NoConciliada) hasta después del loop, la BD nunca se entera
                // de que el pago quedó Conciliada/Parcial y una recarga lo muestra otra vez
                // como Pendiente aunque en memoria ya se viera bien.
                pago.IdGroupUnit = idGroupUnit;
                pago.Balance = saldoRestante;
                pago.ReconciliationStatus = saldoRestante <= 0 ? ConcilationType.Conciliada : ConcilationType.Parcial;
                pago.ReconciliationDate = DateTime.Now;

                await AgregarPagoAsync(pagoCuota);
                await BankService.InstallmentConciliationAsync(pago, cuota);
            }
        }

        public async Task RevertirPagoAsync(TransactionBankDetail pago, List<InstallmentPaid> pagosDeEstaTransaccion, Services.IBankAccountService BankService)
        {
            if (pago == null) throw new ArgumentNullException(nameof(pago));
            if (pagosDeEstaTransaccion == null || !pagosDeEstaTransaccion.Any()) return;

            await ec.DeleteInstallmentPaidByTransactionAsync(pago.IdStatementDetail);

            pago.ReconciliationStatus = ConcilationType.NoConciliada;
            pago.ReconciliationDate = null;
            pago.Balance = 0;
            pago.IdGroupUnit = Guid.Empty;

            foreach (var idInstallment in pagosDeEstaTransaccion.Select(p => p.IdInstallment).Distinct())
            {
                var cuotaLiberada = new Installment { IdInstallment = idInstallment, Status = ConcilationType.NoConciliada };
                await BankService.InstallmentConciliationAsync(pago, cuotaLiberada);
            }
        }
    }
}