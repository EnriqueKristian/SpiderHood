using Microsoft.Extensions.Logging;
using SpiderHood.Models;
using System.Text;
using Microsoft.JSInterop;

namespace SpiderHood.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginModel model);
        Task LogoutAsync();
        Task<UserSession?> GetCurrentUserAsync();
        /*Task<UserSession?> GetStoredSessionAsync();*/
        /*UserSession? CurrentUser { get; }
        event Action? OnAuthStateChanged;*/

        // Fixed: Declare as instance method on the interface (not an extension method).
        Task<bool> RequestBuildingAccess(Guid buildingId, string role);
        Task SetCurrentBuilding(Guid? Idbuilding);
    }

    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private readonly CustomAuthenticationStateProvider _authStateProvider;

        private static readonly List<User> _users = new();
        private static readonly List<UserBuildingAssociation> _userBuildings = new();

        private const string DEFAULT_BUILDING_KEY = "defaultBuildingId";
        private readonly IJSRuntime _jsRuntime;

        public event Action? OnAuthStateChanged;

        public AuthService(
            ILogger<AuthService> logger,
            CustomAuthenticationStateProvider authStateProvider)
        {
            _logger = logger;
            _authStateProvider = authStateProvider;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            if (_users.Any()) return;

            var adminId = Guid.NewGuid();

            _users.Add(new User
            {
                Id = adminId,
                // 🔴 Usar minúsculas para consistencia
                Email = "admin@spiderhood.com",
                PasswordHash = HashPassword("Admin123!"),
                FirstName = "Admin",
                LastName = "Principal",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            _userBuildings.Add(new UserBuildingAssociation
            {
                UserId = adminId,
                BuildingId = Guid.Parse("C574A553-1573-48C8-F85E-08DE3426C28E"),
                Role = "Administrador",
                IsApproved = true,
                ApprovedAt = DateTime.UtcNow
            });

            _logger.LogWarning($"✅ Usuario de prueba creado: admin@spiderhood.com / Admin123!");
        }

        // En AuthService.cs, actualizar estos métodos:
        public async Task<AuthResponse> LoginAsync(LoginModel model)
        {
            try
            {
                _logger.LogWarning($"🔵 AuthService.LoginAsync INICIADO - Email: {model.Email}");

                // 🔴 NORMALIZAR EMAIL: convertir a minúsculas para comparación
                var normalizedEmail = model.Email?.Trim().ToLowerInvariant();

                // Buscar usuario con email normalizado
                var user = _users.FirstOrDefault(u =>
                    u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase));

                if (user == null)
                {
                    _logger.LogWarning($"❌ Usuario no encontrado: {model.Email}");
                    return AuthFail("Email o contraseña incorrectos");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning($"❌ Usuario inactivo: {model.Email}");
                    return AuthFail("Tu cuenta está desactivada");
                }

                if (!VerifyPassword(model.Password, user.PasswordHash))
                {
                    _logger.LogWarning($"❌ Contraseña incorrecta para: {model.Email}");
                    return AuthFail("Email o contraseña incorrectos");
                }

                // Obtener edificios
                var buildings = _userBuildings
                    .Where(ub => ub.UserId == user.Id)
                    .Select(ub => new UserBuilding
                    {
                        BuildingId = ub.BuildingId,
                        BuildingName = GetBuildingName(ub.BuildingId),
                        Role = ub.Role,
                        IsApproved = ub.IsApproved,
                        ApprovedAt = ub.ApprovedAt
                    }).ToList();

                // Crear sesión
                var session = new UserSession
                {
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Roles = buildings.Select(b => b.Role).Distinct().ToList(),
                    Buildings = buildings,
                    CurrentBuildingId = GetDefaultBuilding(buildings),
                    RememberMe = model.RememberMe,
                    SessionStart = DateTime.UtcNow,
                    SessionExpiry = DateTime.UtcNow.AddHours(8)
                };

                _logger.LogWarning($"✅ Usuario autenticado: {model.Email}");
                _logger.LogWarning($"🔵 Llamando a MarkUserAsAuthenticated...");

                // Guardar sesión
                await _authStateProvider.MarkUserAsAuthenticated(session);

                _logger.LogWarning($"✅ MarkUserAsAuthenticated completado");

                NotifyAuthStateChanged();

                return new AuthResponse
                {
                    Success = true,
                    UserSession = session,
                    Message = "Login exitoso"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en LoginAsync");
                return AuthFail("Error interno del servidor");
            }
        }

        public async Task<UserSession?> GetCurrentUserAsync()
        {
            return await _authStateProvider.GetCurrentUserAsync();
        }

        public async Task SetCurrentBuilding(Guid? buildingId)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return;

            if (buildingId.HasValue)
            {
                var building = user.Buildings.FirstOrDefault(b => b.BuildingId == buildingId.Value);
                if (building != null)
                {
                    user.CurrentBuildingId = buildingId.Value;
                    await _authStateProvider.MarkUserAsAuthenticated(user);
                }
            }
        }

        public async Task LogoutAsync()
        {
            await _authStateProvider.MarkUserAsLoggedOut();
            NotifyAuthStateChanged();
        }

        // -------------------------------------------
        //        UTILITARIOS
        // -------------------------------------------

        private static AuthResponse AuthFail(string message) =>
            new AuthResponse { Success = false, Message = message };

        private bool VerifyPassword(string password, string hash)
        {
            var hashedInput = HashPassword(password);

            // 🔴 LOGGING PARA DEBUG
            _logger.LogWarning($"🔐 Verificando contraseña:");
            _logger.LogWarning($"   - Input password: {password}");
            _logger.LogWarning($"   - Hash del input: {hashedInput}");
            _logger.LogWarning($"   - Hash almacenado: {hash}");
            _logger.LogWarning($"   - Coinciden: {hashedInput == hash}");

            return hashedInput == hash;
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            var hashString = Convert.ToBase64String(hash);

            _logger.LogWarning($"🔐 Hash generado para '{password}': {hashString}");

            return hashString;
        }

        private string GetBuildingName(Guid id) => "Torre del Sol";

        private Guid? GetDefaultBuilding(List<UserBuilding> buildings)
        {
            var approved = buildings.Where(b => b.IsApproved).ToList();
            if (approved.Count == 1)
                return approved[0].BuildingId;

            return null;
        }

        private void NotifyAuthStateChanged() =>
            OnAuthStateChanged?.Invoke();

        // Fixed: implement as an instance method to satisfy IAuthService.
        public Task<bool> RequestBuildingAccess(Guid buildingId, string role)
        {
            // TODO: Replace with actual request logic (HTTP call, repository, etc.)
            // This stub returns success so the project compiles and the UI flow can proceed.
            return Task.FromResult(true);
        }

        // Fixed: instance method (removed 'this' from parameter)
        public void DebugPrintUsers()
        {
            _logger.LogWarning("=== USUARIOS EN BASE DE DATOS ===");
            foreach (var user in _users)
            {
                _logger.LogWarning($"Usuario: {user.Email}");
                _logger.LogWarning($"  - Hash: {user.PasswordHash}");
                _logger.LogWarning($"  - Activo: {user.IsActive}");
            }

            _logger.LogWarning("=== EDIFICIOS ASIGNADOS ===");
            foreach (var ub in _userBuildings)
            {
                _logger.LogWarning($"Usuario: {ub.UserId} -> Edificio: {ub.BuildingId} - {ub.Role} - Aprobado: {ub.IsApproved}");
            }
        }

        /// <summary>
        /// Guarda el edificio por defecto del usuario actual
        /// </summary>
        public async Task SetDefaultBuildingAsync(Guid buildingId)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                {
                    _logger.LogWarning("⚠️ No hay usuario autenticado para guardar preferencia");
                    return;
                }

                var key = $"defaultBuilding_{user.UserId}"; // Asumiendo que UserSession tiene UserId
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, buildingId.ToString());

                // También guardar el último edificio usado (para sesión actual)
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "lastBuildingId", buildingId.ToString());

                _logger.LogInformation($"✅ Edificio por defecto guardado para usuario {user.UserId}: {buildingId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error guardando edificio por defecto: {buildingId}");
            }
        }

        /// <summary>
        /// Obtiene el edificio por defecto del usuario actual
        /// </summary>
        public async Task<Guid?> GetDefaultBuildingAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                    return null;

                // Primero intentar con el específico del usuario
                var userKey = $"defaultBuilding_{user.UserId}";
                var buildingIdString = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", userKey);

                if (Guid.TryParse(buildingIdString, out Guid buildingId))
                    return buildingId;

                // Si no, intentar con el último usado (compatibilidad hacia atrás)
                var lastUsed = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "lastBuildingId");
                if (Guid.TryParse(lastUsed, out Guid lastUsedId))
                    return lastUsedId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo edificio por defecto");
            }

            return null;
        }

        /// <summary>
        /// Limpia las preferencias del usuario al hacer logout
        /// </summary>
        public async Task ClearUserPreferencesAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user != null)
                {
                    var userKey = $"defaultBuilding_{user.UserId}";
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", userKey);
                }

                // No limpiar lastBuildingId porque podría ser útil para el próximo login
                _logger.LogInformation("✅ Preferencias de usuario limpiadas");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error limpiando preferencias");
            }
        }

    }

}

// Modelos internos
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UserBuildingAssociation
    {
        public Guid UserId { get; set; }
        public Guid BuildingId { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
    }

    public class RegisterResult
    {
        public bool Success { get; set; }
        public string[] Errors { get; set; } = new string[0];
    }
