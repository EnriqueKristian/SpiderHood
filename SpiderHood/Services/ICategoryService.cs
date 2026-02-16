using SpiderHood.Data;

namespace SpiderHood.Services
{
    public interface ICategoryService
    {
        Task<List<Models.Category>> GetCategoriesAsync(Guid IdBulding);
        Task AddCategoryAsync(Models.Category newcategory);
        Task UpdateCategoryAsync(Models.Category newcategory);

        Task DeleteCategoryAsync(Models.Category category);

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
                return [];
            }
        }

        public async Task AddCategoryAsync(Models.Category newcategory)
        {
            try
            {
                await ec.AddNewRecordAsync(newcategory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear la categoria: {ex.Message}");
            }
        }

        public async Task UpdateCategoryAsync(Models.Category category)
        {
            try
            {
                await ec.UpdateRecordAsync(category);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar la categoria: {ex.Message}");
            }
        }

        public async Task DeleteCategoryAsync(Models.Category category)
        {
            try
            {
                await ec.DeleteRecordAsync(category);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar la categoria: {ex.Message}");
            }
        }
    }
}