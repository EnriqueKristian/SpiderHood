using MercadoPago.Client.Preapproval;

namespace SpiderHood.Services
{
    // Cobro de la Suscripción SaaS del Administrador vía MercadoPago
    // (Preapproval = pago recurrente), modo test -- ver
    // Docs/Design-Subscripcion-Administrador.md. No hay Stripe disponible para
    // Perú, de ahí el cambio. La activación real del plan NO pasa por acá: pasa
    // por el webhook (Program.cs, POST /api/mercadopago/webhook) cuando
    // MercadoPago confirma que la suscripción quedó autorizada.
    public interface IPaymentService
    {
        // Arma una Preapproval (suscripción recurrente mensual) con el
        // monto/moneda ya cargados en SubscriptionPlan.Amount/CurrencyId -- no
        // hace falta un "Plan" pre-creado en el Dashboard de MercadoPago, a
        // diferencia de Stripe. Devuelve la URL (InitPoint) a la que hay que
        // redirigir el navegador. Tira InvalidOperationException si el plan no
        // existe o todavía no tiene un Amount configurado (runbook en el
        // documento de diseño).
        Task<string> CreateCheckoutSessionAsync(Guid idUser, string userEmail, int idSubscriptionPlan, string domain);
    }

    public class PaymentService : IPaymentService
    {
        private readonly ISubscriptionService _subscriptionService;

        public PaymentService(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        public async Task<string> CreateCheckoutSessionAsync(Guid idUser, string userEmail, int idSubscriptionPlan, string domain)
        {
            var plans = await _subscriptionService.GetAllPlansAsync();
            var plan = plans.FirstOrDefault(p => p.IdSubscriptionPlan == idSubscriptionPlan)
                ?? throw new InvalidOperationException("El plan solicitado no existe.");

            if (plan.Amount is not { } amount || string.IsNullOrWhiteSpace(plan.CurrencyId))
                throw new InvalidOperationException($"El plan '{plan.Name}' todavía no tiene un precio configurado.");

            var request = new PreapprovalCreateRequest
            {
                Reason = $"Suscripción SpiderHood - {plan.Name}",
                PayerEmail = userEmail,
                BackUrl = $"{domain}/pago-exitoso",
                // El webhook (subscription_preapproval) parsea esto para saber a
                // qué usuario/plan activar -- ver Program.cs.
                ExternalReference = $"{idUser}:{plan.IdSubscriptionPlan}",
                AutoRecurring = new PreApprovalAutoRecurringCreateRequest
                {
                    Frequency = 1,
                    FrequencyType = "months",
                    TransactionAmount = amount,
                    CurrencyId = plan.CurrencyId,
                },
            };

            var client = new PreapprovalClient();
            var preapproval = await client.CreateAsync(request);
            return preapproval.InitPoint;
        }
    }
}
