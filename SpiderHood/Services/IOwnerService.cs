using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IOwnerService
    {
        Task<Models.Owner> AddOwnerAsync(Owner newowner);

        Task<Models.Owner> UpdateOwnerAsync(Owner owner);

        Task DeleteOwnerAsync(Owner owner);

    }

    public class OwnerService : IOwnerService
    {
        public BDLayout ec = default!;
        private readonly AuthService _authService;

        public OwnerService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
        {
            ec = new BDLayout(contextFactory);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        public async Task<Models.Owner> AddOwnerAsync(Owner newowner)
        {
            try
            {
                var result = await ec.AddNewRecordAsync(newowner);
                var performedBy = (await _authService.GetCurrentUserAsync())?.Email ?? "system";
                await ec.StampAuditAsync(AuditableEntity.Owner, result.IdOwner, performedBy, isCreate: true);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar propietario: {ex.Message}");
                return new Models.Owner();
            }
        }

        public async Task<Models.Owner> UpdateOwnerAsync(Owner owner)
        {
            try
            {
                var result = await ec.UpdateRecordAsync(owner);
                var performedBy = (await _authService.GetCurrentUserAsync())?.Email ?? "system";
                await ec.StampAuditAsync(AuditableEntity.Owner, result.IdOwner, performedBy, isCreate: false);
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar propietario: {ex.Message}");
                return new Models.Owner();
            }
        }

        public async Task DeleteOwnerAsync(Owner owner)
        {
            try
            {
                await ec.DeleteRecordAsync(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar propietario: {ex.Message}");
            }
        }
    }
}