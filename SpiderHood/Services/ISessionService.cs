using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface ISessionService
    {
        Task<List<ActiveSession>> GetActiveSessionsAsync(Guid userId);
        Task<AuthResult> RevokeSessionAsync(Guid sessionId);
        Task<AuthResult> RevokeAllSessionsAsync(Guid userId);
        Task<AuthResult> LogAccessAsync(Guid userId, string ipAddress, string device);
    }

    public class ActiveSession
    {
        public Guid Id { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public DateTime LastAccess { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class SessionService : ISessionService
    {
        private readonly BDLayout _db;
        private readonly ILogger<SessionService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionService(
            BDLayout db,
            ILogger<SessionService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _db = db;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<List<ActiveSession>> GetActiveSessionsAsync(Guid userId)
        {
            try
            {
                var sessions = new List<ActiveSession>();// await _db.GetActiveSessionsAsync(userId);

                // Determinar cuál es la sesión actual
                var currentSessionId = _httpContextAccessor.HttpContext?.Session.Id;

                foreach (var session in sessions)
                {
                    session.IsCurrent = session.Id.ToString() == currentSessionId;
                }

                return sessions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener sesiones activas para usuario {UserId}", userId);
                return [];
            }
        }

        public async Task<AuthResult> RevokeSessionAsync(Guid sessionId)
        {
            try
            {
                var result = true; // await _db.RevokeSessionAsync(sessionId);
                if (result)
                {
                    _logger.LogInformation("Sesión {SessionId} revocada exitosamente", sessionId);
                    return new AuthResult { Success = true, Message = "Sesión cerrada exitosamente." };
                }
                else
                {
                    return new AuthResult { Success = false, Message = "No se pudo cerrar la sesión." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revocar sesión {SessionId}", sessionId);
                return new AuthResult { Success = false, Message = "Error al cerrar la sesión." };
            }
        }

        public async Task<AuthResult> RevokeAllSessionsAsync(Guid userId)
        {
            try
            {
                var currentSessionId = _httpContextAccessor.HttpContext?.Session.Id;
                var result = true;// await _db.RevokeAllSessionsAsync(userId, currentSessionId);

                if (result)
                {
                    _logger.LogInformation("Todas las sesiones revocadas para usuario {UserId}", userId);
                    return new AuthResult { Success = true, Message = "Todas las sesiones cerradas exitosamente." };
                }
                else
                {
                    return new AuthResult { Success = false, Message = "No se pudieron cerrar las sesiones." };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al revocar todas las sesiones para usuario {UserId}", userId);
                return new AuthResult { Success = false, Message = "Error al cerrar las sesiones." };
            }
        }

        public async Task<AuthResult> LogAccessAsync(Guid userId, string ipAddress, string device)
        {
            try
            {
                //await _db.LogAccessAsync(userId, ipAddress, device);
                return new AuthResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar acceso para usuario {UserId}", userId);
                return new AuthResult { Success = false };
            }
        }

        public async Task<AuthResult> CreateSessionAsync(Guid userId, string device, string ipAddress)
        {
            try
            {
                // Crear nueva sesión en base de datos
                //var sessionId = await _db.CreateSessionAsync(userId, device, ipAddress);

                // Almacenar ID de sesión en HttpContext
                //_httpContextAccessor.HttpContext?.Session.SetString("SessionId", sessionId.ToString());

                // Registrar el acceso
                //await LogAccessAsync(userId, ipAddress, device);

                _logger.LogInformation("Sesión creada para usuario {UserId}", userId);
                return new AuthResult { Success = true };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear sesión para usuario {UserId}", userId);
                return new AuthResult { Success = false };
            }
        }
    }
}