using DocumentFormat.OpenXml.Presentation;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;


namespace SpiderHood.Services
{
    // Services/IPresupuestoService.cs
    public interface IBudgetService
    {
        public List<BudgetHeader> _Budgets { get; set; }
        public BudgetHeader _SelectedBudget { get; set; }

        Task<List<BudgetHeader>> GetPresupuestosAsync(Guid IdBuilding, string? search = null, string? mes = null, BudgetStatus? estado = null);
        Task<List<BudgetSumCategory>> GetPresupuestosSumAsync(Guid IdBuilding);
        Task<BudgetHeader?> GetPresupuestoByIdAsync(Guid id);
        Task<BudgetHeader> CreatePresupuestoAsync(BudgetHeader presupuesto);
        Task UpdatePresupuestoAsync(BudgetHeader presupuesto);
        Task DeletePresupuestoAsync(Guid id);
        Task<BudgetState> InitializeBudgetStateAsync(BudgetHeader selectedBudget);
        Task LoadDefaultBudgetDetailsAsync(BudgetState state);
        Task LoadDataDefaultAsync(BudgetState state);
        Task SaveBudgetAsync(BudgetState state, List<Models.Period> _periods);
        Task<List<Exoneration>> GetExonerationByBudgetHeaderAsync(Guid presupuestoId);
        Task<List<Exoneration>> GetExonerationsByBuildingAsync(Guid IdBuilding);
        Task<List<BudgetDetail>> GetBudgetDetailAsync(Guid presupuestoId);

        // Categorías
        Task<List<Category>> GetCategoriasAsync(Guid IdBuilding, bool? activas = true);
        Task<Category?> GetCategoriaByIdAsync(Guid id);
        Task<Category> CreateCategoriaAsync(Category categoria);
        Task UpdateCategoriaAsync(Category categoria);

        // Detalles
        //Task<List<BudgetDetail>> GetDetallesByPresupuestoAsync(Guid presupuestoId);
        Task AddDetalleToPresupuestoAsync(BudgetDetail detalle);
        Task UpdateDetalleAsync(BudgetDetail detalle);
        Task DeleteDetalleAsync(Guid detalleId);

        // Categorías del presupuesto
        Task<List<PresupuestoCategoria>> GetCategoriasByPresupuestoAsync(Guid presupuestoId);
        Task UpdateCategoriaPresupuestoAsync(PresupuestoCategoria presupuestoCategoria);

    }

    public class BudgetService : IBudgetService
    {

        public List<BudgetHeader> _Budgets { get; set; } = new List<BudgetHeader>();
        public BudgetHeader _SelectedBudget { get; set; } = new BudgetHeader();

        private readonly IDbContextFactory<SpiderHoodContext> _contextFactory;
        private readonly ILogger<IBudgetService> _logger;
        private BDLayout ec { get; set; }

        public BudgetService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<IBudgetService> logger)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(contextFactory);
        }

        #region Presupuestos

        public async Task<List<BudgetHeader>> GetPresupuestosAsync(Guid IdBuilding, string? search = null, string? mes = null, BudgetStatus? estado = null)
        {
            try
            {
                List<BudgetHeader> query = await ec.GetBudgetsAsync(IdBuilding);

                // Aplicar filtros
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();

                    Func<BudgetHeader, bool> predicate = p =>
                        p.BudgetName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                        p.Mes.Contains(search, StringComparison.CurrentCultureIgnoreCase);

                    query = query.Where(predicate).ToList();

                }

                if (!string.IsNullOrWhiteSpace(mes))
                {
                    query = query.Where(p => p.Mes == mes).ToList();
                }

                if (estado != null)
                {
                    query = query.Where(p => p.Status == estado).ToList();
                }

                // Ordenar por fecha de creación descendente
                query = query.OrderByDescending(p => p.CreatedOn).ToList();

                var presupuestos = query.ToList();

                // Calcular totales si no están actualizados
                foreach (var presupuesto in presupuestos)
                {
                    if (presupuesto.Amount == 0)
                    {
                        presupuesto.Amount = presupuesto.Details.Sum(d => d.MonthlyAmount);
                    }
                }

                return presupuestos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener presupuestos");
                throw;
            }
        }

        public async Task<List<BudgetSumCategory>> GetPresupuestosSumAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetBudgetSumAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la suma de presupuestos");
                throw;
            }
        }

        public async Task<BudgetHeader?> GetPresupuestoByIdAsync(Guid id)
        {
            try
            {
                var presupuesto = await ec.GetBudgetByIdAsync(id);
                var detail = await ec.GetBudgetDetailAsync(id);
                presupuesto.Details = detail;

                if (presupuesto != null)
                {
                    // Calcular total si no está actualizado
                    if (presupuesto.Amount == 0)
                    {
                        presupuesto.Amount = presupuesto.Details.Sum(d => d.MonthlyAmount);
                    }
                }

                return presupuesto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener presupuesto por ID: {Id}", id);
                throw;
            }
        }

        public async Task<BudgetHeader> CreatePresupuestoAsync(BudgetHeader presupuesto)
        {
            try
            {
                if (presupuesto.IdBudgetHeader == Guid.Empty)
                    presupuesto.IdBudgetHeader = Guid.NewGuid();

                presupuesto.CreatedOn = DateTime.Now;

                await ec.AddNewRecordAsync(presupuesto);

                _logger.LogInformation("Presupuesto creado: {Id}", presupuesto.IdBudgetHeader);

                return presupuesto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear presupuesto");
                throw;
            }
        }

        public async Task UpdatePresupuestoAsync(BudgetHeader presupuesto)
        {
            /*try
            {
                // Verificar si existe
                var existente = await _context.Presupuestos
                    .FirstOrDefaultAsync(p => p.Id == presupuesto.Id);

                if (existente == null)
                {
                    throw new KeyNotFoundException($"Presupuesto con ID {presupuesto.Id} no encontrado");
                }

                // Validar código único (si cambió)
                if (existente.Codigo != presupuesto.Codigo)
                {
                    var existeCodigo = await _context.Presupuestos
                        .AnyAsync(p => p.Codigo == presupuesto.Codigo && p.Id != presupuesto.Id);

                    if (existeCodigo)
                    {
                        throw new InvalidOperationException($"Ya existe un presupuesto con el código {presupuesto.Codigo}");
                    }
                }

                // Actualizar propiedades
                existente.Codigo = presupuesto.Codigo;
                existente.Mes = presupuesto.Mes;
                existente.BudgetName = presupuesto.BudgetName;
                existente.Status= presupuesto.Status;
                existente.Amount = presupuesto.Amount;

                // Recalcular total si es necesario
                if (existente.Amount == 0)
                {
                    var detalles = await _context.PresupuestoDetalles
                        .Where(d => d.PresupuestoId == existente.Id)
                        .ToListAsync();

                    existente.Amount = detalles.Sum(d => d.Monto);
                }

                _context.Presupuestos.Update(existente);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Presupuesto actualizado: {Codigo}", existente.Codigo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar presupuesto ID: {Id}", presupuesto.Id);
                throw;
            }*/
        }

        public async Task DeletePresupuestoAsync(Guid id)
        {
            // BDLayout normalmente crea su propio SpiderHoodContext por operación (ver
            // BDLayout.Core.cs), pero eso rompería una transacción como esta: cada llamada
            // de ec.X() usaría su propia conexión, fuera de la transacción, y un rollback no
            // revertiría nada. Por eso este método arma su propio contexto + transacción y
            // pasa ese MISMO contexto a un BDLayout local (modo "fijo"), para que ambas
            // llamadas de abajo compartan la misma conexión/transacción.
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            var ecLocal = new BDLayout(context);

            try
            {
                BudgetHeader presupuesto = await ecLocal.GetBudgetByIdAsync(id);

                if (presupuesto == null)
                {
                    throw new KeyNotFoundException($"Presupuesto con ID {id} no encontrado");
                }

                // Eliminar detalles asociados
                await ecLocal.DeleteRecordAsync(presupuesto);

                await transaction.CommitAsync();

                //_logger.LogInformation("Presupuesto eliminado: {Codigo}", presupuesto.Codigo);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                //_logger.LogError(ex, "Error al eliminar presupuesto ID: {Id}", id);
                Console.WriteLine($"Error al eliminar presupuesto : {ex.Message}");
                throw;
            }
        }

        public async Task<BudgetState> InitializeBudgetStateAsync(BudgetHeader selectedBudget)
        {
            var state = new BudgetState();

            state.Budget = selectedBudget!;
            await LoadDataDefaultAsync(state);

            if (selectedBudget?.IdBudgetHeader != Guid.Empty)
            {

                //state.UpdateStatus(selectedBudget!.Status);
                state.IsNewBudget = false;

                await LoadBudgetDetailsAsync(state);
                state.CalculateTotals();
            }
            else
            {
                state.IsNewBudget = true;
                state.Status = BudgetStatus.Created;
            }

            return state;
        }

        public async Task LoadBudgetDetailsAsync(BudgetState state)
        {
            state.Budget.Details = await ec.GetBudgetDetailAsync(state.Budget.IdBudgetHeader);
        }

        public async Task LoadDataDefaultAsync(BudgetState state)
        {
            state.ExpensesList = await ec.GetPendingConciliationExpensesAsync(state.Budget.IdBuilding, state.Budget.BudgetDate, state.Budget.BudgetDate);
            state.Owners = await ec.GetOwnersByBuildingAsync(state.Budget.IdBuilding);
            state.Owners = state.Owners.Where(c => c.Role == 1 && c.TypeUnit == 1).ToList();
        }

        public async Task LoadDefaultBudgetDetailsAsync(BudgetState state)
        {
            //Cargar Template Default
            state.ListDefault = await ec.GetBudgetDetailDefaultAsync(state.Budget.IdBuilding);


            var sequentialNumber = 0.0m;
            state.Budget.Details.Clear();

            foreach (var detail in state.ListDefault)
            {
                if (detail.IsHeader)
                    sequentialNumber = 0.00m;

                var categoryAmount = state.ExpensesList
                    .Where(c => c.IdCategory == detail.IdCategory)
                    .Sum(x => x.Amount);

                var newItem = new BudgetDetail
                {
                    IdBudgetDetail = detail.IdBudgetDetail,
                    IdCategory = detail.IdCategory,
                    IdSection = detail.IdSection,
                    ItemNumber = detail.IdSection + sequentialNumber,
                    Description = detail.Description,
                    MonthlyAmount = detail.MonthlyAmount == 0 ? categoryAmount : detail.MonthlyAmount,
                    AnnualAmount = detail.AnnualAmount,
                    Frequency = detail.Frequency,
                    Type = detail.Type,
                    IsHeader = detail.IsHeader,
                    IdBudgetHeader = state.Budget.IdBudgetHeader,
                    IdParent = detail.IdParent
                };

                state.Budget.Details.Add(newItem);
                sequentialNumber += 0.01m;
            }
            state.CalculateTotals();

            Task.CompletedTask.Wait();
        }

        public async Task SaveBudgetAsync(BudgetState state, List<Models.Period> _periods)
        {
            // Igual que en DeletePresupuestoAsync: todas las llamadas de abajo (directas y
            // de los métodos privados que llaman) tienen que compartir el mismo contexto/
            // conexión que esta transacción, así que se pasa un BDLayout local en modo
            // "fijo" a través de toda la cadena en vez de usar el campo `ec` (que crea un
            // contexto nuevo por llamada y quedaría fuera de la transacción).
            using var context = await _contextFactory.CreateDbContextAsync();
            using var transaction = await context.Database.BeginTransactionAsync();
            var ecLocal = new BDLayout(context);

            try
            {
                foreach (var period in _periods.Where(c => c.IsNewPeriod))
                {
                    await ecLocal.AddNewRecordAsync(period);
                }

                if (state.Status == BudgetStatus.Created || state.Status == BudgetStatus.Rejected)
                {
                    await SaveCategoriesAsync(ecLocal, state);
                }

                if (state.Status == BudgetStatus.Active)
                {
                    //Guardar Installments en BD
                    await SaveInstallment(ecLocal, state);
                }

                if (state.IsNewBudget)
                {
                    await CreateNewBudgetAsync(ecLocal, state);
                }
                else
                {
                    if (state.Status < BudgetStatus.Check || state.Status == BudgetStatus.Rejected)
                        await UpdateExistingBudgetAsync(ecLocal, state);
                    else
                    {
                        await ecLocal.UpdateRecordAsync(state.Budget);

                        // Solo al llegar a Active (publicado) este presupuesto pasa a ser el
                        // vigente del edificio y corresponde cerrar el anterior. Antes se
                        // llamaba en cada paso desde Check en adelante, así que el presupuesto
                        // previo se cerraba apenas se mandaba el nuevo a revisión — si la Junta
                        // lo rechazaba después, el edificio se quedaba sin ningún presupuesto
                        // activo (el viejo ya cerrado, el nuevo nunca llegó a publicarse).
                        if (state.Status == BudgetStatus.Active)
                            await ecLocal.ClosePastBudgetsAsync(state.Budget.IdBuilding, state.Budget.BudgetDate);
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                //throw new BudgetException("Error al guardar el presupuesto", ex);
                throw new Exception("Error al guardar el presupuesto", ex);
            }
        }

        private async Task SaveCategoriesAsync(BDLayout ecLocal, BudgetState state)
        {
            foreach (var header in state.Details.Where(c => c.IsHeader && c.IsNewItem))
            {
                await SaveCategoryAsync(ecLocal, header, Guid.Empty, state.Budget.IdBuilding);

                foreach (var subItem in state.Details.Where(c => c.IdSection == header.IdSection && !c.IsHeader && c.IsNewItem))
                {
                    await SaveCategoryAsync(ecLocal, subItem, header.IdCategory, state.Budget.IdBuilding);
                }
            }
        }

        private async Task SaveCategoryAsync(BDLayout ecLocal, BudgetDetail item, Guid parentId, Guid IdBuilding)
        {
            var category = new Category
            {
                IdCategory = item.IdCategory,
                Description = item.Description,
                ShortDescript = item.Description,
                Color = "#FFFFFF",
                Icon = "fa-solid fa-droplet",
                IdBuilding = IdBuilding,
                Nivel = parentId == Guid.Empty ? 0 : item.IdSection,
                ParentId = parentId
            };

            await ecLocal.AddNewRecordAsync(category);
        }

        private async Task CreateNewBudgetAsync(BDLayout ecLocal, BudgetState state)
        {

            await ecLocal.AddNewRecordAsync(state.Budget);

            foreach (var item in state.Budget.Details.Where(c => c.IsHeader || c.MonthlyAmount > 0))
            {
                item.IdBudgetHeader = state.Budget.IdBudgetHeader;
                await ecLocal.AddNewRecordAsync(item);
            }

            state.IsNewBudget = false;
        }

        private async Task UpdateExistingBudgetAsync(BDLayout ecLocal, BudgetState state)
        {
            await ecLocal.DeleteRecordAsync(state.Budget.IdBudgetHeader);

            foreach (var item in state.Budget.Details.Where(c => c.IsHeader || c.MonthlyAmount > 0))
            {
                item.IdBudgetHeader = state.Budget.IdBudgetHeader;
                await ecLocal.AddNewRecordAsync(item);
            }

            await ecLocal.UpdateRecordAsync(state.Budget);
        }

        private async Task SaveInstallment(BDLayout ecLocal, BudgetState state)
        {

            foreach (var item in state.Installments)
                await ecLocal.AddNewRecordAsync(item);


            var ServiceHeader = state.WaterReadings.FirstOrDefault();
            ServiceReading UpdStatus = new ServiceReading();

            UpdStatus.IdServiceReading = ServiceHeader!.IdServiceReading;
            UpdStatus.Status = 2;

            //Actualizar lectura de Agua
            await ecLocal.UpdateRecordAsync(UpdStatus);

            InstallmentExoneration _exoneration = new();
            _exoneration.IdBudgetHeader = state.Budget.IdBudgetHeader;
            _exoneration.IdBuilding = state.Budget.IdBuilding;

            //Generar Hist de Excepciones para calculo
            await ecLocal.AddNewRecordAsync(_exoneration);
        }


        #endregion

        #region Categorías

        public async Task<List<Category>> GetCategoriasAsync(Guid IdBuilding, bool? activas = true)
        {
            try
            {
                List<Category> query = await ec.GetCategoriesAsync(IdBuilding);

                if (activas.HasValue)
                {
                    query = query.Where(c => c.Nivel == 0).ToList();
                }

                query = query.OrderBy(c => c.Nivel).ThenBy(c => c.ShortDescript).ToList();

                return query.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías");
                throw;
            }
        }

        public async Task<Category?> GetCategoriaByIdAsync(Guid id)
        {
            try
            {
                return await ec.GetCategoryByIdAsync(id);
                /*return await _context.Category
                    .FirstOrDefaultAsync(c => c.IdCategory == id);*/
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categoría por ID: {Id}", id);
                throw;
            }
        }

        public async Task<Category> CreateCategoriaAsync(Category categoria)
        {
            /*
            try
            {
                // Validar código único
                var existeCodigo = await _context.Categorias
                    .AnyAsync(c => c.Codigo == categoria.Codigo);

                if (existeCodigo)
                {
                    throw new InvalidOperationException($"Ya existe una categoría con el código {categoria.Codigo}");
                }

                // Establecer valores por defecto
                if (string.IsNullOrWhiteSpace(categoria.Tipo))
                {
                    categoria.Tipo = "Gasto";
                }

                if (string.IsNullOrWhiteSpace(categoria.Color))
                {
                    categoria.Color = "#3498db";
                }

                categoria.Activo = categoria.Activo;

                // Si no se especifica orden, poner al final
                if (categoria.Orden == 0)
                {
                    var maxOrden = await _context.Categorias
                        .MaxAsync(c => (int?)c.Orden) ?? 0;
                    categoria.Orden = maxOrden + 1;
                }

                _context.Categorias.Add(categoria);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoría creada: {Nombre}", categoria.Nombre);

                return categoria;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear categoría");
                throw;
            }
            */
            return categoria;
        }

        public async Task UpdateCategoriaAsync(Category categoria)
        {
            /*
            try
            {
                // Verificar si existe
                var existente = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.Id == categoria.Id);

                if (existente == null)
                {
                    throw new KeyNotFoundException($"Categoría con ID {categoria.Id} no encontrada");
                }

                // Validar código único (si cambió)
                if (existente.Codigo != categoria.Codigo)
                {
                    var existeCodigo = await _context.Categorias
                        .AnyAsync(c => c.Codigo == categoria.Codigo && c.Id != categoria.Id);

                    if (existeCodigo)
                    {
                        throw new InvalidOperationException($"Ya existe una categoría con el código {categoria.Codigo}");
                    }
                }

                // Actualizar propiedades
                existente.Codigo = categoria.Codigo;
                existente.Nombre = categoria.Nombre;
                existente.Descripcion = categoria.Descripcion;
                existente.Tipo = categoria.Tipo;
                existente.Activo = categoria.Activo;
                existente.Color = categoria.Color;
                existente.Orden = categoria.Orden;

                _context.Categorias.Update(existente);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoría actualizada: {Nombre}", existente.Nombre);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría ID: {Id}", categoria.Id);
                throw;
            }
            */
        }

        #endregion

        #region Detalles

        public async Task<List<BudgetDetail>> GetBudgetDetailAsync(Guid presupuestoId)
        {
            try
            {
                return await ec.GetBudgetDetailAsync(presupuestoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del presupuesto ID: {Id}", presupuestoId);
                throw;
            }
        }

        /*public async Task<List<BudgetDetail>> GetDetallesByPresupuestoAsync(Guid presupuestoId)
        {
            try
            {
                return await ec.GetBudgetDetailAsync(presupuestoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del presupuesto ID: {Id}", presupuestoId);
                throw;
            }
        }*/

        public async Task<List<Exoneration>> GetExonerationByBudgetHeaderAsync(Guid presupuestoId)
        {
            try
            {
                return await ec.GetExonerationByBudgetHeaderAsync(presupuestoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del presupuesto ID: {Id}", presupuestoId);
                throw;
            }
        }

        public async Task<List<Exoneration>> GetExonerationsByBuildingAsync(Guid IdBuilding)
        {
            try
            {
                return await ec.GetExonerationsByBuildingAsync(IdBuilding);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del presupuesto ID: {Id}", IdBuilding);
                throw;
            }
        }

        public async Task AddDetalleToPresupuestoAsync(BudgetDetail detalle)
        {
            try
            {
                if (detalle.IdBudgetDetail == Guid.Empty)
                    detalle.IdBudgetDetail = Guid.NewGuid();

                await ec.AddNewRecordAsync(detalle);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al agregar detalle al presupuesto {PresupuestoId}", detalle.IdBudgetHeader);
                throw;
            }
        }

        public async Task UpdateDetalleAsync(BudgetDetail detalle)
        {
            /*
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verificar si existe
                var existente = await _context.PresupuestoDetalles
                    .Include(d => d.Presupuesto)
                    .FirstOrDefaultAsync(d => d.Id == detalle.Id);

                if (existente == null)
                {
                    throw new KeyNotFoundException($"Detalle con ID {detalle.Id} no encontrado");
                }

                // Guardar monto anterior para ajustar total
                var montoAnterior = existente.Monto;

                // Actualizar propiedades
                existente.Descripcion = detalle.Descripcion;
                existente.Monto = detalle.Monto;
                existente.Notas = detalle.Notas;

                // Si cambió la categoría
                if (existente.CategoriaId != detalle.IdCategory)
                {
                    // Validar nueva categoría
                    var nuevaCategoria = await _context.Categorias
                        .FirstOrDefaultAsync(c => c.Id == detalle.IdCategory);

                    if (nuevaCategoria == null)
                    {
                        throw new KeyNotFoundException($"Categoría con ID {detalle.IdCategory} no encontrada");
                    }

                    if (!nuevaCategoria.Activo)
                    {
                        throw new InvalidOperationException($"La categoría {nuevaCategoria.Nombre} no está activa");
                    }

                    existente.CategoriaId = detalle.IdCategory;

                    // Actualizar relaciones con categorías
                    await AjustarRelacionesCategoriasAsync(
                        existente.PresupuestoId,
                        existente.CategoriaId,
                        montoAnterior,
                        detalle.MonthlyAmount);
                }

                _context.PresupuestoDetalles.Update(existente);

                // Ajustar total del presupuesto
                if (existente.Presupuesto != null)
                {
                    var diferencia = detalle.MonthlyAmount - montoAnterior;
                    existente.Presupuesto.Amount += diferencia;
                    _context.Presupuestos.Update(existente.Presupuesto);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Detalle actualizado ID: {Id}", detalle.IdBudgetDetail);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al actualizar detalle ID: {Id}", detalle.IdBudgetDetail);
                throw;
            }*/
        }

        public async Task DeleteDetalleAsync(Guid detalleId)
        {
            /*
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verificar si existe
                var detalle = await _context.PresupuestoDetalles
                    .Include(d => d.Presupuesto)
                    .FirstOrDefaultAsync(d => d.Id == detalleId);

                if (detalle == null)
                {
                    throw new KeyNotFoundException($"Detalle con ID {detalleId} no encontrado");
                }

                // Guardar datos para ajustes
                var presupuestoId = detalle.PresupuestoId;
                var categoriaId = detalle.CategoriaId;
                var monto = detalle.Monto;

                // Eliminar detalle
                _context.PresupuestoDetalles.Remove(detalle);

                // Ajustar total del presupuesto
                if (detalle.Presupuesto != null)
                {
                    detalle.Presupuesto.Amount -= monto;
                    _context.Presupuestos.Update(detalle.Presupuesto);
                }

                // Actualizar relación presupuesto-categoría
                await ActualizarRelacionCategoriaAsync(presupuestoId, categoriaId, -monto);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Detalle eliminado ID: {Id}", detalleId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al eliminar detalle ID: {Id}", detalleId);
                throw;
            }
            */
        }

        #endregion

        #region Categorías del Presupuesto

        public async Task<List<PresupuestoCategoria>> GetCategoriasByPresupuestoAsync(Guid presupuestoId)
        {
            try
            {
                return new List<Models.PresupuestoCategoria>();// ec.GetPresupuestoCategoria(presupuestoId).ToList();
                /*return await _context.PresupuestoCategorias
                    .Include(pc => pc.Categoria)
                    .Where(pc => pc.PresupuestoId == presupuestoId)
                    .OrderBy(pc => pc.Categoria.Orden)
                    .ToListAsync();*/
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener categorías del presupuesto ID: {Id}", presupuestoId);
                throw;
            }
        }

        public async Task UpdateCategoriaPresupuestoAsync(PresupuestoCategoria presupuestoCategoria)
        {
            /*try
            {
                // Verificar si existe
                var existente = await _context.PresupuestoCategorias
                    .FirstOrDefaultAsync(pc =>
                        pc.PresupuestoId == presupuestoCategoria.PresupuestoId &&
                        pc.CategoriaId == presupuestoCategoria.CategoriaId);

                if (existente == null)
                {
                    throw new KeyNotFoundException("Relación presupuesto-categoría no encontrada");
                }

                // Actualizar montos
                existente.MontoAsignado = presupuestoCategoria.MontoAsignado;
                existente.MontoEjecutado = presupuestoCategoria.MontoEjecutado;

                _context.PresupuestoCategorias.Update(existente);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Categoría de presupuesto actualizada: Presupuesto {PresupuestoId}, Categoría {CategoriaId}",
                    presupuestoCategoria.PresupuestoId, presupuestoCategoria.CategoriaId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar categoría de presupuesto");
                throw;
            }*/
        }

        #endregion

        #region Métodos Privados de Ayuda

        private async Task CrearRelacionesCategoriasAsync(Guid presupuestoId, Guid IdBuilding)
        {
            /*try
            {
                var categoriasActivas = await GetCategoriasAsync(IdBuilding, true);

                foreach (var categoria in categoriasActivas)
                {
                    var presupuestoCategoria = new PresupuestoCategoria
                    {
                        PresupuestoId = presupuestoId,
                        CategoriaId = categoria.IdCategory,
                        MontoAsignado = 0,
                        MontoEjecutado = 0
                    };

                    _context.PresupuestoCategorias.Add(presupuestoCategoria);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear relaciones de categorías para el presupuesto ID: {Id}", presupuestoId);
                throw;
            }*/
        }

        private async Task ActualizarRelacionCategoriaAsync(Guid presupuestoId, Guid categoriaId, decimal monto)
        {
            /*try
            {
                var relacion = await _context.PresupuestoCategorias
                    .FirstOrDefaultAsync(pc =>
                        pc.PresupuestoId == presupuestoId &&
                        pc.CategoriaId == categoriaId);

                if (relacion != null)
                {
                    relacion.MontoAsignado += monto;
                    _context.PresupuestoCategorias.Update(relacion);
                }
                else
                {
                    // Crear relación si no existe
                    relacion = new PresupuestoCategoria
                    {
                        PresupuestoId = presupuestoId,
                        CategoriaId = categoriaId,
                        MontoAsignado = monto,
                        MontoEjecutado = 0
                    };
                    _context.PresupuestoCategorias.Add(relacion);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar relación de categoría");
                throw;
            }*/
        }

        private async Task AjustarRelacionesCategoriasAsync(Guid presupuestoId, Guid nuevaCategoriaId,
                                                           decimal montoViejo, decimal montoNuevo)
        {
            /*try
            {
                // Restar de categoría anterior
                var relacionVieja = await _context.PresupuestoCategorias
                    .FirstOrDefaultAsync(pc =>
                        pc.PresupuestoId == presupuestoId &&
                        pc.CategoriaId == nuevaCategoriaId); // Nota: Aquí debería ser la categoría anterior

                if (relacionVieja != null)
                {
                    relacionVieja.MontoAsignado -= montoViejo;
                    _context.PresupuestoCategorias.Update(relacionVieja);
                }

                // Sumar a nueva categoría
                await ActualizarRelacionCategoriaAsync(presupuestoId, nuevaCategoriaId, montoNuevo);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ajustar relaciones de categorías");
                throw;
            }*/
        }

        #endregion
    }

    // Services/IPeriodService.cs
    public interface IPeriodService
    {
        Task<IEnumerable<Models.Period>> GetPeriodsByBuildingAsync(Guid IdBuilding);
        Task<Models.Period> GetPeriodByIdAsync(Guid id);
        Task<Models.Period> GetCurrentPeriodAsync(Guid buildingId);
        Task<bool> CreatePeriodAsync(Models.Period period);
        Task<bool> UpdatePeriodAsync(Models.Period period);
        Task<bool> DeletePeriodAsync(Guid id);
        Task<bool> SetAsCurrentPeriodAsync(Guid periodId, Guid buildingId);
        Task<bool> ValidatePeriodDatesAsync(Guid buildingId, DateTime startDate, DateTime endDate, Guid? excludeId = null);
    }

    public class PeriodService : IPeriodService
    {
        private BDLayout ec { get; set; }

        public PeriodService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<IEnumerable<Models.Period>> GetPeriodsByBuildingAsync(Guid IdBuilding)
        {
            return await ec.GetPeriodsByBuildingAsync(IdBuilding);
        }

        public async Task<Models.Period> GetPeriodByIdAsync(Guid id)
        {
            /*return await _context.Periods
                .FirstOrDefaultAsync(p => p.IdPeriod == id);*/
            return new Models.Period();
        }

        public async Task<Models.Period> GetCurrentPeriodAsync(Guid buildingId)
        {
            /*return await _context.Periods
                .FirstOrDefaultAsync(p => p.IdBuilding == buildingId && p.IsCurrentPeriod && p.Status == 1);*/
            return new Models.Period();
        }

        public async Task<bool> CreatePeriodAsync(Models.Period period)
        {
            try
            {
                // Validar que no haya superposición de fechas
                var hasOverlap = await ec.CheckPeriodOverlapAsync(period);

                if (hasOverlap)
                {
                    throw new Exception("El periodo se superpone con otro periodo existente");
                }

                //_context.Periods.Add(period);
                await ec.AddNewRecordAsync(period);

                // Si este periodo es el actual, desmarcar los demás
                if (period.IsCurrentPeriod)
                {
                    await UnsetOtherCurrentPeriods(period.IdBuilding, period.IdPeriod);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> UpdatePeriodAsync(Models.Period period)
        {
            try
            {
                // Validar que no haya superposición de fechas con otro periodo (el propio
                // IdPeriod que se le pasa a CHK_Period_CheckOverlap lo excluye de sí mismo).
                var hasOverlap = await ec.CheckPeriodOverlapAsync(period);

                if (hasOverlap)
                {
                    throw new Exception("El periodo se superpone con otro periodo existente");
                }

                await ec.UpdateRecordAsync(period);

                // Si este periodo pasa a ser el actual, desmarcar los demás
                if (period.IsCurrentPeriod)
                {
                    await UnsetOtherCurrentPeriods(period.IdBuilding, period.IdPeriod);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeletePeriodAsync(Guid id)
        {
            try
            {
                await ec.DeleteRecordAsync(new Models.Period { IdPeriod = id });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SetAsCurrentPeriodAsync(Guid periodId, Guid buildingId)
        {
            try
            {
                await UnsetOtherCurrentPeriods(buildingId, periodId);
                await ec.SetPeriodAsCurrentAsync(periodId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ValidatePeriodDatesAsync(Guid buildingId, DateTime startDate, DateTime endDate, Guid? excludeId = null)
        {
            /*var query = _context.Periods
                .Where(p => p.IdBuilding == buildingId &&
                           ((p.StartDate <= endDate && p.EndDate >= startDate) ||
                            (startDate <= p.EndDate && endDate >= p.StartDate)));

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.IdPeriod != excludeId.Value);
            }

            return !await query.AnyAsync();*/
            return false;
        }

        private async Task UnsetOtherCurrentPeriods(Guid IdBuilding, Guid IdcurrentPeriod)
        {
            await ec.UnsetOtherCurrentPeriodsAsync(IdBuilding, IdcurrentPeriod);
        }
    }
}