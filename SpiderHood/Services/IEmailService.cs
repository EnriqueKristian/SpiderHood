// SpiderHood/Services/IEmailService.cs
using System.Net;
using System.Net.Mail;


namespace SpiderHood.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task SendEmailWithTemplateAsync(string to, string subject, string templateName, object model);
        Task<bool> IsValidEmailAsync(string email);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            // Leer configuración
            _smtpServer = configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(configuration["Email:SmtpPort"] ?? "587");
            _smtpUser = configuration["Email:SmtpUser"] ?? "";
            _smtpPass = configuration["Email:SmtpPassword"] ?? "";
            _fromEmail = configuration["Email:FromEmail"] ?? "noreply@spiderhood.com";
            _fromName = configuration["Email:FromName"] ?? "SpiderHood";
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                using var message = new MailMessage();
                using var client = new SmtpClient(_smtpServer, _smtpPort);

                message.From = new MailAddress(_fromEmail, _fromName);
                message.To.Add(to);
                message.Subject = subject;
                message.Body = body;
                message.IsBodyHtml = true;

                client.EnableSsl = true;
                client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);

                await client.SendMailAsync(message);

                _logger.LogInformation("Email enviado exitosamente a: {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email a: {To}", to);
                throw;
            }
        }

        public async Task SendEmailWithTemplateAsync(string to, string subject, string templateName, object model)
        {
            // Implementar si necesitas plantillas HTML
            var body = await RenderTemplateAsync(templateName, model);
            await SendEmailAsync(to, subject, body);
        }

        public Task<bool> IsValidEmailAsync(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return Task.FromResult(addr.Address == email);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        private Task<string> RenderTemplateAsync(string templateName, object model)
        {
            // Implementar renderizado de plantillas si es necesario
            return Task.FromResult(string.Empty);
        }
    }
}