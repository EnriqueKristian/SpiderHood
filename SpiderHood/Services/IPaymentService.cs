using Stripe.Checkout;

namespace SpiderHood.Services
{
    // Cobro de la Suscripción SaaS del Administrador vía Stripe Checkout, modo
    // test -- ver Docs/Design-Subscripcion-Administrador.md. La activación real
    // del plan NO pasa por acá: pasa por el webhook (Program.cs,
    // POST /api/stripe/webhook) cuando Stripe confirma que el pago se completó.
    public interface IPaymentService
    {
        // Arma una Checkout Session en modo "subscription" (cobro recurrente
        // mensual) para el Price ya cargado en SubscriptionPlan.StripePriceId, y
        // devuelve la URL a la que hay que redirigir el navegador. Tira
        // InvalidOperationException si el plan no existe o todavía no tiene un
        // Price de Stripe configurado (runbook en el documento de diseño).
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

            if (string.IsNullOrWhiteSpace(plan.StripePriceId))
                throw new InvalidOperationException($"El plan '{plan.Name}' todavía no tiene un Price de Stripe configurado.");

            // El webhook (checkout.session.completed) lee este metadata para saber
            // a qué usuario/plan activar -- se repite en SubscriptionData.Metadata
            // porque ese es el que persiste en las facturas de renovación
            // (invoice.*), útil el día que se maneje pago fallido/cancelación.
            var metadata = new Dictionary<string, string>
            {
                ["IdUser"] = idUser.ToString(),
                ["IdSubscriptionPlan"] = plan.IdSubscriptionPlan.ToString(),
            };

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                LineItems =
                [
                    new SessionLineItemOptions
                    {
                        Price = plan.StripePriceId,
                        Quantity = 1,
                    },
                ],
                CustomerEmail = userEmail,
                SuccessUrl = $"{domain}/pago-exitoso",
                CancelUrl = $"{domain}/pago-cancelado",
                Metadata = metadata,
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = metadata,
                },
            };

            var service = new Stripe.Checkout.SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }
    }
}
