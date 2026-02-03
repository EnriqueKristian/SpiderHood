using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using SpiderHood.Data;
using SpiderHood.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // BudgetState.cs
    public class BudgetState
    {
        public BudgetHeader Budget { get; set; } = new();
        public List<BudgetDetail> Details => Budget.Details;
        public List<Installment> Installments { get; set; } =[];

        public List<OwnerUnitView> Owners { get; set; } = [];
        public List<ViewBudgetDetail> ListDefault { get; set; } = [];
        public List<ViewExpense> ExpensesList { get; set; } = [];
        public List<ServiceReadingDetail> WaterReadings { get; set; } = [];
        public List<Exoneration> Exonerations { get; set; } = [];

        public decimal TotalMonthly { get; private set; }
        public decimal TotalAnnual { get; private set; }
        public decimal QuotaPerApartment { get; private set; }
        public decimal TotalInstallments { get; private set; }
        public BuildingConfiguration Configuration { get; set; } = new();
        public decimal TotalArea { get; set; } = 9999;
        public int TotalApartments { get; set; } = 30;
        public int OccupiedApartments { get; set; } = 30;
        public bool UseProportionalDistribution { get; set; }
        public BudgetStatus Status
        {
            get => Budget.Status;
            set
            {
                Budget.Status = value;
            }
        }
        public bool IsReadOnly => !(Status == BudgetStatus.Rejected || Status == BudgetStatus.Created);
        public bool IsNewBudget { get; set; } = true;
        public DateTime LastPeriod { get; set; }
        public bool AddNewSection => Status == BudgetStatus.Rejected || Status == BudgetStatus.Created;
        public bool AddSampleData => Status == BudgetStatus.Rejected || Status == BudgetStatus.Created;
        public bool LoadServiceReading => !(Budget.Details.Count > 0);
        public bool IsWaterReadingReady => (WaterReadings.Count > 0);
        public bool SaveBudget()
        {
            return (Status == BudgetStatus.Rejected || Status == BudgetStatus.Created)
                   && Budget.Details.Count > 0;
        }
        public bool PublishBudget() {

            if (IsNewBudget)
                return false;
            else 
                return true;
        }
        public bool GenerateReport() {
            return Budget.Details.Count == 0;
        }

        private readonly BudgetCalculator _calculator;

        public BudgetState()
        {
            _calculator = new BudgetCalculator(this);
        }

        public void CalculateTotals()
        {
            (TotalMonthly, TotalAnnual) = _calculator.CalculateTotals();
            CalculateQuota();
        }

        public void CalculateQuota()
        {
            if (WaterReadings == null || !IsWaterReadingReady) return;
            
            if ( Status != BudgetStatus.Active && Status != BudgetStatus.Closed)
                TotalInstallments = _calculator.CalculateQuota(TotalApartments);
            else 
                TotalInstallments = Installments.Sum(i => i.Amount);
            
        }
    }

    public class BudgetCalculator(BudgetState state)
    {
        private readonly BudgetState _state = state;

        public (decimal Monthly, decimal Annual) CalculateTotals()
        {
            var nonHeaderItems = _state.Details.Where(x => !x.IsHeader).ToList();
            decimal monthly = 0;
            decimal annual = 0;

            foreach (var item in nonHeaderItems)
            {
                var annualMultiplier = GetAnnualMultiplier(item.Frequency);
                item.AnnualAmount = Math.Round(item.MonthlyAmount * annualMultiplier, 2);

                monthly += item.MonthlyAmount;
                annual += item.AnnualAmount;
            }

            return (monthly, annual);
        }

        public decimal CalculateQuota(int totalApartments)
        {
            decimal _totalInstallments = 0;

            // Calcular consumo total de agua
            decimal _totalWaterConsumption = _state.WaterReadings.Sum(c => c.CalculatedAmount);

            // Obtener excepciones/exoneraciones
            List<Exoneration> exceptions = _state.Exonerations;

            // Limpiar cuotas anteriores
            _state.Installments.Clear();

            foreach (var unit in _state.Owners)
            {
                // Crear nueva cuota
                Installment _dpto = new Installment
                {
                    IdInstallment = Guid.NewGuid(),
                    IdBudgetHeader = _state.Budget.IdBudgetHeader,
                    CreationDate = DateTime.Now, //_Budget.BudgetDate;
                    TotalArea = unit.TotalArea,
                    UnitName = unit.UnitNumber,
                     Number = int.Parse(unit.UnitNumber),
                    OwnerName = unit.FirstName,
                    IdGroupUnit = unit.IdGroupUnit,
                    CreatedBy = "eechevarria", //UserName;
                    DueDate = DateTime.Now.AddDays(_state.Configuration.DueDay),//DateTime.Now.AddDays(ParameterService.DueDay);
                    Status = 1 //Created
                };

                decimal _total = 0;

                // Calcular distribución por área (si totalArea > 0)
                decimal _distr = unit.TotalArea / _state.TotalArea;

                // 1. AGREGAR CONSUMO DE AGUA (si aplica)
                if (_state.WaterReadings.Count > 0)
                {
                    var wateritem = _state.WaterReadings.Where(c => c.IdGroupUnit == unit.IdGroupUnit).FirstOrDefault()!;
                    _total += wateritem.CalculatedAmount;
                }

                // 2. PROCESAR DETALLES DEL PRESUPUESTO
                foreach (var item in _state.Budget.Details)
                {
                    // 2.1. CASO ESPECIAL: AGUA
                    if (item.IdCategory == _state.Configuration.WaterReadingDefault && _state.WaterReadings!.Count > 0 )
                    {
                        // Distribuir el consumo general menos lo ya asignado individualmente
                        _total += Math.Abs(item.MonthlyAmount - _totalWaterConsumption ) / totalApartments;
                    }
                    else
                    {
                        // 2.2. OTRAS CATEGORÍAS
                        //Obtener cuantos DPTOs tienen exoneracion en esta categoria
                        var _nroException = exceptions.Count(c => c.IdCategory == item.IdCategory);

                        //Verificar que la unidad tenga esta exoneración
                        if ( unit.IdGroupUnit == exceptions.Where(c => c.IdCategory == item.IdCategory).Select(c => c.IdGroupUnit).FirstOrDefault())
                        {
                            _total += 0;
                        }
                        else
                        {
                            // Distribuir según tipo
                            _total += item.Type == 1 ? item.MonthlyAmount / (totalApartments - _nroException) : item.MonthlyAmount * _distr;
                        }
                    }
                }

                _dpto.Amount = _total;
                _dpto.Percent = 100 * _distr;
                _totalInstallments = _totalInstallments + Math.Round(_total,2);

                _state.Installments.Add(_dpto);
            }
            return _totalInstallments;
        }

        public decimal CalculateQuota1(int totalApartments)
        {
            decimal _totalInstallments = 0;

            // Calcular consumo total de agua
            decimal _totalWaterConsumption = _state.WaterReadings.Sum(c => c.CalculatedAmount);

            // Obtener excepciones/exoneraciones
            List<Exoneration> exceptions = _state.Exonerations;

            // Limpiar cuotas anteriores
            _state.Installments.Clear();

            // Calcular área total
            decimal totalArea = _state.TotalArea > 0 ? _state.TotalArea :
                               _state.Owners.Sum(o => o.TotalArea);

            foreach (var unit in _state.Owners)
            {
                // Crear nueva cuota
                Installment _dpto = new Installment
                {
                    IdInstallment = Guid.NewGuid(),
                    IdBudgetHeader = _state.Budget.IdBudgetHeader,
                    CreationDate = DateTime.Now,
                    TotalArea = unit.TotalArea,
                    UnitName = unit.UnitNumber,
                    OwnerName = unit.FirstName,
                    IdGroupUnit = unit.IdGroupUnit,
                    CreatedBy = "eechevarria", // TODO: Reemplazar con usuario real
                    DueDate = DateTime.Now.AddDays(_state.Configuration.DueDay),
                    Status = 1 // Created
                };

                decimal _total = 0;

                // Calcular distribución por área (si totalArea > 0)
                decimal _distr = totalArea > 0 ? unit.TotalArea / totalArea : 0;

                // 1. AGREGAR CONSUMO DE AGUA (si aplica)
                if (_state.WaterReadings.Count > 0)
                {
                    var wateritem = _state.WaterReadings
                        .FirstOrDefault(c => c.IdGroupUnit == unit.IdGroupUnit);

                    if (wateritem != null)
                    {
                        _total += wateritem.CalculatedAmount;
                    }
                }

                // 2. PROCESAR DETALLES DEL PRESUPUESTO
                foreach (var item in _state.Budget.Details)
                {
                    // 2.1. CASO ESPECIAL: AGUA
                    if (item.IdCategory == _state.Configuration.WaterReadingDefault && _state.WaterReadings.Count > 0)
                    {
                        // Esta lógica parece incorrecta. Normalmente el agua se distribuye diferente
                        // Dependiendo de si hay medición individual o general
                        if (_state.WaterReadings.Any(w => w.IdGroupUnit == unit.IdGroupUnit))
                        {
                            // Si tiene medición individual, ya se agregó arriba
                            continue;
                        }
                        else
                        {
                            // Distribuir el consumo general menos lo ya asignado individualmente
                            decimal individualWaterTotal = _state.WaterReadings.Sum(w => w.CalculatedAmount);
                            decimal generalWaterToDistribute = Math.Abs(item.MonthlyAmount - individualWaterTotal);
                            _total += generalWaterToDistribute / totalApartments;
                        }
                    }
                    // 2.2. OTRAS CATEGORÍAS
                    else
                    {
                        // Verificar si esta unidad está exonerada de esta categoría
                        bool isExonerated = exceptions
                            .Any(e => e.IdGroupUnit == unit.IdGroupUnit &&
                                   e.IdCategory == item.IdCategory);

                        if (isExonerated)
                        {
                            // Unidad exonerada - no paga esta categoría
                            continue;
                        }
                        else
                        {
                            // Calcular cuántas unidades NO están exoneradas para esta categoría
                            int nonExoneratedUnits = totalApartments -
                                exceptions.Count(e => e.IdCategory == item.IdCategory);

                            if (nonExoneratedUnits <= 0)
                            {
                                // Todas las unidades están exoneradas, no hay que distribuir
                                continue;
                            }

                            // Distribuir según tipo
                            if (item.Type == 1) // Distribución por unidad (igual para todos)
                            {
                                _total += item.MonthlyAmount / nonExoneratedUnits;
                            }
                            else if (item.Type == 2) // Distribución por área
                            {
                                // Calcular área total NO exonerada para esta categoría
                                decimal nonExoneratedArea = _state.Owners
                                    .Where(o => !exceptions.Any(e =>
                                        e.IdGroupUnit == o.IdGroupUnit &&
                                        e.IdCategory == item.IdCategory))
                                    .Sum(o => o.TotalArea);

                                if (nonExoneratedArea > 0 && totalArea > 0)
                                {
                                    _total += item.MonthlyAmount * (unit.TotalArea / nonExoneratedArea);
                                }
                            }
                            else // Otro tipo de distribución
                            {
                                _total += item.MonthlyAmount * _distr;
                            }
                        }
                    }
                }

                // Asignar valores finales
                //_dpto.Amount = Math.Round(_total, 2);
                //_dpto.Percent = totalArea > 0 ? Math.Round(100 * _distr, 2) : 0;
                _dpto.Amount = _total;
                _dpto.Percent = totalArea > 0 ? 100 * _distr : 0;

                _totalInstallments += _dpto.Amount;
                _state.Installments.Add(_dpto);
            }

            //return Math.Round(_totalInstallments, 2);
            return _totalInstallments;
        }

        public List<Installment> UpdateInstallments(List<Installment> installments)
        {
            // Lógica para actualizar las cuotas
            return installments;
        }

        private int GetAnnualMultiplier(int frequency) => frequency switch
        {
            1 => 12,
            2 => 6,
            3 => 4,
            4 => 3,
            _ => 1
        };
    }

    // Models/InstallmentException.cs
    public class Exoneration
    {
        public Guid IdExoneration { get; set; }
        public Guid IdGroupUnit { get; set; }
        public Guid IdCategory { get; set; } // Categoría a excluir (ej: Mantenimiento Ascensor)
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public Guid IdBuilding{ get; set; }
        [NotMapped]
        public bool IsDeleted { get; set; } = false;

        public Exoneration Clone() => (Exoneration)this.MemberwiseClone();
    }

    public class InstallmentException
    {
        public Guid IdException { get; set; }
        public Guid IdBudgetHeader { get; set; }
        public Guid IdGroupUnit { get; set; }
        public Guid IdCategory { get; set; } // Categoría a excluir (ej: Mantenimiento Ascensor)
        public string Description { get; set; } = string.Empty;
        public decimal PercentageExcluded { get; set; } // Porcentaje a excluir (100% = totalmente excluido)
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;

        // Navigation properties
        [NotMapped]
        public virtual BudgetHeader? Budget { get; set; }
        [NotMapped]
        public virtual Category? Category { get; set; }
        [NotMapped]
        public virtual GroupUnit? GroupUnit { get; set; }
    }



    public class InstallmentExoneration
    {
        public Guid IdInstallmentExoneration { get; set; }
        public Guid IdGroupUnit { get; set; }
        public Guid IdCategory { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; } = string.Empty;
        public Guid IdBudgetHeader { get; set; }
        public Guid IdBuilding { get; set; }

        // Relaciones (si deseas agregarlas más adelante)
        // public GroupUnit GroupUnit { get; set; }
        // public Category Category { get; set; }
        // public BudgetHeader BudgetHeader { get; set; }
    }











}


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
}



namespace SpiderHood.Services
{
    public class ExceptionService : IExceptionService
    {
        private SpiderHoodContext Context { get; set; }
        private BDLayout ec { get; set; }

        private readonly ILogger<ExceptionService> _logger;
        //private readonly IBudgetService _budgetService;
        //private readonly ICategoryService _categoryService;

        /*public ExceptionService(
            SpiderHoodContext context,
            ILogger<ExceptionService> logger,
            IBudgetService budgetService,
            ICategoryService categoryService)
        {
            _context = context;
            _logger = logger;
            _budgetService = budgetService;
            _categoryService = categoryService;
        }*/

        public ExceptionService(
            SpiderHoodContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            ec = new BDLayout(Context);
        }

        public async Task<List<Exoneration>> GetExceptionsByBuildingAsync(Guid idBuilding)
        {
            return await ec.GetExonerationsByBuildingAsync(idBuilding);
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
            return new List<InstallmentException>();
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
            return new List<InstallmentException>();
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
            return new List<InstallmentException>();
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
            return new List<InstallmentException>();
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
            return new List<InstallmentException>();
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
            return new List<InstallmentException>();
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
            return new List<Guid>(); 
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
            return new Dictionary<Guid, decimal>();
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
        private async Task<decimal> GetUnitAreaAsync(Guid unitId)
        {
            /*var unit = await _context.GroupUnits
                .FirstOrDefaultAsync(u => u.IdGroupUnit == unitId);
            return unit?.TotalArea ?? 0;*/
            return 0;
        }

        private async Task<decimal> GetTotalAreaAsync(Guid budgetId)
        {
            // Obtener todas las unidades asociadas al presupuesto
            /*var budget = await _context.BudgetHeaders
                .Include(b => b.Installments)
                .FirstOrDefaultAsync(b => b.IdBudget == budgetId);

            return budget?.Installments?.Sum(i => i.TotalArea) ?? 0;*/
            return 0;
        }
    }
}