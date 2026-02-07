using SpiderHood.Data;

namespace SpiderHood.Services
{
    public interface ICategoryService
    {
        Task<List<Models.Category>> GetCategoriesAsync(Guid IdBulding);

    }

    public class CategoryService : ICategoryService
    {
        public BDLayout ec = default!;
        public SpiderHoodContext Context { get; private set; }

        public CategoryService(SpiderHoodContext _context)
        {
            Context = _context ?? throw new ArgumentNullException(nameof(_context));
            ec = new BDLayout(Context);
        }

        public async Task<List<Models.Category>> GetCategoriesAsync(Guid IdBulding)
        {
            try
            {
                // En una app real, esto vendría de una API
                return await ec.GetCategoriesAsync(IdBulding);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener las categorias: {ex.Message}");
                return new List<Models.Category>();
            }
        }

    }
}