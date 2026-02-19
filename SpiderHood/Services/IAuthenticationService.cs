using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Text;

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

        private List<Models.User> _users = new();
        private List<UserBuildingAssociation> _userBuildings = new();

        private const string DEFAULT_BUILDING_KEY = "defaultBuildingId";
        private readonly IJSRuntime _jsRuntime;

        public event Action? OnAuthStateChanged;
        private BDLayout ec { get; set; }
        public SpiderHoodContext _context = default!;

        public AuthService(SpiderHoodContext context,
           ILogger<AuthService> logger,
           CustomAuthenticationStateProvider authStateProvider,
           IJSRuntime jsRuntime) // Added parameter to satisfy readonly field assignment
        {
            _logger = logger;
            _authStateProvider = authStateProvider;
            _jsRuntime = jsRuntime; // Assign the non-nullable readonly field
            ec = new BDLayout(context);
            //InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            if (_users.Any()) return;

            var adminId = Guid.Parse("BCD5E9DE-0FD2-431D-AB16-B4B9BB19DDC7"); //Guid.NewGuid();

            _users.Add(new Models.User
            {
                IdUser = adminId,
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
                IdUser = adminId,
                IdBuilding = Guid.Parse("C574A553-1573-48C8-F85E-08DE3426C28E"),
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

                _users = await ec.GetUsersByEmailAsync(normalizedEmail!);

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


                _userBuildings = await ec.GetUserBuildingAssociationAsync(user.IdUser);
                List<Building> builds = await ec.GetAllBuildingByOwnerAsync(user.IdUser);
                List<BuildingConfiguration> configurations = await ec.GetAllBuildingsConfigAsync(user.IdUser);

                // Obtener edificios
                foreach (var item in builds)
                {
                    item.Configuration = configurations.Where(c => c.IdBuilding == item.IdBuilding).FirstOrDefault()!;
                }

                var buildings = _userBuildings
                    .Where(ub => ub.IdUser == user.IdUser)
                    .Select(ub => new UserBuilding
                    {
                        Building = builds.FirstOrDefault(b => b.IdBuilding == ub.IdBuilding),

                        Role = ub.Role,
                        IsApproved = ub.IsApproved,
                        ApprovedAt = ub.ApprovedAt
                    }).ToList();

                // Crear sesión
                var session =  (new UserSession
                {
                    IdUser = user.IdUser,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Roles = buildings.Select(b => b.Role).Distinct().ToList(),
                    Buildings = buildings,
                    CurrentBuildingId = GetDefaultBuilding(buildings),
                    RememberMe = model.RememberMe,
                    SessionStart = DateTime.UtcNow,
                    SessionExpiry = DateTime.UtcNow.AddHours(8)
                });

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
                var building = user.Buildings.FirstOrDefault(b => b.Building!.IdBuilding == buildingId.Value);
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

        private Guid GetDefaultBuilding(List<UserBuilding> buildings)
        {
            var approved = buildings.Where(b => b.IsApproved).ToList();
            if (approved.Count == 1)
                return approved[0].Building!.IdBuilding;

            return Guid.Empty;
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
                _logger.LogWarning($"Usuario: {ub.IdUser} -> Edificio: {ub.IdBuilding} - {ub.Role} - Aprobado: {ub.IsApproved}");
            }
        }

        /// <summary>
        /// Guarda el edificio por defecto del usuario actual
        /// </summary>
        public async Task SetDefaultBuildingAsync(Guid buildingId)
        {
            string key = string.Empty;
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null)
                {
                    _logger.LogWarning("⚠️ No hay usuario autenticado para guardar preferencia");
                    return;
                }

                key = $"defaultBuilding_{user.IdUser}"; // Asumiendo que UserSession tiene UserId
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, buildingId.ToString());

                // También guardar el último edificio usado (para sesión actual)
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "lastBuildingId", buildingId.ToString());

                _logger.LogInformation($"✅ Edificio por defecto guardado para usuario {user.IdUser}: {buildingId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"❌ Error guardando edificio por defecto: {key} {buildingId}");
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
                var userKey = $"defaultBuilding_{user.IdUser}";
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

        // Services/AuthService.cs - Agregar estos métodos

        /// <summary>
        /// Limpia las preferencias del usuario (edificio por defecto, etc)
        /// </summary>
        public async Task ClearUserPreferencesAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user != null)
                {
                    var userKey = $"defaultBuilding_{user.IdUser}";
                    await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", userKey);
                    _logger.LogInformation($"✅ Preferencias limpiadas para usuario {user.IdUser}");
                }

                // También limpiar cualquier otra preferencia global
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "lastBuildingId");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "theme");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "sidebarCollapsed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error limpiando preferencias");
            }
        }

        /// <summary>
        /// Limpia la sesión del usuario (datos de autenticación)
        /// </summary>
        public async Task ClearUserSessionAsync()
        {
            try
            {
                // Limpiar caché en memoria
                //_cachedUser = null;

                // Limpiar localStorage
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "currentUser");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "refreshToken");

                // Limpiar sessionStorage si es necesario
                await _jsRuntime.InvokeVoidAsync("sessionStorage.clear");

                _logger.LogInformation("✅ Sesión limpiada exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error limpiando sesión");
            }
        }

        /// <summary>
        /// Obtiene el token de autenticación
        /// </summary>
        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo token");
                return null;
            }
        }
    }

}
