using Microsoft.Data.SqlClient;
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

        Task<OperationResult> DeleteCategoryAsync(Models.Category category);

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

        // Category (a diferencia de Parameter) sí admite borrado real -- ver
        // Docs/Design-Defaults-Sistema-Mixto.md §6. Expense/Exoneration/BudgetDetail/
        // CalendarItem tienen ahora un FK real hacia Category.IdCategory
        // (Database/Scripts/2026-09-02_24_Category_RealFK.sql), así que borrar una
        // categoría todavía en uso falla en BD (SQL error 547) en vez de dejar data
        // huérfana -- antes esto se tragaba en silencio (catch sin rethrow) y la UI
        // seguía como si el borrado hubiera funcionado.
        public async Task<OperationResult> DeleteCategoryAsync(Models.Category category)
        {
            try
            {
                await ec.DeleteRecordAsync(category);
                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                if (IsForeignKeyViolation(ex))
                {
                    return OperationResult.Failure(
                        "No se puede eliminar: la categoría está en uso (Gastos, Presupuesto, Exoneraciones o Calendario).");
                }

                Console.WriteLine($"Error al eliminar la categoria: {ex.Message}");
                return OperationResult.Failure($"No se pudo eliminar la categoría: {DescribeError(ex)}");
            }
        }

        // BDLayout envuelve la excepción real (la de SQL Server) en una
        // RepositoryException genérica -- mismo patrón que IBuildingService.DescribeError.
        private static string DescribeError(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost.Message;
        }

        private static bool IsForeignKeyViolation(Exception ex)
        {
            var innermost = ex;
            while (innermost.InnerException != null)
                innermost = innermost.InnerException;
            return innermost is SqlException sqlEx && sqlEx.Number == 547;
        }
    }
}