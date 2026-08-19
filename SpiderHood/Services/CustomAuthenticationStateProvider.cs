using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using SpiderHood.Models;
using System.Security.Claims;
using System.Text.Json;

namespace SpiderHood.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService _localStorage;
        private readonly ILogger<CustomAuthenticationStateProvider> _logger;

        // FIX CRÍTICO: estos campos eran `static`, es decir compartidos por TODOS los
        // usuarios conectados al servidor (Blazor Server es un solo proceso sirviendo a
        // muchos circuitos). Eso podía filtrar la sesión de un usuario a otro. Ahora son
        // campos de instancia, correctamente aislados por circuito ya que este servicio
        // está registrado como Scoped.
        private UserSession? _staticCurrentUser;
        private bool _staticIsClientInitialized = false;

        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        private bool _isPrerendering = true;

        public CustomAuthenticationStateProvider(
            ILocalStorageService localStorage,
            ILogger<CustomAuthenticationStateProvider> logger)
        {
            _localStorage = localStorage;
            _logger = logger;
            //_logger.LogWarning($"🔴 CustomAuthenticationStateProvider CONSTRUIDO - Hash: {this.GetHashCode()}");
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            _logger.LogWarning($"📌 GetAuthenticationStateAsync - Instancia: {this.GetHashCode()}, _isPrerendering: {_isPrerendering}, _staticIsClientInitialized: {_staticIsClientInitialized}");

            try
            {
                // 1. Si hay usuario estático, usarlo
                if (_staticCurrentUser?.IsAuthenticated == true)
                {
                    _logger.LogWarning($"✅ Usuario estático encontrado: {_staticCurrentUser.Email}");
                    return CreateAuthenticationState(_staticCurrentUser);
                }

                // 2. Si estamos en prerendering, retornar anónimo
                if (_isPrerendering)
                {
                    _logger.LogWarning("⚠️ Prerendering - retornando anónimo");
                    return new AuthenticationState(_anonymous);
                }

                // 3. Si no estamos en cliente, retornar anónimo
                if (!_staticIsClientInitialized)
                {
                    _logger.LogWarning("⚠️ Cliente no inicializado - retornando anónimo");
                    return new AuthenticationState(_anonymous);
                }

                // 4. Intentar cargar desde localStorage
                _logger.LogWarning("📦 Intentando cargar desde localStorage...");

                try
                {
                    var sessionJson = await _localStorage.GetItemAsync<string>("userSession");

                    _logger.LogWarning($"📦 localStorage.GetItemAsync: {(string.IsNullOrEmpty(sessionJson) ? "null" : "tiene datos")}");

                    if (!string.IsNullOrEmpty(sessionJson))
                    {
                        var session = JsonSerializer.Deserialize<UserSession>(sessionJson);

                        if (session?.IsAuthenticated == true)
                        {
                            _logger.LogWarning($"✅ Sesión cargada para: {session.Email}");
                            _staticCurrentUser = session;
                            return CreateAuthenticationState(session);
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogError(ex, "❌ Error de operación inválida - probablemente prerendering");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error al acceder a localStorage");
                }

                return new AuthenticationState(_anonymous);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en GetAuthenticationStateAsync");
                return new AuthenticationState(_anonymous);
            }
        }

        public async Task InitializeClientAsync()
        {
            if (_staticIsClientInitialized)
            {
                //_logger.LogWarning($"⚠️ Cliente ya inicializado - Instancia: {this.GetHashCode()}");
                return;
            }

            _logger.LogWarning($"🔄 InitializeClientAsync INICIADO - Instancia: {this.GetHashCode()}");

            try
            {
                _isPrerendering = false;

                //_logger.LogWarning("📦 Cargando desde localStorage en InitializeClientAsync...");

                var sessionJson = await _localStorage.GetItemAsync<string>("userSession");

                //_logger.LogWarning($"📦 Resultado: {(string.IsNullOrEmpty(sessionJson) ? "vacío" : "tiene datos")}");

                if (!string.IsNullOrEmpty(sessionJson))
                {
                    var session = JsonSerializer.Deserialize<UserSession>(sessionJson);

                    if (session?.IsAuthenticated == true)
                    {
                        _logger.LogWarning($"✅ Sesión restaurada: {session.Email}");
                        _staticCurrentUser = session;

                        //      _logger.LogWarning("📢 Notificando cambio de estado...");
                        NotifyAuthenticationStateChanged(
                            Task.FromResult(CreateAuthenticationState(session)));
                    }
                }

                _staticIsClientInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en InitializeClientAsync");
                _staticIsClientInitialized = true;
            }

            _logger.LogWarning($"🔄 InitializeClientAsync FINALIZADO - Instancia: {this.GetHashCode()}");
        }

        public async Task MarkUserAsAuthenticated(UserSession session)
        {
            //_logger.LogWarning($"🔐 MarkUserAsAuthenticated INICIADO para: {session.Email} - Instancia: {this.GetHashCode()}");

            try
            {
                // Guardar en memoria estática (compartida entre instancias)
                _staticCurrentUser = session;
                _isPrerendering = false;

                // Intentar guardar en localStorage
                try
                {
                    //_logger.LogWarning("💾 Guardando en localStorage...");
                    var sessionJson = JsonSerializer.Serialize(session);
                    await _localStorage.SetItemAsync("userSession", sessionJson);
                    //_logger.LogWarning("✅ Sesión guardada en localStorage");
                    _staticIsClientInitialized = true;
                }
                catch (InvalidOperationException)
                {
                    _logger.LogWarning("⚠️ No se pudo guardar en localStorage - estamos en prerendering");
                    //_logger.LogWarning($"✅ Pero la sesión está guardada en memoria estática: {_staticCurrentUser.Email}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error guardando en localStorage");
                }

                // Notificar cambio de estado
                //_logger.LogWarning("📢 Notificando cambio de estado...");
                var authState = CreateAuthenticationState(session);
                NotifyAuthenticationStateChanged(Task.FromResult(authState));

                //_logger.LogWarning($"✅ MarkUserAsAuthenticated COMPLETADO - Instancia: {this.GetHashCode()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en MarkUserAsAuthenticated");
                throw;
            }
        }

        public async Task MarkUserAsLoggedOut()
        {
            _logger.LogWarning($"🚪 MarkUserAsLoggedOut INICIADO - Instancia: {this.GetHashCode()}");

            try
            {
                _staticCurrentUser = null;

                try
                {
                    _logger.LogWarning("🗑️ Eliminando de localStorage...");
                    await _localStorage.RemoveItemAsync("userSession");
                    _logger.LogWarning("✅ Eliminado de localStorage");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error eliminando de localStorage");
                }

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en MarkUserAsLoggedOut");
            }
        }

        public async Task<UserSession?> GetCurrentUserAsync()
        {
            //_logger.LogWarning($"👤 GetCurrentUserAsync llamado - Instancia: {this.GetHashCode()}, Memoria estática: {_staticCurrentUser?.IsAuthenticated}");

            // PRIMERO: Verificar memoria estática
            if (_staticCurrentUser?.IsAuthenticated == true)
            {
                //_logger.LogWarning($"✅ Usuario encontrado en memoria estática: {_staticCurrentUser.Email}");
                return _staticCurrentUser;
            }

            // SEGUNDO: Intentar obtener del estado
            var authState = await GetAuthenticationStateAsync();

            if (_staticCurrentUser?.IsAuthenticated == true)
            {
                //_logger.LogWarning($"✅ Usuario recuperado vía GetAuthenticationState: {_staticCurrentUser.Email}");
                return _staticCurrentUser;
            }

            //_logger.LogWarning("❌ No se encontró usuario autenticado");
            return null;
        }

        private AuthenticationState CreateAuthenticationState(UserSession session)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, session.IdUser.ToString()),
                new Claim(ClaimTypes.Name, session.FullName),
                new Claim(ClaimTypes.Email, session.Email),
                new Claim("SessionStart", session.SessionStart.ToString()),
                new Claim("SessionExpiry", session.SessionExpiry.ToString())
            };

            foreach (var role in session.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            if (session.CurrentBuildingId != Guid.Empty)
            {
                claims.Add(new Claim("CurrentBuildingId", session.CurrentBuildingId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, "custom");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public async Task NotifyUserLogoutAsync()
        {
            try
            {
                await _localStorage.RemoveItemAsync("currentUser");
                await _localStorage.RemoveItemAsync("authToken");

                NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));

                _logger.LogInformation("✅ Usuario desautenticado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notificando logout");
            }
        }
    }
}