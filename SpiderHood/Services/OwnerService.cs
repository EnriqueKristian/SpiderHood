using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IOwnerService
    {
        Task<Models.Owner> AddOwnerAsync(Owner owner);

    }

    public class OwnerService : IOwnerService
    {
        public BDLayout ec = default!;
        public SpiderHoodContext Context { get; private set; }

        public OwnerService(SpiderHoodContext _context)
        {
            Context = _context ?? throw new ArgumentNullException(nameof(_context));
            ec = new BDLayout(Context);
        }

        public async Task<Models.Owner> AddOwnerAsync(Owner owner)
        {
            try
            {
                return await ec.AddNewRecordAsync(owner);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar propietario: {ex.Message}");
                return new Models.Owner();
            }
        }

    }
}