using Microsoft.AspNetCore.Components.Authorization;
using SpiderHood.Models;
using System.Security.Claims;

namespace SpiderHood.Services
{
    // Antes esta clase reconstruía la sesión leyendo "userSession" de localStorage vía
    // JS interop — lo que sólo estaba disponible una vez conectado el circuito, nunca
    // durante el prerender, y obligaba a los componentes a "adivinar" (con reintentos
    // y delays, ver Home.razor.cs) cuándo ya había terminado de cargar.
    //
    // Ahora la identidad viene de la cookie de autenticación de ASP.NET Core (ver
    // Program.cs → AddAuthentication().AddCookie(...)), que ya está resuelta por el
    // middleware ANTES de que se arme el árbol de componentes — no depende de JS y no
    // hay carrera posible. El HttpContext sólo existe durante la request que crea este
    // scope (prerender o negotiate del circuito), así que el ClaimsPrincipal se captura
    // una sola vez en el constructor.
    //
    // La cookie sólo transporta identidad (IdUser, email, roles). El resto de la
    // sesión (edificios, edificio actual) se reconstruye desde la base de datos vía
    // IUserSessionLoader la primera vez que se pide en cada circuito, y de ahí en
    // adelante vive en memoria (_currentUser), igual que antes.
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IUserSessionLoader _sessionLoader;
        private readonly ILogger<CustomAuthenticationStateProvider> _logger;
        private readonly ClaimsPrincipal _initialPrincipal;
        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        private UserSession? _currentUser;
        private Task<UserSession?>? _hydrationTask;

        public CustomAuthenticationStateProvider(
            IHttpContextAccessor httpContextAccessor,
            IUserSessionLoader sessionLoader,
            ILogger<CustomAuthenticationStateProvider> logger)
        {
            _sessionLoader = sessionLoader;
            _logger = logger;
            _initialPrincipal = httpContextAccessor.HttpContext?.User ?? _anonymous;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var session = await EnsureHydratedAsync();
            return session != null ? CreateAuthenticationState(session) : new AuthenticationState(_anonymous);
        }

        // Reconstruye _currentUser desde la cookie (una sola vez por circuito, incluso
        // si varios componentes la piden a la vez — de ahí el memoizado).
        private Task<UserSession?> EnsureHydratedAsync()
        {
            if (_currentUser != null)
                return Task.FromResult<UserSession?>(_currentUser);

            _hydrationTask ??= HydrateFromCookieAsync();
            return _hydrationTask;
        }

        private async Task<UserSession?> HydrateFromCookieAsync()
        {
            var idClaim = _initialPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (_initialPrincipal.Identity?.IsAuthenticated != true || !Guid.TryParse(idClaim, out var idUser))
            {
                // Cookie ausente o realmente anónima: es un resultado estable, no hace
                // falta reintentar (y no hubo llamada a BD, así que reintentar no cuesta
                // nada tampoco — pero liberamos igual la memoización por consistencia).
                _hydrationTask = null;
                return null;
            }

            var session = await _sessionLoader.LoadAsync(idUser);
            if (session == null)
            {
                // A diferencia del caso anterior, acá la cookie SÍ es válida — el null
                // vino de un fallo (probablemente transitorio: timeout, contención de
                // conexiones) al leer la base de datos. Si dejáramos _hydrationTask
                // memoizado en esta tarea fallida, todo el resto del circuito quedaría
                // "deslogueado" en memoria para siempre, aunque la cookie siga siendo
                // válida — eso es exactamente lo que alimentaba un ciclo Home ⇄
                // SelectBuilding ⇄ Login cuando la hidratación fallaba una sola vez.
                // Liberar la memoización permite que la siguiente llamada reintente.
                _logger.LogWarning("No se pudo reconstruir la sesión para el usuario {IdUser} (cookie válida, pero sin datos en BD) — se reintentará en la próxima llamada", idUser);
                _hydrationTask = null;
                return null;
            }

            _currentUser = session;
            return session;
        }

        // Mantenido por compatibilidad con los componentes (LeftMenu, HeaderMainLayout,
        // Home.razor.cs, Profile.razor) que la llaman en su primer render para forzar
        // la carga temprana de la sesión. Ya no hace falta esperar a JS interop, pero
        // sigue siendo útil como "asegurate de que ya está cargada".
        public async Task InitializeClientAsync()
        {
            await EnsureHydratedAsync();
        }

        public async Task MarkUserAsAuthenticated(UserSession session)
        {
            _currentUser = session;
            NotifyAuthenticationStateChanged(Task.FromResult(CreateAuthenticationState(session)));
        }

        public Task MarkUserAsLoggedOut()
        {
            _currentUser = null;
            // Sin esto, una llamada posterior a EnsureHydratedAsync() en el mismo
            // circuito devolvería la sesión vieja memoizada en _hydrationTask en vez de
            // volver a evaluar la cookie (que para entonces ya pudo haber cambiado).
            _hydrationTask = null;
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
            return Task.CompletedTask;
        }

        public async Task<UserSession?> GetCurrentUserAsync()
        {
            return await EnsureHydratedAsync();
        }

        private static AuthenticationState CreateAuthenticationState(UserSession session)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, session.IdUser.ToString()),
                new Claim(ClaimTypes.Name, session.FullName),
                new Claim(ClaimTypes.Email, session.Email),
                new Claim("SessionStart", session.SessionStart.ToString("O")),
                new Claim("SessionExpiry", session.SessionExpiry.ToString("O"))
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

    }
}