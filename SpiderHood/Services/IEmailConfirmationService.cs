// Services/IEmailConfirmationService.cs
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Text;

namespace SpiderHood.Services
{
    public interface IEmailConfirmationService
    {
        Task<EmailConfirmationResult> ConfirmEmailAsync(string userId, string token);
        Task<EmailConfirmationResult> ResendConfirmationEmailAsync(string email);
        Task<bool> IsEmailConfirmedAsync(string userId);
    }

    public class EmailConfirmationService : IEmailConfirmationService
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailConfirmationService> _logger;
        private readonly IConfiguration _configuration;
        public SpiderHoodContext _context = default!;

        private BDLayout Ec { get; set; }

        public EmailConfirmationService(SpiderHoodContext context,
            UserManager<IdentityUser> userManager,
            IEmailService emailService,
            ILogger<EmailConfirmationService> logger,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
            Ec = new BDLayout(context);
            //_invitationRepository = invitationRepository;
        }

        public async Task<EmailConfirmationResult> ConfirmEmailAsync(string userId, string token)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                {
                    return new EmailConfirmationResult
                    {
                        Success = false,
                        Message = "Enlace de confirmación inválido"
                    };
                }

                // 🔴 NORMALIZAR EMAIL: convertir a minúsculas para comparación
                var _user = await Ec.GetUserByIdAsync(Guid.Parse(userId));


                //var user = await _userManager.FindByIdAsync(userId);
                var user = new IdentityUser
                {
                    Id = userId,
                    UserName = _user.UserName,
                    Email = _user.Email,
                    EmailConfirmed = _user.EmailConfirmed
                };
                if (user == null)
                {
                    return new EmailConfirmationResult
                    {
                        Success = false,
                        Message = "Usuario no encontrado"
                    };
                }

                // Decodificar el token
                var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

                var result = await ConfirmEmailAsync(_user, decodedToken);
                //var result = await _userManager.ConfirmEmailAsync(user, decodedToken);

                if (result.Succeeded)
                {
                    // Verificar si el usuario requiere aprobación
                    var requiresApproval = true; // await _invitationRepository.UserRequiresApprovalAsync(user.Id);

                    _logger.LogInformation("Email confirmado exitosamente para usuario: {Email}", user.Email);

                    return new EmailConfirmationResult
                    {
                        Success = true,
                        Message = "Email confirmado exitosamente",
                        Email = user.Email,
                        RequiresApproval = requiresApproval,
                        EmailConfirmed = true
                    };
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return new EmailConfirmationResult
                    {
                        Success = false,
                        Message = $"Error al confirmar email: {errors}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirmando email para usuario {UserId}", userId);
                return new EmailConfirmationResult
                {
                    Success = false,
                    Message = "Error interno al confirmar el email"
                };
            }
        }

        private async Task<IdentityResult> ConfirmEmailAsync(UserModel user, string decodedToken) {

            IdentityResult result;

            if (user.Token == decodedToken)
            {
                result = IdentityResult.Success;
            }
            else
            {
                result = IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "Error",
                        Description = "Token incorrecto"
                    }
                );
            }
            return result;
        }

        public async Task<EmailConfirmationResult> ResendConfirmationEmailAsync(string email)
        {
            try
            {
                // 🔴 NORMALIZAR EMAIL: convertir a minúsculas para comparación
                var normalizedEmail = email?.Trim().ToLowerInvariant();
                var _users = await Ec.GetUsersByEmailAsync(normalizedEmail!);

                // Buscar usuario con email normalizado
                var _user = _users.FirstOrDefault(u =>
                    u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));

                //var user = await _userManager.FindByEmailAsync(email);
                if (_user == null)
                {
                    // No revelar que el usuario no existe por seguridad
                    return new EmailConfirmationResult
                    {
                        Success = true,
                        Message = "Si el email existe, recibirás un enlace de confirmación"
                    };
                }

                // Crear el usuario
                var user = new IdentityUser
                {
                    Id = _user.IdUser.ToString(),
                    UserName = _user!.Email,
                    Email = _user!.Email,
                    EmailConfirmed = _user!.EmailConfirmed,
                };

                if (_user!.EmailConfirmed)//await _userManager.IsEmailConfirmedAsync(user))
                {
                    return new EmailConfirmationResult
                    {
                        Success = false,
                        Message = "El email ya está confirmado"
                    };
                }

                // Generar token de confirmación
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                _user.Token = token;

                await Ec.UpdateTokenUserAsync(_user);

                var baseUrl = _configuration["BaseUrl"] ?? "https://localhost:7175";
                var confirmationLink = $"{baseUrl}/confirmemail?userId={user.Id}&token={encodedToken}";

                // Enviar email
                /*await _emailService.SendEmailAsync(
                    user.Email!,
                    "Confirma tu email - SpiderHood",
                    GenerateConfirmationEmailBody(user.UserName!, confirmationLink));*/

                //_logger.LogInformation("Email de confirmación reenviado a: {Email}", email);
                _logger.LogInformation("ConfirmationLink {ConfirmationLink}", confirmationLink);

                return new EmailConfirmationResult
                {
                    Success = true,
                    Message = "Email de confirmación enviado"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reenviando email de confirmación a {Email}", email);
                return new EmailConfirmationResult
                {
                    Success = false,
                    Message = "Error al reenviar el email de confirmación"
                };
            }
        }

        public async Task<bool> IsEmailConfirmedAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return false;

            return await _userManager.IsEmailConfirmedAsync(user);
        }

        private string GenerateConfirmationEmailBody(string userName, string confirmationLink)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <style>
                    body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                    .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                    .header {{ background-color: #0d6efd; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                    .content {{ background-color: #f8f9fa; padding: 30px; border-radius: 0 0 5px 5px; }}
                    .button {{ display: inline-block; padding: 12px 30px; background-color: #0d6efd; color: white; 
                              text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                    .button:hover {{ background-color: #0b5ed7; }}
                    .footer {{ margin-top: 30px; text-align: center; color: #6c757d; font-size: 0.9em; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>SpiderHood</h2>
                    </div>
                    <div class='content'>
                        <h3>¡Hola {userName}!</h3>
                        <p>Gracias por registrarte en SpiderHood. Por favor, confirma tu dirección de email haciendo clic en el siguiente enlace:</p>
                        
                        <div style='text-align: center;'>
                            <a href='{confirmationLink}' class='button'>Confirmar Email</a>
                        </div>
                        
                        <p>Si el botón no funciona, copia y pega este enlace en tu navegador:</p>
                        <p style='word-break: break-all; background-color: #e9ecef; padding: 10px; border-radius: 3px;'>
                            {confirmationLink}
                        </p>
                        
                        <p>Si no solicitaste este registro, puedes ignorar este email.</p>
                        
                        <p>Saludos,<br>El equipo de SpiderHood</p>
                    </div>
                    <div class='footer'>
                        <p>&copy; {DateTime.Now.Year} SpiderHood. Todos los derechos reservados.</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}