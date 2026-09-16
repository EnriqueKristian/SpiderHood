// Services/IPasswordResetService.cs
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Security.Cryptography;

namespace SpiderHood.Services
{
    public interface IPasswordResetService
    {
        Task<PasswordResetResult> RequestPasswordResetAsync(string email);
        Task<PasswordResetResult> ResetPasswordAsync(string userId, string token, string newPassword);
    }

    // "¿Olvidaste tu contraseña?" -- Login.razor ya tenía el link a /forgot-password,
    // pero no existía nada del lado del servidor (ni la página, ni token, ni SP).
    //
    // A diferencia de IEmailConfirmationService (que inyecta UserManager<IdentityUser>,
    // un flujo que en la práctica no funciona porque nunca se crea un IdentityUser real
    // -- ver el comentario grande en Program.cs sobre AddIdentity), este servicio sigue
    // el mismo patrón que el resto de la app: Users (tabla propia, vía BDLayout) y
    // PasswordHasher<UserModel> standalone (el mismo que ya usa AuthService.LoginAsync/
    // ChangePasswordAsync) para no reinventar el hasheo. El token es un valor aleatorio
    // propio (RandomNumberGenerator), no un token de ASP.NET Identity -- no hace falta
    // esa capa para algo que se valida con una simple comparación + expiración.
    public class PasswordResetService : IPasswordResetService
    {
        private readonly BDLayout _ec;
        private readonly AuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IConfiguration _configuration;

        // Ventana de validez del link -- igual de razonable que cualquier reset por
        // email (ni tan corta que expire antes de que alguien revise su bandeja, ni
        // tan larga que un link viejo en la bandeja quede utilizable por mucho tiempo).
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(30);

        public PasswordResetService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            AuthService authService,
            IEmailService emailService,
            ILogger<PasswordResetService> logger,
            IConfiguration configuration)
        {
            _ec = new BDLayout(contextFactory);
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<PasswordResetResult> RequestPasswordResetAsync(string email)
        {
            // Mismo mensaje genérico haya o no una cuenta con ese email -- no hay que
            // revelarle a quien pregunta si un email está o no registrado (mismo
            // criterio que IEmailConfirmationService.ResendConfirmationEmailAsync).
            var generic = new PasswordResetResult
            {
                Success = true,
                Message = "Si el email existe, vas a recibir un enlace para restablecer tu contraseña."
            };

            try
            {
                var normalizedEmail = email?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(normalizedEmail))
                    return generic;

                var users = await _ec.GetUsersByEmailAsync(normalizedEmail);
                var user = users.FirstOrDefault(u => u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));
                if (user == null || !user.IsActive)
                    return generic;

                var tokenBytes = RandomNumberGenerator.GetBytes(32);
                var token = WebEncoders.Base64UrlEncode(tokenBytes);
                var expiresAt = DateTime.UtcNow.Add(TokenLifetime);

                await _ec.UpdateUserPasswordResetTokenAsync(user.IdUser, token, expiresAt);

                var baseUrl = _configuration["BaseUrl"] ?? "https://localhost:7175";
                var resetLink = $"{baseUrl}/reset-password?userId={user.IdUser}&token={Uri.EscapeDataString(token)}";

                // Siempre se loguea el link primero -- así, si SendEmailAsync se cuelga
                // (SMTP inalcanzable, típico en un entorno de pruebas sin credenciales
                // reales o sin salida de red), el link ya quedó disponible para probar
                // el flujo sin depender de que el email realmente salga.
                _logger.LogInformation("Password reset link para {Email}: {ResetLink}", user.Email, resetLink);

                try
                {
                    // Timeout propio: MailKit no aplica uno por defecto a ConnectAsync,
                    // así que un SMTP que no contesta (host inalcanzable, puerto bloqueado)
                    // colgaría este request entero -- el usuario nunca vería el mensaje
                    // genérico de "revisá tu email" ni siquiera un error, porque
                    // HandleSubmit del lado de la página jamás terminaría. El token ya
                    // quedó guardado arriba, así que el link sigue siendo válido aunque
                    // el email no llegue a salir a tiempo.
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await _emailService.SendEmailAsync(
                        user.Email,
                        "Restablecer tu contraseña - SpiderHood",
                        BuildResetEmailBody(user.FirstName, resetLink)).WaitAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error enviando email de restablecimiento a {Email}", user.Email);
                }

                return generic;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando solicitud de restablecimiento para {Email}", email);
                return generic;
            }
        }

        public async Task<PasswordResetResult> ResetPasswordAsync(string userId, string token, string newPassword)
        {
            const string invalidMessage = "El enlace es inválido o ya expiró. Solicitá uno nuevo.";

            try
            {
                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                    return new PasswordResetResult { Success = false, Message = invalidMessage };

                if (!Guid.TryParse(userId, out var idUser))
                    return new PasswordResetResult { Success = false, Message = invalidMessage };

                UserModel user;
                try
                {
                    user = await _ec.GetUserByIdAsync(idUser);
                }
                catch (RepositoryException)
                {
                    // GetUserByIdAsync envuelve el EntityNotFoundException interno en
                    // RepositoryException (ver BDLayout.ExecuteWithErrorHandlingAsync) --
                    // un userId que no existe (link armado a mano) cae acá, no en un
                    // EntityNotFoundException suelto.
                    return new PasswordResetResult { Success = false, Message = invalidMessage };
                }

                if (string.IsNullOrEmpty(user.PasswordResetToken)
                    || user.PasswordResetToken != token
                    || user.PasswordResetTokenExpiresAt == null
                    || user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
                {
                    return new PasswordResetResult { Success = false, Message = invalidMessage };
                }

                // Reusa AdminResetPasswordAsync (hashea con el mismo PasswordHasher que
                // ChangePasswordAsync/LoginAsync, persiste y revoca cualquier sesión ya
                // abierta -- lo mismo que un admin restableciendo la contraseña de un
                // usuario que sospecha comprometido, que es exactamente lo que esto es).
                var result = await _authService.AdminResetPasswordAsync(idUser, newPassword);
                if (!result.Success)
                    return new PasswordResetResult { Success = false, Message = result.Message };

                // Invalidar el token apenas se usa -- no debe servir dos veces.
                await _ec.UpdateUserPasswordResetTokenAsync(idUser, null, null);

                return new PasswordResetResult { Success = true, Message = "Tu contraseña fue actualizada. Ya podés iniciar sesión." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restableciendo contraseña para usuario {UserId}", userId);
                return new PasswordResetResult { Success = false, Message = "No se pudo restablecer la contraseña." };
            }
        }

        private static string BuildResetEmailBody(string firstName, string resetLink)
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
                    .footer {{ margin-top: 30px; text-align: center; color: #6c757d; font-size: 0.9em; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h2>SpiderHood</h2>
                    </div>
                    <div class='content'>
                        <h3>¡Hola {firstName}!</h3>
                        <p>Recibimos un pedido para restablecer tu contraseña. Hacé clic en el siguiente enlace para elegir una nueva (válido por 30 minutos):</p>

                        <div style='text-align: center;'>
                            <a href='{resetLink}' class='button'>Restablecer contraseña</a>
                        </div>

                        <p>Si el botón no funciona, copiá y pegá este enlace en tu navegador:</p>
                        <p style='word-break: break-all; background-color: #e9ecef; padding: 10px; border-radius: 3px;'>
                            {resetLink}
                        </p>

                        <p>Si no pediste este cambio, podés ignorar este email -- tu contraseña actual sigue funcionando.</p>

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
