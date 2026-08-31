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
                        ApprovedAt = ub.ApprovedAt
                    }).ToList();

                return new UserSession
                {
                    IdUser = user.IdUser,
                    Email = user.Email,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Roles = buildings.Select(b => b.Role).Distinct().ToList(),
                    Buildings = buildings,
                    CurrentBuildingId = GetDefaultBuilding(buildings),
                    Role = buildings.Select(b => b.Role).Distinct().FirstOrDefault() ?? string.Empty,
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

        private static Guid GetDefaultBuilding(List<UserBuilding> buildings)
        {
            var approved = buildings.Where(b => b.IsApproved).ToList();
            if (approved.Count == 1)
                return approved[0].Building!.IdBuilding;

            return Guid.Empty;
        }
    }
}
