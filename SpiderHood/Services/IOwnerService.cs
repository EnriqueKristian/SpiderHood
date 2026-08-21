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

        public OwnerService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<Models.Owner> AddOwnerAsync(Owner newowner)
        {
            try
            {
                return await ec.AddNewRecordAsync(newowner);
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
                return await ec.UpdateRecordAsync(owner);
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