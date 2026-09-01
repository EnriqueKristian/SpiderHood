using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

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
        private readonly AuthService _authService;

        public CategoryService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
        {
            ec = new BDLayout(contextFactory);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
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
                await ec.StampAuditAsync(AuditableEntity.Category, newcategory.IdCategory, await GetPerformedByAsync(), isCreate: true);
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
                await ec.StampAuditAsync(AuditableEntity.Category, category.IdCategory, await GetPerformedByAsync(), isCreate: false);
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