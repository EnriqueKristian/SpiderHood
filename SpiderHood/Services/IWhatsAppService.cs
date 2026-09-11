using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SpiderHood.Services
{
    // Envío de WhatsApp vía Twilio -- Docs/Pendientes-Negocio-Consolidado.md #17
    // (canal prioritario del módulo de Comunicados, decidido 2026-09-11). Twilio
    // elegido sobre la Cloud API directa de Meta por velocidad de arranque del
    // piloto (SDK .NET listo, sin webhooks propios que mantener).
    //
    // Sin cuenta de Twilio/verificación de negocio en Meta todavía (trámite
    // aparte, no instantáneo) -- este servicio queda listo para conectar en
    // cuanto exista, y mientras tanto corre en modo "Simulate" (mismo patrón
    // que MercadoPago en IPaymentService): sin AccountSid/AuthToken configurados
    // no llama a Twilio de verdad, sólo loguea, para no bloquear el resto del
    // trabajo (ej. construir la pantalla de Comunicados) esperando el trámite.
    public interface IWhatsAppService
    {
        // Mensaje de texto libre. Fuera del sandbox de Twilio (o de la ventana
        // de 24hs de una conversación que el destinatario inició), WhatsApp
        // Business exige plantillas pre-aprobadas por Meta para mensajes que
        // inicia el negocio -- ver SendTemplateMessageAsync. Sirve para probar
        // el circuito completo contra el sandbox de Twilio mientras se gestiona
        // la cuenta real.
        Task<bool> SendMessageAsync(string toPhoneNumber, string message);

        // Mensaje con un Content Template ya aprobado (Twilio Content API --
        // contentSid es el Id que asigna Twilio a la plantilla una vez
        // aprobada por Meta). No se puede usar todavía -- no existe ninguna
        // plantilla dada de alta -- queda listo para cuando exista la primera.
        Task<bool> SendTemplateMessageAsync(string toPhoneNumber, string contentSid, IDictionary<string, string>? contentVariables = null);

        // Expuesto aparte para poder validar un número (o depurar por qué no
        // se pudo mandar algo) sin tener que mandar un mensaje real.
        string? NormalizeToE164(string phoneNumber);
    }

    public class WhatsAppService : IWhatsAppService
    {
        private readonly ILogger<WhatsAppService> _logger;
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber;
        private readonly string _defaultCountryCode;
        private readonly bool _simulate;

        public WhatsAppService(IConfiguration configuration, ILogger<WhatsAppService> logger)
        {
            _logger = logger;
            _accountSid = configuration["Twilio:AccountSid"] ?? "";
            _authToken = configuration["Twilio:AuthToken"] ?? "";
            // Sandbox de Twilio por default -- permite probar sin un número de
            // WhatsApp Business propio todavía (cada número de prueba tiene que
            // "unirse" al sandbox mandando el código que da Twilio).
            _fromNumber = configuration["Twilio:WhatsAppFrom"] ?? "whatsapp:+14155238886";
            // Los edificios vistos hasta ahora en este código son de Perú
            // (BuildingConfiguration.Currency default "PEN") -- se usa como
            // default para completar números sin código de país, pero es
            // configurable: BuildingPage.razor.cs también ofrece USD/EUR, así
            // que no puede quedar hardcodeado si algún día hay un edificio
            // fuera de Perú.
            _defaultCountryCode = configuration["Twilio:DefaultCountryCode"] ?? "51";
            // Igual que MercadoPago (IPaymentService): sin credenciales
            // configuradas, o con Twilio:Simulate=true a propósito, no se llama
            // a Twilio de verdad. Apagado (es decir, "simular") por default
            // hasta que existan AccountSid/AuthToken reales.
            _simulate = configuration.GetValue<bool>("Twilio:Simulate")
                || string.IsNullOrWhiteSpace(_accountSid)
                || string.IsNullOrWhiteSpace(_authToken);
        }

        public async Task<bool> SendMessageAsync(string toPhoneNumber, string message)
        {
            var to = NormalizeToE164(toPhoneNumber);
            if (to is null)
            {
                _logger.LogWarning("WhatsApp: número inválido, no se pudo normalizar a E.164: {Phone}", toPhoneNumber);
                return false;
            }

            if (_simulate)
            {
                _logger.LogInformation(
                    "WhatsApp SIMULADO (sin credenciales de Twilio configuradas) a {To}: {Message}",
                    to, message);
                return true;
            }

            try
            {
                TwilioClient.Init(_accountSid, _authToken);
                var result = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_fromNumber),
                    to: new PhoneNumber($"whatsapp:{to}"));

                _logger.LogInformation("WhatsApp enviado a {To} -- Sid={Sid} Status={Status}", to, result.Sid, result.Status);
                return result.Status != MessageResource.StatusEnum.Failed
                    && result.Status != MessageResource.StatusEnum.Undelivered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando WhatsApp a {To}", to);
                return false;
            }
        }

        public async Task<bool> SendTemplateMessageAsync(string toPhoneNumber, string contentSid, IDictionary<string, string>? contentVariables = null)
        {
            var to = NormalizeToE164(toPhoneNumber);
            if (to is null)
            {
                _logger.LogWarning("WhatsApp: número inválido, no se pudo normalizar a E.164: {Phone}", toPhoneNumber);
                return false;
            }

            if (_simulate)
            {
                _logger.LogInformation(
                    "WhatsApp SIMULADO (sin credenciales de Twilio configuradas) a {To} -- plantilla {ContentSid}",
                    to, contentSid);
                return true;
            }

            try
            {
                TwilioClient.Init(_accountSid, _authToken);
                var result = await MessageResource.CreateAsync(
                    from: new PhoneNumber(_fromNumber),
                    to: new PhoneNumber($"whatsapp:{to}"),
                    contentSid: contentSid,
                    contentVariables: contentVariables is null
                        ? null
                        : System.Text.Json.JsonSerializer.Serialize(contentVariables));

                _logger.LogInformation(
                    "WhatsApp (plantilla {ContentSid}) enviado a {To} -- Sid={Sid} Status={Status}",
                    contentSid, to, result.Sid, result.Status);
                return result.Status != MessageResource.StatusEnum.Failed
                    && result.Status != MessageResource.StatusEnum.Undelivered;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando WhatsApp (plantilla {ContentSid}) a {To}", contentSid, to);
                return false;
            }
        }

        // E.164 (https://en.wikipedia.org/wiki/E.164): '+' seguido de 8 a 15
        // dígitos, sin espacios/guiones/paréntesis -- formato que exige
        // WhatsApp. `User`/`Owner`/`Contact.PhoneNumber` (Classes/User.cs) hoy
        // son texto libre, así que hay que normalizar antes de mandar nada.
        public string? NormalizeToE164(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber)) return null;

            var hadPlus = phoneNumber.TrimStart().StartsWith('+');
            var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
            if (digits.Length == 0) return null;

            // "00" internacional (común fuera de LatAm) equivale a '+'.
            if (!hadPlus && digits.StartsWith("00"))
            {
                digits = digits[2..];
                hadPlus = true;
            }
            // Prefijo de discado nacional/troncal ("0" + código de área, ej.
            // fijos de Lima "01-XXXXXXX") -- se descarta antes de anteponer el
            // código de país, igual que se hace para marcar internacional.
            // Probado con datos reales de ejemplo (smoke test manual): sin
            // esto, "01 999 8877" quedaba mal normalizado.
            else if (!hadPlus && digits.StartsWith('0'))
            {
                digits = digits[1..];
            }

            if (!hadPlus)
            {
                // Sin código de país: se asume el default configurado (Perú,
                // celulares de 9 dígitos) -- si el número ya trae más dígitos
                // que uno local, se deja tal cual para no adivinar mal un
                // código de país que ya viniera incluido sin '+'.
                if (digits.Length <= 9)
                    digits = _defaultCountryCode + digits;
            }

            return digits.Length is >= 8 and <= 15 ? "+" + digits : null;
        }
    }
}
