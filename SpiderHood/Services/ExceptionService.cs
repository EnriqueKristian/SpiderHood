using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IExceptionService
    {
        // Operaciones CRUD básicas
        Task<InstallmentException> GetByIdAsync(Guid id);
        Task<List<InstallmentException>> GetAllAsync();
        Task<InstallmentException> CreateAsync(InstallmentException exception);
        Task<InstallmentException> UpdateAsync(InstallmentException exception);
        Task<bool> DeleteAsync(Guid id);

        // Consultas específicas
        Task<List<InstallmentException>> GetByBudgetAsync(Guid budgetId);
        Task<List<InstallmentException>> GetByBudgetAndUnitAsync(Guid budgetId, Guid unitId);
        Task<List<InstallmentException>> GetByUnitAsync(Guid unitId);
        Task<List<InstallmentException>> GetByCategoryAsync(Guid categoryId);
        Task<List<InstallmentException>> GetActiveExceptionsAsync(Guid budgetId);

        // Validaciones y cálculos
        Task<bool> HasExceptionAsync(Guid budgetId, Guid unitId, Guid categoryId);
        Task<decimal> GetExclusionPercentageAsync(Guid budgetId, Guid unitId, Guid categoryId);
        Task<List<Guid>> GetExcludedCategoriesAsync(Guid budgetId, Guid unitId);
        Task<Dictionary<Guid, decimal>> GetExclusionSummaryAsync(Guid budgetId);

        // Operaciones por lote
        Task<int> CreateBatchAsync(List<InstallmentException> exceptions);
        Task<int> UpdateBatchAsync(List<InstallmentException> exceptions);
        Task<int> DeleteByBudgetAsync(Guid budgetId);

        // Cálculos financieros
        Task<decimal> CalculateExcludedAmountAsync(Guid budgetId, Guid unitId);
        Task<decimal> CalculateRedistributionAmountAsync(Guid budgetId, Guid categoryId);
        Task<Dictionary<Guid, decimal>> CalculateUnitSavingsAsync(Guid budgetId);
    }

    public class ExceptionService : IExceptionService
    {
        private BDLayout Ec { get; set; }

        private readonly ILogger<ExceptionService> _logger;


        public ExceptionService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            ILogger<ExceptionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Ec = new BDLayout(contextFactory);
        }

        public async Task<List<Exoneration>> GetExceptionsByBuildingAsync(Guid idBuilding)
        {
            return await Ec.GetExonerationsByBuildingAsync(idBuilding);
        }

        // CRUD Operations
        public async Task<InstallmentException> GetByIdAsync(Guid id)
        {
            /*try
            {
                return await Context.InstallmentExceptions
                    .Include(e => e.Budget)
                    .Include(e => e.Category)
                    .Include(e => e.GroupUnit)
                    .FirstOrDefaultAsync(e => e.IdException == id)
                    ?? throw new KeyNotFoundException($"Excepción con ID {id} no encontrada");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepción por ID: {Id}", id);
                throw;
            }*/
            return new InstallmentException();
        }

        public async Task<List<InstallmentException>> GetAllAsync()
        {
            /*try
            {
                return await context.InstallmentExceptions
                    .Include(e => e.Budget)
                    .Include(e => e.Category)
                    .Include(e => e.GroupUnit)
                    .Where(e => e.IsActive)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener todas las excepciones");
                throw;
            }*/
            return [];
        }

        public async Task<InstallmentException> CreateAsync(InstallmentException exception)
        {
            /*try
            {
                // Validar que no exista una excepción duplicada
                var exists = await _context.InstallmentExceptions
                    .AnyAsync(e => e.IdBudget == exception.IdBudget &&
                                   e.IdGroupUnit == exception.IdGroupUnit &&
                                   e.IdCategory == exception.IdCategory &&
                                   e.IsActive);

                if (exists)
                {
                    throw new InvalidOperationException("Ya existe una excepción activa para esta combinación de presupuesto, departamento y categoría");
                }

                // Validar que el presupuesto existe
                var budgetExists = await _context.BudgetHeaders
                    .AnyAsync(b => b.IdBudget == exception.IdBudget);
                if (!budgetExists)
                {
                    throw new KeyNotFoundException($"Presupuesto con ID {exception.IdBudget} no encontrado");
                }

                // Validar que la categoría existe
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == exception.IdCategory);
                if (!categoryExists)
                {
                    throw new KeyNotFoundException($"Categoría con ID {exception.IdCategory} no encontrada");
                }

                // Asignar fechas
                exception.IdException = Guid.NewGuid();
                exception.CreatedAt = DateTime.UtcNow;
                exception.UpdatedAt = DateTime.UtcNow;

                // Guardar en la base de datos
                await _context.InstallmentExceptions.AddAsync(exception);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Excepción creada exitosamente: {Id}", exception.IdException);
                return exception;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear excepción");
                throw;
            }*/
            return new InstallmentException();
        }

        public async Task<InstallmentException> UpdateAsync(InstallmentException exception)
        {
            /*try
            {
                var existing = await GetByIdAsync(exception.IdException);

                // Actualizar propiedades
                existing.Description = exception.Description;
                existing.PercentageExcluded = exception.PercentageExcluded;
                existing.IsActive = exception.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;
                existing.UpdatedBy = exception.UpdatedBy;

                _context.InstallmentExceptions.Update(existing);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Excepción actualizada exitosamente: {Id}", exception.IdException);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar excepción: {Id}", exception.IdException);
                throw;
            }*/
            return new InstallmentException();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            /*try
            {
                var exception = await GetByIdAsync(id);
                _context.InstallmentExceptions.Remove(exception);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Excepción eliminada exitosamente: {Id}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar excepción: {Id}", id);
                throw;
            }*/
            return false;
        }

        // Consultas específicas
        public async Task<List<InstallmentException>> GetByBudgetAsync(Guid budgetId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Include(e => e.Category)
                    .Include(e => e.GroupUnit)
                    .Where(e => e.IdBudget == budgetId && e.IsActive)
                    .OrderBy(e => e.GroupUnit.UnitName)
                    .ThenBy(e => e.Category.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepciones por presupuesto: {BudgetId}", budgetId);
                throw;
            }*/
            return [];
        }

        public async Task<List<InstallmentException>> GetByBudgetAndUnitAsync(Guid budgetId, Guid unitId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Include(e => e.Category)
                    .Where(e => e.IdBudget == budgetId &&
                                e.IdGroupUnit == unitId &&
                                e.IsActive)
                    .OrderBy(e => e.Category.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepciones por presupuesto y unidad: {BudgetId}, {UnitId}", budgetId, unitId);
                throw;
            }*/
            return [];
        }

        public async Task<List<InstallmentException>> GetByUnitAsync(Guid unitId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Include(e => e.Budget)
                    .Include(e => e.Category)
                    .Where(e => e.IdGroupUnit == unitId && e.IsActive)
                    .OrderByDescending(e => e.Budget.BudgetDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepciones por unidad: {UnitId}", unitId);
                throw;
            }*/
            return [];
        }

        public async Task<List<InstallmentException>> GetByCategoryAsync(Guid categoryId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Include(e => e.Budget)
                    .Include(e => e.GroupUnit)
                    .Where(e => e.IdCategory == categoryId && e.IsActive)
                    .OrderByDescending(e => e.Budget.BudgetDate)
                    .ThenBy(e => e.GroupUnit.UnitName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepciones por categoría: {CategoryId}", categoryId);
                throw;
            }*/
            return [];
        }

        public async Task<List<InstallmentException>> GetActiveExceptionsAsync(Guid budgetId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Include(e => e.Category)
                    .Include(e => e.GroupUnit)
                    .Where(e => e.IdBudget == budgetId && e.IsActive)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener excepciones activas: {BudgetId}", budgetId);
                throw;
            }*/
            return [];
        }

        // Validaciones y cálculos
        public async Task<bool> HasExceptionAsync(Guid budgetId, Guid unitId, Guid categoryId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .AnyAsync(e => e.IdBudget == budgetId &&
                                   e.IdGroupUnit == unitId &&
                                   e.IdCategory == categoryId &&
                                   e.IsActive &&
                                   e.PercentageExcluded > 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al verificar existencia de excepción");
                throw;
            }*/
            return false;
        }

        public async Task<decimal> GetExclusionPercentageAsync(Guid budgetId, Guid unitId, Guid categoryId)
        {
            /*try
            {
                var exception = await _context.InstallmentExceptions
                    .FirstOrDefaultAsync(e => e.IdBudget == budgetId &&
                                              e.IdGroupUnit == unitId &&
                                              e.IdCategory == categoryId &&
                                              e.IsActive);

                return exception?.PercentageExcluded ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener porcentaje de exclusión");
                throw;
            }*/
            return 0;
        }

        public async Task<List<Guid>> GetExcludedCategoriesAsync(Guid budgetId, Guid unitId)
        {
            /*try
            {
                return await _context.InstallmentExceptions
                    .Where(e => e.IdBudget == budgetId &&
                                e.IdGroupUnit == unitId &&
                                e.IsActive &&
                                e.PercentageExcluded > 0)
                    .Select(e => e.IdCategory)
                    .Distinct()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías excluidas");
                throw;
            }*/
            return [];
        }

        public async Task<Dictionary<Guid, decimal>> GetExclusionSummaryAsync(Guid budgetId)
        {
            /*try
            {
                var summary = await _context.InstallmentExceptions
                    .Where(e => e.IdBudget == budgetId && e.IsActive)
                    .GroupBy(e => e.IdCategory)
                    .Select(g => new
                    {
                        CategoryId = g.Key,
                        TotalExcluded = g.Sum(e => e.PercentageExcluded),
                        UnitCount = g.Count()
                    })
                    .ToListAsync();

                return summary.ToDictionary(
                    x => x.CategoryId,
                    x => x.TotalExcluded / x.UnitCount // Promedio de exclusión por categoría
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener resumen de exclusiones");
                throw;
            }*/
            return [];
        }

        // Operaciones por lote
        public async Task<int> CreateBatchAsync(List<InstallmentException> exceptions)
        {
            /*try
            {
                // Validar duplicados antes de insertar
                var budgetId = exceptions.First().IdBudget;
                var existingExceptions = await GetByBudgetAsync(budgetId);

                var validExceptions = exceptions.Where(e =>
                    !existingExceptions.Any(ex =>
                        ex.IdGroupUnit == e.IdGroupUnit &&
                        ex.IdCategory == e.IdCategory))
                    .ToList();

                foreach (var exception in validExceptions)
                {
                    exception.IdException = Guid.NewGuid();
                    exception.CreatedAt = DateTime.UtcNow;
                    exception.UpdatedAt = DateTime.UtcNow;
                }

                await _context.InstallmentExceptions.AddRangeAsync(validExceptions);
                var result = await _context.SaveChangesAsync();

                _logger.LogInformation("Creadas {Count} excepciones en lote", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear excepciones en lote");
                throw;
            }*/
            return 0;
        }

        public async Task<int> UpdateBatchAsync(List<InstallmentException> exceptions)
        {
            /*try
            {
                var updatedCount = 0;
                foreach (var exception in exceptions)
                {
                    var existing = await _context.InstallmentExceptions
                        .FirstOrDefaultAsync(e => e.IdException == exception.IdException);

                    if (existing != null)
                    {
                        existing.Description = exception.Description;
                        existing.PercentageExcluded = exception.PercentageExcluded;
                        existing.IsActive = exception.IsActive;
                        existing.UpdatedAt = DateTime.UtcNow;
                        existing.UpdatedBy = exception.UpdatedBy;

                        updatedCount++;
                    }
                }

                if (updatedCount > 0)
                {
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Actualizadas {Count} excepciones en lote", updatedCount);
                return updatedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar excepciones en lote");
                throw;
            }*/
            return 0;
        }

        public async Task<int> DeleteByBudgetAsync(Guid budgetId)
        {
            /*try
            {
                var exceptions = await GetByBudgetAsync(budgetId);
                _context.InstallmentExceptions.RemoveRange(exceptions);
                var result = await _context.SaveChangesAsync();

                _logger.LogInformation("Eliminadas {Count} excepciones del presupuesto {BudgetId}", result, budgetId);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar excepciones por presupuesto");
                throw;
            }*/
            return 0;
        }

        // Cálculos financieros
        public async Task<decimal> CalculateExcludedAmountAsync(Guid budgetId, Guid unitId)
        {
            /*try
            {
                var exceptions = await GetByBudgetAndUnitAsync(budgetId, unitId);
                if (!exceptions.Any()) return 0;

                // Obtener el presupuesto
                var budget = await _budgetService.GetByIdAsync(budgetId);
                var budgetDetails = budget?.Details?.Where(d => !d.IsHeader).ToList() ?? new List<BudgetDetail>();

                decimal totalExcluded = 0;

                foreach (var exception in exceptions)
                {
                    var categoryAmount = budgetDetails
                        .Where(d => d.IdCategory == exception.IdCategory)
                        .Sum(d => d.MonthlyAmount);

                    var unitArea = await GetUnitAreaAsync(unitId);
                    var totalArea = await GetTotalAreaAsync(budgetId);
                    var areaPercentage = totalArea > 0 ? unitArea / totalArea : 0;

                    var normalShare = categoryAmount * areaPercentage;
                    var excludedAmount = normalShare * (exception.PercentageExcluded / 100);

                    totalExcluded += excludedAmount;
                }

                return Math.Round(totalExcluded, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular monto excluido");
                throw;
            }*/
            return 0;
        }

        public async Task<decimal> CalculateRedistributionAmountAsync(Guid budgetId, Guid categoryId)
        {
            /*try
            {
                var exceptions = await _context.InstallmentExceptions
                    .Where(e => e.IdBudget == budgetId &&
                                e.IdCategory == categoryId &&
                                e.IsActive)
                    .ToListAsync();

                if (!exceptions.Any()) return 0;

                // Obtener monto total de la categoría
                var budget = await _budgetService.GetByIdAsync(budgetId);
                var categoryAmount = budget?.Details?
                    .Where(d => d.IdCategory == categoryId && !d.IsHeader)
                    .Sum(d => d.MonthlyAmount) ?? 0;

                decimal totalExcluded = 0;

                foreach (var exception in exceptions)
                {
                    var unitArea = await GetUnitAreaAsync(exception.IdGroupUnit);
                    var totalArea = await GetTotalAreaAsync(budgetId);
                    var areaPercentage = totalArea > 0 ? unitArea / totalArea : 0;

                    var normalShare = categoryAmount * areaPercentage;
                    var excludedAmount = normalShare * (exception.PercentageExcluded / 100);

                    totalExcluded += excludedAmount;
                }

                return Math.Round(totalExcluded, 2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular monto de redistribución");
                throw;
            }*/
            return 0;
        }

        public async Task<Dictionary<Guid, decimal>> CalculateUnitSavingsAsync(Guid budgetId)
        {
            try
            {
                var exceptions = await GetByBudgetAsync(budgetId);
                var units = exceptions.Select(e => e.IdGroupUnit).Distinct();
                var savings = new Dictionary<Guid, decimal>();

                foreach (var unitId in units)
                {
                    var unitSavings = await CalculateExcludedAmountAsync(budgetId, unitId);
                    savings[unitId] = unitSavings;
                }

                return savings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular ahorros por unidad");
                throw;
            }
        }

        // Métodos auxiliares privados
        /*private async Task<decimal> GetUnitAreaAsync(Guid unitId)
        {
            /var unit = await _context.GroupUnits
                .FirstOrDefaultAsync(u => u.IdGroupUnit == unitId);
            return unit?.TotalArea ?? 0;/
            return 0;
        }

        private async Task<decimal> GetTotalAreaAsync(Guid budgetId)
        {
            // Obtener todas las unidades asociadas al presupuesto
            /var budget = await _context.BudgetHeaders
                .Include(b => b.Installments)
                .FirstOrDefaultAsync(b => b.IdBudget == budgetId);

            return budget?.Installments?.Sum(i => i.TotalArea) ?? 0;/
            return 0;
        }*/
    }
}