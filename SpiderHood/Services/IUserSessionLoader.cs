using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Reconstruye un UserSession completo (edificios, roles) a partir de un IdUser.
    //
    // Antes, esa información sólo existía una vez, al momento del login, y de ahí en
    // adelante se leía de localStorage. Esto la reconstruye desde la base de datos
    // (fuente de verdad) cada vez que arranca un circuito nuevo — recarga de página,
    // reconexión — a partir del IdUser que trae la cookie de autenticación. Es lo que
    // permite que CustomAuthenticationStateProvider deje de depender de localStorage.
    public interface IUserSessionLoader
    {
        Task<UserSession?> LoadAsync(Guid idUser);
    }

    public class UserSessionLoader : IUserSessionLoader
    {
        private readonly BDLayout _ec;
        private readonly ILogger<UserSessionLoader> _logger;

        public UserSessionLoader(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<UserSessionLoader> logger)
        {
            _ec = new BDLayout(contextFactory);
            _logger = logger;
        }

        public async Task<UserSession?> LoadAsync(Guid idUser)
        {
            try
            {
                var user = await _ec.GetUserByIdAsync(idUser);
                if (!user.IsActive)
                    return null;

                var userBuildings = await _ec.GetUserBuildingAssociationAsync(user.IdUser);
                var builds = await _ec.GetAllBuildingByOwnerAsync(user.IdUser);
                var configurations = await _ec.GetAllBuildingsConfigAsync(user.IdUser);

                foreach (var item in builds)
                {
                    item.Configuration = configurations.FirstOrDefault(c => c.IdBuilding == item.IdBuilding)!;
                }

                var buildings = userBuildings
                    .Where(ub => ub.IdUser == user.IdUser)
                    .Select(ub => new UserBuilding
                    {
                        Building = builds.FirstOrDefault(b => b.IdBuilding == ub.IdBuilding),
                        Role = ub.Role,
                        IsApproved = ub.IsApproved,
                        ApprovedAt = ub.ApprovedAt,
                        IdGroupUnit = ub.IdGroupUnit
                    }).ToList();

                // Ver AuthService.LoginAsync para el detalle de por qué se chequea también
                // esto -- SysAdmin se puede reconocer sin ninguna fila en
                // UserBuildingAssociation, vía el rol global en UserRole.
                var rolGlobal = await _ec.GetRoleByUserIdAsync(user.IdUser);
                var esSysAdminGlobal = rolGlobal?.RoleName == "SysAdmin";

                await GrantSysAdminAccessToAllBuildingsAsync(buildings, esSysAdminGlobal);
                var (defaultBuildingId, defaultRole) = ResolveDefaultBuildingAndRole(buildings, esSysAdminGlobal);

                return new UserSession
                {
                    IdUser = user.IdUser,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Roles = buildings.Select(b => b.Role).Distinct().ToList(),
                    Buildings = buildings,
                    CurrentBuildingId = defaultBuildingId,
                    Role = defaultRole,
                    SessionStart = DateTime.UtcNow,
                    SessionExpiry = DateTime.UtcNow.AddHours(8),
                };
            }
            catch (EntityNotFoundException)
            {
                // El usuario de la cookie ya no existe (fue borrado) — tratar como anónimo.
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconstruyendo la sesión del usuario {IdUser}", idUser);
                return null;
            }
        }

        // SysAdmin nunca tiene que elegir con qué rol entra -- ver el mismo método en
        // AuthService.LoginAsync para el detalle; se repite acá por la misma razón que
        // GrantSysAdminAccessToAllBuildingsAsync (este método corre en cada circuito
        // nuevo, no sólo en el login).
        private static (Guid BuildingId, string Role) ResolveDefaultBuildingAndRole(List<UserBuilding> buildings, bool esSysAdminGlobal)
        {
            var sysAdmin = buildings.FirstOrDefault(b => b.Role == "SysAdmin");
            if (sysAdmin != null)
                return (sysAdmin.Building!.IdBuilding, "SysAdmin");

            if (esSysAdminGlobal && buildings.Count > 0)
                return (buildings[0].Building!.IdBuilding, "SysAdmin");

            var approved = buildings.Where(b => b.IsApproved).ToList();
            if (approved.Count == 1)
                return (approved[0].Building!.IdBuilding, approved[0].Role);

            return (Guid.Empty, buildings.Select(b => b.Role).Distinct().FirstOrDefault() ?? string.Empty);
        }

        // Ver el mismo método en AuthService.LoginAsync -- se repite acá porque este
        // método reconstruye la sesión en cada circuito nuevo (recarga, reconexión), no
        // sólo en el login inicial.
        private async Task GrantSysAdminAccessToAllBuildingsAsync(List<UserBuilding> buildings, bool esSysAdminGlobal)
        {
            if (!esSysAdminGlobal && !buildings.Any(b => b.Role == "SysAdmin"))
                return;

            foreach (var b in buildings.Where(b => b.Role == "SysAdmin"))
            {
                b.IsApproved = true;
            }

            var todosLosEdificios = await _ec.GetAllBuildingsPublicAsync();
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
    }
}
