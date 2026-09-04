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
        private readonly string? _testPayerEmail;
        private readonly bool _simulate;

        // BaseUrl viene de configuración (mismo valor que ya usa AuthService para
        // los links de los emails), NO de NavigationManager.BaseUri -- ese refleja
        // lo que el navegador tiene puesto en ese momento (p.ej. "localhost" si
        // entraste directo, en vez del túnel público), y MercadoPago rechaza
        // back_url que no sea una URL pública real ("Invalid value for back_url").
        public PaymentService(ISubscriptionService subscriptionService, IConfiguration configuration)
        {
            _subscriptionService = subscriptionService;
            _baseUrl = (configuration["BaseUrl"] ?? "https://localhost:7175").TrimEnd('/');
            // Email real de la cuenta "Comprador" de MercadoPago > Cuentas de prueba
            // -- NO puede ser cualquier email inventado ("Payer is associated with a
            // different site", probado): tiene que ser una cuenta que exista de
            // verdad en el mismo país/site que el vendedor. Se configura por
            // usuario/máquina vía dotnet user-secrets, nunca commiteado. Si está
            // configurado, se lo usa en vez del email real -- "modo test" se define
            // por esto, NO por el prefijo del Access Token: un vendedor que a su vez
            // es una cuenta de MercadoPago de prueba (creada en "Cuentas de prueba")
            // sólo tiene un juego de credenciales, etiquetado "de producción" en su
            // Dashboard, pese a operar 100% en el ambiente de test (probado).
            _testPayerEmail = configuration["MercadoPago:TestPayerEmail"];
            // Simulador (Docs/Design-Subscripcion-Administrador.md): pensado para no
            // bloquearse en configuración de MercadoPago mientras se prueba el resto
            // del flujo (activación, BD, /Settings). Apagado por default -- nunca
            // "true" commiteado, se prende sólo por dotnet user-secrets.
            _simulate = configuration.GetValue<bool>("MercadoPago:Simulate");
        }

        public async Task<string> CreateCheckoutSessionAsync(Guid idUser, string userEmail, int idSubscriptionPlan)
        {
            var plans = await _subscriptionService.GetAllPlansAsync();
            var plan = plans.FirstOrDefault(p => p.IdSubscriptionPlan == idSubscriptionPlan)
                ?? throw new InvalidOperationException("El plan solicitado no existe.");

            if (plan.Amount is not { } amount || string.IsNullOrWhiteSpace(plan.CurrencyId))
                throw new InvalidOperationException($"El plan '{plan.Name}' todavía no tiene un precio configurado.");

            if (_simulate)
                return $"{_baseUrl}/pago-simulado?u={idUser}&p={idSubscriptionPlan}";

            var request = new PreapprovalCreateRequest
            {
                Reason = $"Suscripción SpiderHood - {plan.Name}",
                // payer_email es obligatorio para la API. Si hay un TestPayerEmail
                // configurado, se usa ese (probando contra un vendedor de prueba) --
                // el email real del Administrador choca con eso ("una de las partes
                // es de prueba", probado). Sin TestPayerEmail configurado (producción
                // real), va el email real del Administrador.
                PayerEmail = string.IsNullOrWhiteSpace(_testPayerEmail) ? userEmail : _testPayerEmail,
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
