using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Security.Cryptography;
using System.Text;

namespace SpiderHood.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> LoginAsync(LoginModel model);
        Task LogoutAsync();
        Task<UserSession?> GetCurrentUserAsync();
        Task<Guid?> GetCurrentUnitIdAsync();
        /*Task<UserSession?> GetStoredSessionAsync();*/
        /*UserSession? CurrentUser { get; }
        event Action? OnAuthStateChanged;*/

        // El rol solicitado NO viaja desde afuera: siempre se otorga como "Residente" --
        // ver el comentario en la implementación. El parámetro se mantiene para no romper
        // las pantallas existentes, pero se ignora.
        Task<bool> RequestBuildingAccess(Guid buildingId, string role);
        Task<AuthResult> RegisterSelfServiceAsync(RegisterModel model);
        Task SetCurrentBuilding(Guid? Idbuilding, string role);
        Task<bool> TryApplyDefaultBuildingAsync();
        Task<UserModel> GetUserProfileAsync(Guid userId);
        Task<AuthResult> UpdateProfileAsync(Guid userId, string firstName, string lastName, string phoneNumber);
        Task<AuthResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);
        Task<AuthResult> CreateUserAsync(string email, string firstName, string lastName, string phoneNumber, string password);
        Task<AuthResult> UpdateUserAdminAsync(Guid userId, string firstName, string lastName, string phoneNumber, bool isActive);
        Task<AuthResult> AdminResetPasswordAsync(Guid userId, string newPassword);
        Task<string?> GetSecurityStampAsync(Guid userId);
        void RevokeAllSessions(Guid userId);
    }

    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly CustomAuthenticationStateProvider _authStateProvider;
        private readonly ISessionRevocationService _sessionRevocation;

        private List<UserModel> _users = new();
        private List<UserBuildingAssociation> _userBuildings = new();

        // Hashing real (PBKDF2, vía Identity — ya referenciado en el proyecto).
        // Reemplaza el SHA-256 sin salt que había antes.
        private readonly PasswordHasher<UserModel> _passwordHasher = new();

        private const string DEFAULT_BUILDING_KEY = "defaultBuildingId";
        private readonly IJSRuntime _jsRuntime;

        public event Action? OnAuthStateChanged;
        private BDLayout Ec { get; set; }

        private readonly string _baseUrl;

        public AuthService(IDbContextFactory<SpiderHoodContext> contextFactory,
           ILogger<AuthService> logger,
           IEmailService emailService,
           CustomAuthenticationStateProvider authStateProvider,
           ISessionRevocationService sessionRevocation,
           IJSRuntime jsRuntime,
           IConfiguration configuration) // Added parameter to satisfy readonly field assignment
        {
            _logger = logger;
            _authStateProvider = authStateProvider;
            _sessionRevocation = sessionRevocation;
            _jsRuntime = jsRuntime; // Assign the non-nullable readonly field
            Ec = new BDLayout(contextFactory);
            _emailService = emailService;
            //InitializeSampleData();
            _configuration = configuration;
            _baseUrl = _configuration["BaseUrl"] ?? "https://localhost:7175";
        }

        // NOTA: se eliminó InitializeSampleData() — era código muerto (nunca se llamaba,
        // estaba comentado) que además usaba la firma vieja de HashPassword(string).

        // En AuthService.cs, actualizar estos métodos:
        public async Task<AuthResponse> LoginAsync(LoginModel model)
        {
            try
            {
                _logger.LogWarning($"🔵 AuthService.LoginAsync INICIADO - Email: {model.Email}");

                // 🔴 NORMALIZAR EMAIL: convertir a minúsculas para comparación
                var normalizedEmail = model.Email?.Trim().ToLowerInvariant();

                _users = await Ec.GetUsersByEmailAsync(normalizedEmail!);

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

                if (!await VerifyPasswordAsync(user, model.Password, user.PasswordHash))
                {
                    _logger.LogWarning($"❌ Contraseña incorrecta para: {model.Email}");
                    return AuthFail("Email o contraseña incorrectos");
                }

                _userBuildings = await Ec.GetUserBuildingAssociationAsync(user.IdUser);
                List<Building> builds = await Ec.GetAllBuildingByOwnerAsync(user.IdUser);
                List<BuildingConfiguration> configurations = await Ec.GetAllBuildingsConfigAsync(user.IdUser);

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
                        ApprovedAt = ub.ApprovedAt,
                        IdGroupUnit = ub.IdGroupUnit
                    }).ToList();

                await GrantSysAdminAccessToAllBuildingsAsync(buildings);
                var (defaultBuildingId, defaultRole) = ResolveDefaultBuildingAndRole(buildings);

                // Crear sesión
                var session = (new UserSession
                {
                    IdUser = user.IdUser,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Roles = buildings.Select(b => b.Role).Distinct().ToList(),
                    Buildings = buildings,
                    CurrentBuildingId = defaultBuildingId,
                    RememberMe = model.RememberMe,
                    SessionStart = DateTime.UtcNow,
                    SessionExpiry = DateTime.UtcNow.AddHours(8),
                    Role = defaultRole,
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

        // Resuelve la unidad (GroupUnit) del usuario actual para el edificio+rol con el
        // que está trabajando ahora mismo — lo usan Mis Recibos/Mis Deudas y Profile >
        // Finanzas para filtrar "mis" cuotas (Installment.IdGroupUnit) en vez de las de
        // todo el edificio. Null si el admin todavía no vinculó una unidad a esta
        // asociación usuario-edificio-rol (ver UserRoles.razor > "Unidad (Residente)").
        public async Task<Guid?> GetCurrentUnitIdAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null || user.CurrentBuildingId == Guid.Empty)
                return null;

            return user.Buildings
                .FirstOrDefault(b => b.Building?.IdBuilding == user.CurrentBuildingId && b.Role == user.Role)
                ?.IdGroupUnit;
        }

        public async Task SetCurrentBuilding(Guid? buildingId, string Role)
        {
            var user = await GetCurrentUserAsync();
            if (user == null) return;

            if (buildingId.HasValue)
            {
                var building = user.Buildings.FirstOrDefault(b => b.Building!.IdBuilding == buildingId.Value && b.Role == Role);
                if (building != null)
                {
                    //Marcar el Building como default

                    user.CurrentBuildingId = buildingId.Value;

                    // Un mismo edificio puede tener varias filas (una por rol) cuando el
                    // usuario tiene acceso con más de un rol — así que seleccionar edificio
                    // TAMBIÉN es seleccionar rol. Antes esto solo actualizaba
                    // CurrentBuildingId y dejaba Role intacto (el primero que trajo el
                    // login), así que elegir el 2do o 3er rol en /select-building no tenía
                    // efecto: el menú y el header seguían mostrando el rol original.
                    user.Role = Role;

                    await _authStateProvider.MarkUserAsAuthenticated(user);
                }
            }
        }

        // Se usa al arrancar el dashboard cuando la sesión no trae un edificio actual
        // resuelto (login con más de un edificio aprobado, o ninguno con exactamente
        // uno). Antes de mandar al usuario a /select-building, intenta aplicar el
        // edificio que haya guardado como preferencia (SetDefaultBuildingAsync) — así
        // alguien que ya eligió su edificio una vez no tiene que repetirlo en cada
        // ingreso. Sólo tiene efecto una vez conectado el circuito (usa localStorage
        // vía IJSRuntime); el llamador es responsable de no invocarlo durante el
        // prerender estático.
        //
        // OJO: sólo salta select-building cuando el edificio preferido no tiene
        // ambigüedad de rol (un único rol aprobado para ese edificio). Si el usuario
        // tiene, por ejemplo, Administrador Y Junta en el mismo edificio, esa es una
        // elección real que se le tiene que seguir preguntando cada vez — aunque
        // sepamos cuál usó la última vez (eso sólo sirve para preseleccionarlo en
        // /select-building, ver GetDefaultRoleAsync). De lo contrario el usuario queda
        // atrapado siempre en el mismo rol sin poder cambiarlo.
        public async Task<bool> TryApplyDefaultBuildingAsync()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                _logger.LogWarning("🏢 TryApplyDefaultBuildingAsync: sin usuario autenticado");
                return false;
            }

            if (user.CurrentBuildingId != Guid.Empty)
            {
                _logger.LogInformation("🏢 TryApplyDefaultBuildingAsync: ya había edificio resuelto ({BuildingId}), no hace falta aplicar preferencia", user.CurrentBuildingId);
                return false;
            }

            var preferredBuildingId = await GetDefaultBuildingAsync();
            if (!preferredBuildingId.HasValue)
            {
                _logger.LogInformation("🏢 TryApplyDefaultBuildingAsync: no hay edificio guardado como preferencia en localStorage para el usuario {UserId}", user.IdUser);
                return false;
            }

            var matches = user.Buildings
                .Where(b => b.IsApproved && b.Building?.IdBuilding == preferredBuildingId.Value)
                .ToList();

            if (matches.Count != 1)
            {
                // 0 -> el edificio guardado ya no es válido (perdió acceso, o cambió de
                //      estado); no rompemos el flujo, simplemente no aplica.
                // >1 -> hay más de un rol para ese edificio: es una elección real, no la
                //      resolvemos en silencio.
                _logger.LogInformation("🏢 TryApplyDefaultBuildingAsync: edificio preferido {BuildingId} tiene {Count} rol(es) aprobados para el usuario — {Reason}",
                    preferredBuildingId.Value, matches.Count, matches.Count == 0 ? "ya no aplica, se ignora" : "ambiguo, se pide elegir");
                return false;
            }

            _logger.LogInformation("🏢 TryApplyDefaultBuildingAsync: aplicando edificio {BuildingId} con rol {Role}", matches[0].Building!.IdBuilding, matches[0].Role);
            await SetCurrentBuilding(matches[0].Building!.IdBuilding, matches[0].Role);
            return true;
        }

        public async Task LogoutAsync()
        {
            await _authStateProvider.MarkUserAsLoggedOut();
            NotifyAuthStateChanged();
        }

        // "Sello" de la sesión: un hash del PasswordHash actual del usuario. Se guarda
        // como claim en la cookie al iniciar sesión (ver Login.razor) y se vuelve a
        // calcular acá en cada revalidación (Program.cs → OnValidatePrincipal) — si no
        // coinciden, la contraseña cambió después de emitida esa cookie, y se la
        // invalida. No expone el hash real como claim (viajaría en la cookie), sólo su
        // huella — y como PasswordHash ya incluye salt propio, dos usuarios nunca
        // comparten huella aunque coincida la contraseña.
        public async Task<string?> GetSecurityStampAsync(Guid userId)
        {
            try
            {
                var user = await Ec.GetUserByIdAsync(userId);
                return ComputeSecurityStamp(user.PasswordHash);
            }
            catch (EntityNotFoundException)
            {
                return null;
            }
        }

        private static string ComputeSecurityStamp(string passwordHash)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash));
            return Convert.ToHexString(bytes);
        }

        // Invalida toda cookie de este usuario emitida hasta ahora — ver
        // ISessionRevocationService. Lo llaman ChangePasswordAsync/AdminResetPasswordAsync
        // automáticamente, y Profile.razor cuando el usuario pide "cerrar sesión en todos
        // los dispositivos" explícitamente.
        public void RevokeAllSessions(Guid userId)
        {
            _sessionRevocation.RevokeAllSessions(userId);
        }

        public async Task<UserModel> AddNewUserAsync(UserModel user)
        {
            return await Ec.AddNewRecordAsync(user);
        }

        public async Task<UserModel> GetUserProfileAsync(Guid userId)
        {
            return await Ec.GetUserByIdAsync(userId);
        }

        public async Task<AuthResult> UpdateProfileAsync(Guid userId, string firstName, string lastName, string phoneNumber)
        {
            try
            {
                var user = await Ec.GetUserByIdAsync(userId);

                user.FirstName = firstName;
                user.LastName = lastName;
                user.PhoneNumber = phoneNumber;

                await Ec.UpdateRecordAsync(user);

                var session = await GetCurrentUserAsync();
                if (session != null)
                {
                    session.FullName = $"{firstName} {lastName}";
                    await _authStateProvider.MarkUserAsAuthenticated(session);
                    NotifyAuthStateChanged();
                }

                return new AuthResult { Success = true, Message = "Perfil actualizado exitosamente" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando el perfil del usuario {UserId}", userId);
                return new AuthResult { Success = false, Message = "No se pudo actualizar el perfil" };
            }
        }

        public async Task<AuthResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
        {
            try
            {
                var user = await Ec.GetUserByIdAsync(userId);

                if (!await VerifyPasswordAsync(user, currentPassword, user.PasswordHash))
                    return new AuthResult { Success = false, Message = "La contraseña actual es incorrecta" };

                var newHash = _passwordHasher.HashPassword(user, newPassword);
                await Ec.UpdateUserPasswordAsync(userId, newHash);

                // Invalida cualquier cookie ya emitida para este usuario (otro navegador
                // propio, o una robada) — sin esto, cambiar la contraseña no protegía nada
                // hasta que esa cookie expirara sola. Ver Program.cs → OnValidatePrincipal.
                _sessionRevocation.RevokeAllSessions(userId);

                return new AuthResult { Success = true, Message = "Contraseña actualizada exitosamente" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando la contraseña del usuario {UserId}", userId);
                return new AuthResult { Success = false, Message = "No se pudo cambiar la contraseña" };
            }
        }

        public async Task<AuthResult> CreateUserAsync(string email, string firstName, string lastName, string phoneNumber, string password)
        {
            try
            {
                var normalizedEmail = email.Trim().ToLowerInvariant();

                var existing = await Ec.GetUsersByEmailAsync(normalizedEmail);
                if (existing.Any(u => u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    return new AuthResult { Success = false, Message = "Ya existe un usuario con ese email" };
                }

                var user = new UserModel
                {
                    IdUser = Guid.NewGuid(),
                    Email = normalizedEmail,
                    FirstName = firstName,
                    LastName = lastName,
                    PhoneNumber = phoneNumber,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, password);

                await AddNewUserAsync(user);

                return new AuthResult { Success = true, Message = "Usuario creado exitosamente", User = user };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando usuario {Email}", email);
                return new AuthResult { Success = false, Message = "No se pudo crear el usuario" };
            }
        }

        public async Task<AuthResult> UpdateUserAdminAsync(Guid userId, string firstName, string lastName, string phoneNumber, bool isActive)
        {
            try
            {
                var user = await Ec.GetUserByIdAsync(userId);

                user.FirstName = firstName;
                user.LastName = lastName;
                user.PhoneNumber = phoneNumber;
                user.IsActive = isActive;

                await Ec.UpdateRecordAsync(user);

                return new AuthResult { Success = true, Message = "Usuario actualizado exitosamente" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando usuario {UserId}", userId);
                return new AuthResult { Success = false, Message = "No se pudo actualizar el usuario" };
            }
        }

        // A diferencia de ChangePasswordAsync, este método es para uso administrativo:
        // no exige conocer la contraseña actual (un admin restableciendo la contraseña
        // de otro usuario no la tiene).
        public async Task<AuthResult> AdminResetPasswordAsync(Guid userId, string newPassword)
        {
            try
            {
                var user = await Ec.GetUserByIdAsync(userId);
                var newHash = _passwordHasher.HashPassword(user, newPassword);
                await Ec.UpdateUserPasswordAsync(userId, newHash);

                // Mismo motivo que en ChangePasswordAsync: un admin reseteando la
                // contraseña de otro usuario (p.ej. porque sospecha que la cuenta está
                // comprometida) tiene que matar también cualquier sesión ya abierta.
                _sessionRevocation.RevokeAllSessions(userId);

                return new AuthResult { Success = true, Message = "Contraseña actualizada exitosamente" };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restableciendo la contraseña del usuario {UserId}", userId);
                return new AuthResult { Success = false, Message = "No se pudo restablecer la contraseña" };
            }
        }

        // -------------------------------------------
        //        UTILITARIOS
        // -------------------------------------------

        private static AuthResponse AuthFail(string message) =>
            new AuthResponse { Success = false, Message = message };

        private async Task<bool> VerifyPasswordAsync(UserModel user, string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash))
                return false;

            // Formato nuevo: PasswordHasher (PBKDF2 con salt por usuario).
            var result = _passwordHasher.VerifyHashedPassword(user, storedHash, password);
            if (result != PasswordVerificationResult.Failed)
            {
                // Si el hasher recomienda re-hashear (cambiaron los parámetros), lo actualizamos
                // y lo persistimos en la base de datos.
                if (result == PasswordVerificationResult.SuccessRehashNeeded)
                {
                    var newHash = _passwordHasher.HashPassword(user, password);
                    try
                    {
                        await Ec.UpdateUserPasswordAsync(user.IdUser, newHash);
                        user.PasswordHash = newHash;
                    }
                    catch (Exception ex)
                    {
                        // No bloqueamos el login si falla la migración del hash — el usuario
                        // ya se autenticó correctamente contra el hash existente.
                        _logger.LogError(ex, "No se pudo persistir el hash migrado para el usuario {IdUser}", user.IdUser);
                    }
                }
                return true;
            }

            // Compatibilidad temporal con hashes antiguos (SHA-256 sin salt).
            if (!VerifyLegacySha256Password(password, storedHash))
                return false;

            // Login válido con hash legado: migramos silenciosamente al formato nuevo.
            var migratedHash = _passwordHasher.HashPassword(user, password);
            try
            {
                await Ec.UpdateUserPasswordAsync(user.IdUser, migratedHash);
                user.PasswordHash = migratedHash;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo migrar el hash legado para el usuario {IdUser}", user.IdUser);
            }

            return true;
        }

        private static bool VerifyLegacySha256Password(string password, string storedHash)
        {
            using var sha256 = SHA256.Create();
            var computedHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));

            var a = Encoding.UTF8.GetBytes(computedHash);
            var b = Encoding.UTF8.GetBytes(storedHash);
            if (a.Length != b.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(a, b);
        }

        // SysAdmin nunca tiene que elegir con qué rol entra -- entra siempre como
        // SysAdmin, al primer edificio disponible (después puede cambiar de edificio
        // con el selector del header). Sin este atajo, alguien con SysAdmin Y además
        // Administrador/Junta sobre el mismo edificio (caso real: admin@spiderhood.com)
        // queda con la misma ambigüedad de rol que cualquier otro usuario y termina en
        // /select-building -- lo cual no tiene sentido para el superusuario.
        private static (Guid BuildingId, string Role) ResolveDefaultBuildingAndRole(List<UserBuilding> buildings)
        {
            var sysAdmin = buildings.FirstOrDefault(b => b.Role == "SysAdmin");
            if (sysAdmin != null)
                return (sysAdmin.Building!.IdBuilding, "SysAdmin");

            var approved = buildings.Where(b => b.IsApproved).ToList();
            if (approved.Count == 1)
                return (approved[0].Building!.IdBuilding, approved[0].Role);

            return (Guid.Empty, buildings.Select(b => b.Role).Distinct().FirstOrDefault() ?? string.Empty);
        }

        // SysAdmin es el administrador general del sistema: no debería depender de estar
        // vinculado (ni mucho menos aprobado) a un edificio puntual para poder entrar --
        // tiene privilegio sobre todos. Si ya tiene el rol SysAdmin en al menos un
        // edificio (así se identifica hoy, vía UserBuildingAssociation), se le completan
        // como aprobados el resto de los edificios del sistema y se fuerza IsApproved en
        // los que ya tenía, para que nunca termine atrapado en "sin edificios" o en la
        // pantalla de aprobación pendiente que ven los demás roles.
        private async Task GrantSysAdminAccessToAllBuildingsAsync(List<UserBuilding> buildings)
        {
            if (!buildings.Any(b => b.Role == "SysAdmin"))
                return;

            foreach (var b in buildings.Where(b => b.Role == "SysAdmin"))
            {
                b.IsApproved = true;
            }

            var todosLosEdificios = await Ec.GetAllBuildingsPublicAsync();
            var yaCubiertos = buildings
                .Where(b => b.Role == "SysAdmin")
                .Select(b => b.Building?.IdBuilding)
                .ToHashSet();

            foreach (var edificio in todosLosEdificios.Where(e => !yaCubiertos.Contains(e.IdBuilding)))
            {
                buildings.Add(new UserBuilding
                {
                    Building = edificio,
                    Role = "SysAdmin",
                    IsApproved = true
                });
            }
        }

        private void NotifyAuthStateChanged() =>
            OnAuthStateChanged?.Invoke();

        // Un usuario YA logueado pide acceso a OTRO edificio (o a un rol que todavía no
        // tiene en éste). El rol pedido siempre es "Residente": Administrador/Junta sólo
        // los otorga un admin desde /Settings/UserRoles, para que nadie se autoasigne un
        // rol de poder llenando un formulario. @role se ignora a propósito -- se mantiene
        // en la firma para no romper las pantallas que ya lo llaman.
        public async Task<bool> RequestBuildingAccess(Guid buildingId, string role)
        {
            var user = await GetCurrentUserAsync();
            if (user == null || buildingId == Guid.Empty)
                return false;

            var existing = await Ec.GetUserBuildingAssociationAsync(user.IdUser);
            if (existing.Any(a => a.IdBuilding == buildingId && a.Role == "Residente"))
                return false; // ya tiene una solicitud (o membresía) de Residente en ese edificio

            return await CreatePendingAssociationAsync(user.IdUser, buildingId, "Residente");
        }

        // Alta pública de cuenta (sin invitación): el visitante elige un edificio y queda
        // como Residente pendiente de aprobación -- ver AcceptInvitationAsync/Ec.AcceptInvitationAsync
        // (INS_UserBuildingAssociation), reutilizado acá con IsApproved=false. Igual que en
        // RequestBuildingAccess, el rol nunca lo elige quien se registra: siempre Residente.
        public async Task<AuthResult> RegisterSelfServiceAsync(RegisterModel model)
        {
            try
            {
                var normalizedEmail = model.Email.Trim().ToLowerInvariant();

                var existing = await Ec.GetUsersByEmailAsync(normalizedEmail);
                if (existing.Any(u => u.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    return new AuthResult { Success = false, Message = "Ya existe una cuenta con ese email" };
                }

                var user = new UserModel
                {
                    IdUser = Guid.NewGuid(),
                    Email = normalizedEmail,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

                await AddNewUserAsync(user);
                await CreatePendingAssociationAsync(user.IdUser, model.BuildingId, "Residente");

                return new AuthResult
                {
                    Success = true,
                    Message = "Registro exitoso. Tu solicitud quedó pendiente de aprobación por el administrador del edificio.",
                    User = user
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RegisterSelfServiceAsync para email: {Email}", model.Email);
                return new AuthResult { Success = false, Message = "No se pudo completar el registro" };
            }
        }

        private async Task<bool> CreatePendingAssociationAsync(Guid idUser, Guid idBuilding, string role)
        {
            var association = new UserBuildingAssociation
            {
                IdUser = idUser,
                IdBuilding = idBuilding,
                Role = role,
                IsApproved = false,
                RequestedAt = DateTime.Now
            };
            return await Ec.AcceptInvitationAsync(association);
        }

        // NOTA: se eliminó DebugPrintUsers() — no se llamaba desde ningún lado y
        // logueaba los password hashes de todos los usuarios.

        /// Guarda el edificio por defecto del usuario actual
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

        /// Obtiene el edificio por defecto del usuario actual
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

        /// Guarda el rol preferido para el edificio por defecto (un mismo edificio puede
        /// tener más de un rol aprobado para el usuario — esto es lo que permite
        /// preseleccionar en /select-building exactamente la misma combinación que se usó
        /// la última vez, en vez de una cualquiera de las disponibles para ese edificio).
        public async Task SetDefaultRoleAsync(string role)
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return;

                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", $"defaultRole_{user.IdUser}", role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error guardando rol por defecto");
            }
        }

        /// Obtiene el rol preferido guardado junto con el edificio por defecto.
        public async Task<string?> GetDefaultRoleAsync()
        {
            try
            {
                var user = await GetCurrentUserAsync();
                if (user == null) return null;

                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", $"defaultRole_{user.IdUser}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error obteniendo rol por defecto");
                return null;
            }
        }

        /// Limpia las preferencias del usuario (edificio por defecto, etc)
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

        // NOTA: se eliminaron ClearUserSessionAsync() y GetTokenAsync() — leían/borraban
        // "currentUser"/"authToken"/"refreshToken" de localStorage, pero nada en la app
        // escribía esas claves (MarkUserAsAuthenticated nunca las usó); eran restos de
        // un diseño previo basado en tokens. La sesión real ahora vive en la cookie de
        // autenticación (HttpContext.SignInAsync/SignOutAsync en Login.razor/Logout.razor),
        // no en localStorage — localStorage queda sólo para preferencias (ver
        // SetDefaultBuildingAsync/GetDefaultBuildingAsync/ClearUserPreferencesAsync arriba).

        public async Task<InvitationModel> GetByCodeAsync(string code)
        {
            return await Ec.GetInvitationByCodeAsync(code);
        }

        public async Task<AuthResult> RegisterWithInvitationAsync(
    RegisterWithInvitationModel model,
    InvitationModel invitation)
        {
            try
            {
                // Validar que la invitación sigue siendo válida
                if (invitation.ExpirationDate < DateTime.Now)
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "La invitación ha expirado"
                    };
                }

                if (invitation.Status != "Pending")
                {
                    return new AuthResult
                    {
                        Success = false,
                        Message = "La invitación ya ha sido procesada"
                    };
                }

                // Crear el usuario
                var user = new IdentityUser
                {
                    UserName = invitation.Email,
                    Email = invitation.Email,
                    PhoneNumber = model.PhoneNumber,
                    EmailConfirmed = true // El email está verificado por la invitación
                };

                // Crear el usuario en la base de datos
                UserModel _user = new UserModel();

                _user.IdUser = Guid.Parse(user.Id);
                _user.Email = invitation.Email;
                _user.PhoneNumber = model.PhoneNumber!;
                _user.FirstName = model.FirstName;
                _user.LastName = model.LastName;
                _user.PasswordHash = _passwordHasher.HashPassword(_user, model.Password);

                //EmailConfirmed = true // El email está verificado por la invitación

                var createResult = await AddNewUserAsync(_user);

                await AcceptInvitationAsync(invitation, _user, model);

                // Si no requiere aprobación, iniciar sesión automáticamente
                if (!invitation.RequiresApproval)
                {
                    LoginModel login = new LoginModel();
                    login.Email = invitation.Email;
                    login.Password = model.Password;
                    login.RememberMe = false;
                    await LoginAsync(login);
                }

                // Enviar correo de confirmación
                await SendWelcomeEmailAsync(user, model.FirstName, invitation.RequiresApproval);

                return new AuthResult
                {
                    Success = true,
                    Message = invitation.RequiresApproval
                        ? "Registro exitoso. Esperando aprobación del administrador."
                        : "Registro exitoso. ¡Bienvenido!",
                    User = new UserModel
                    {
                        IdUser = Guid.Parse(user.Id),
                        //UserName = user.UserName,
                        Email = user.Email,
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        PhoneNumber = model.PhoneNumber!,
                        //RequiresApproval = invitation.RequiresApproval
                    },
                    RequiresApproval = invitation.RequiresApproval
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en RegisterWithInvitationAsync para email: {Email}", model.Email);
                return new AuthResult
                {
                    Success = false,
                    Message = "Error interno al procesar el registro"
                };
            }
        }

        private async Task<bool> AcceptInvitationAsync(InvitationModel invitation, UserModel User, RegisterWithInvitationModel model)
        {
            // Asociar usuario con el edificio y departamento
            UserBuildingAssociation _association = new UserBuildingAssociation();
            _association.IdUser = User.IdUser;
            _association.IdBuilding = invitation.IdBuilding;
            _association.Role = await ResolveValidRoleNameAsync(invitation.Role);
            _association.IsApproved = true;
            _association.RequestedAt = DateTime.Now;

            return await Ec.AcceptInvitationAsync(_association);

        }

        // InvitationModel.Role es texto libre (no hay FK a Role al crear la invitación,
        // y no existe ningún flujo en la app que las genere todavía — sólo se insertan a
        // mano), así que nada valida que coincida con un rol real antes de llegar acá.
        // Si no matchea, UserBuildingAssociation.Role queda con un valor que el resto de
        // la app (menú, permisos) no reconoce: el usuario se registra pero no ve nada
        // (caso real detectado: una invitación con Role='Visitor', que no es ninguno de
        // los roles del sistema). Se valida en este único lugar que efectivamente
        // persiste el valor, en vez de confiar ciegamente en el texto de la invitación.
        private async Task<string> ResolveValidRoleNameAsync(string invitationRole)
        {
            var roles = await Ec.GetAllRolesAsync();
            var match = roles.FirstOrDefault(r =>
                string.Equals(r.RoleName, invitationRole, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                return match.RoleName;
            }

            _logger.LogWarning(
                "Invitación con rol '{InvitationRole}' no coincide con ningún rol del sistema; se usa 'Residente' por default.",
                invitationRole);
            return "Residente";
        }

        private async Task SendWelcomeEmailAsync(IdentityUser user, string firstName, bool requiresApproval)
        {
            try
            {
                var subject = requiresApproval
                    ? "Solicitud de registro recibida"
                    : "Bienvenido a SpiderHood";

                var body = requiresApproval
                    ? $@"
                    <h2>Hola {firstName},</h2>
                    <p>Hemos recibido tu solicitud de registro en SpiderHood.</p>
                    <p>Tu cuenta está pendiente de aprobación por un administrador.</p>
                    <p>Recibirás un correo electrónico cuando tu cuenta sea activada.</p>
                    <br>
                    <p>Saludos,<br>El equipo de SpiderHood</p>"
                    : $@"
                    <h2>¡Bienvenido a SpiderHood, {firstName}!</h2>
                    <p>Tu cuenta ha sido creada exitosamente.</p>
                    <p>Ya puedes acceder al sistema con tu correo electrónico y contraseña.</p>
                    <br>
                    <p><a href='{_baseUrl}/dashboard'>Ir al Dashboard</a></p>
                    <br>
                    <p>Saludos,<br>El equipo de SpiderHood</p>";

                await _emailService.SendEmailAsync(user.Email!, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enviando email de bienvenida a: {Email}", user.Email);
            }
        }
    }
}