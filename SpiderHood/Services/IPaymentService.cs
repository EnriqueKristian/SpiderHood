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
        Task<string> CreateCheckoutSessionAsync(Guid idUser, string userEmail, int idSubscriptionPlan);
    }

    public class PaymentService : IPaymentService
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly string _baseUrl;
        private readonly bool _isTestMode;
        private readonly string? _testPayerEmail;

        // BaseUrl viene de configuración (mismo valor que ya usa AuthService para
        // los links de los emails), NO de NavigationManager.BaseUri -- ese refleja
        // lo que el navegador tiene puesto en ese momento (p.ej. "localhost" si
        // entraste directo, en vez del túnel público), y MercadoPago rechaza
        // back_url que no sea una URL pública real ("Invalid value for back_url").
        public PaymentService(ISubscriptionService subscriptionService, IConfiguration configuration)
        {
            _subscriptionService = subscriptionService;
            _baseUrl = (configuration["BaseUrl"] ?? "https://localhost:7175").TrimEnd('/');
            // Convención de MercadoPago: sus Access Token de prueba arrancan con
            // "TEST-", los de producción no.
            _isTestMode = (configuration["MercadoPago:AccessToken"] ?? "").StartsWith("TEST-");
            // Email real de la cuenta "Comprador" de MercadoPago > Cuentas de prueba
            // -- NO puede ser cualquier email inventado ("Payer is associated with a
            // different site", probado): tiene que ser una cuenta que exista de
            // verdad en el mismo país/site que el vendedor. Se configura por
            // usuario/máquina vía dotnet user-secrets, nunca commiteado.
            _testPayerEmail = configuration["MercadoPago:TestPayerEmail"];
        }

        public async Task<string> CreateCheckoutSessionAsync(Guid idUser, string userEmail, int idSubscriptionPlan)
        {
            var plans = await _subscriptionService.GetAllPlansAsync();
            var plan = plans.FirstOrDefault(p => p.IdSubscriptionPlan == idSubscriptionPlan)
                ?? throw new InvalidOperationException("El plan solicitado no existe.");

            if (plan.Amount is not { } amount || string.IsNullOrWhiteSpace(plan.CurrencyId))
                throw new InvalidOperationException($"El plan '{plan.Name}' todavía no tiene un precio configurado.");

            // payer_email es obligatorio para la API, pero en modo test NO puede ser
            // el email real del Administrador (choca con la cuenta de prueba que
            // autoriza -- "una de las partes es de prueba", probado) ni uno
            // inventado (tiene que existir de verdad -- "Payer is associated with a
            // different site", también probado). En modo test hace falta el email
            // real de una cuenta de MercadoPago > Cuentas de prueba.
            if (_isTestMode && string.IsNullOrWhiteSpace(_testPayerEmail))
            {
                throw new InvalidOperationException(
                    "Falta configurar \"MercadoPago:TestPayerEmail\" (dotnet user-secrets) con el email de una cuenta de MercadoPago > Cuentas de prueba.");
            }

            var request = new PreapprovalCreateRequest
            {
                Reason = $"Suscripción SpiderHood - {plan.Name}",
                PayerEmail = _isTestMode ? _testPayerEmail : userEmail,
                BackUrl = $"{_baseUrl}/pago-exitoso",
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
