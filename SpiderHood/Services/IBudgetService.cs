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
    }

    public class BudgetService : IBudgetService
    {

        public List<BudgetHeader> _Budgets { get; set; } = new List<BudgetHeader>();
        public BudgetHeader _SelectedBudget { get; set; } = new BudgetHeader();

        private readonly IDbContextFactory<SpiderHoodContext> _contextFactory;
        private readonly ILogger<IBudgetService> _logger;
        private readonly AuthService _authService;
        private BDLayout ec { get; set; }

        public BudgetService(IDbContextFactory<SpiderHoodContext> contextFactory, ILogger<IBudgetService> logger, AuthService authService)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            ec = new BDLayout(contextFactory);
        }

        // Usuario de la sesión actual, para estampar Auditoría (CreatedBy/ModifiedBy) en
        // BudgetHeader. "system" es el fallback para llamadas fuera de un circuito con
        // sesión activa (no debería pasar en uso normal de la app).
        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
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
                await ec.StampAuditAsync(AuditableEntity.BudgetHeader, presupuesto.IdBudgetHeader, await GetPerformedByAsync(), isCreate: true);

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
            // La implementación original de este método (comentada más abajo en el
            // historial de git) usaba _context.Presupuestos como DbSet<T> rastreado por
            // EF Core, que nunca existió en SpiderHoodContext (Presupuesto está
            // registrado HasNoKey(), solo para FromSqlRaw) — el método quedó como no-op
            // silencioso. Único caller (Index.razor → GuardarCambiosDetalle) llama a
            // OnSave de PresupuestoDetalleModal, que hoy no tiene ningún botón "Guardar"
            // conectado, así que esto no afectaba a nadie en producción todavía. Se
            // implementa igual, con el mismo patrón que el resto de BudgetService (SP
            // vía BDLayout), para que quede correcto en cuanto se conecte el botón.
            try
            {
                await ec.UpdateRecordAsync(presupuesto);
                await ec.StampAuditAsync(AuditableEntity.BudgetHeader, presupuesto.IdBudgetHeader, await GetPerformedByAsync(), isCreate: false);
                _logger.LogInformation("Presupuesto actualizado: {Id}", presupuesto.IdBudgetHeader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar presupuesto ID: {Id}", presupuesto.IdBudgetHeader);
                throw;
            }
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
                state.Budget.CreatedBy = await GetPerformedByAsync();
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
            var performedBy = await GetPerformedByAsync();

            try
            {
                foreach (var period in _periods.Where(c => c.IsNewPeriod))
                {
                    await ecLocal.AddNewRecordAsync(period);
                    await ecLocal.StampAuditAsync(AuditableEntity.Period, period.IdPeriod, performedBy, isCreate: true);
                }

                if (state.Status == BudgetStatus.Created || state.Status == BudgetStatus.Rejected)
                {
                    await SaveCategoriesAsync(ecLocal, state, performedBy);
                }

                if (state.Status == BudgetStatus.Active)
                {
                    //Guardar Installments en BD
                    await SaveInstallment(ecLocal, state, performedBy);
                }

                if (state.IsNewBudget)
                {
                    await CreateNewBudgetAsync(ecLocal, state, performedBy);
                }
                else
                {
                    if (state.Status < BudgetStatus.Check || state.Status == BudgetStatus.Rejected)
                        await UpdateExistingBudgetAsync(ecLocal, state, performedBy);
                    else
                    {
                        await ecLocal.UpdateRecordAsync(state.Budget);
                        await ecLocal.StampAuditAsync(AuditableEntity.BudgetHeader, state.Budget.IdBudgetHeader, performedBy, isCreate: false);
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

        private async Task SaveCategoriesAsync(BDLayout ecLocal, BudgetState state, string performedBy)
        {
            // Antes este método sólo recorría headers NUEVOS (IsHeader && IsNewItem), así
            // que un item nuevo agregado bajo una sección ya existente nunca pasaba por acá
            // y su Category jamás se creaba en BD — el INSERT del BudgetDetail fallaba
            // después con FK_BudgetDetail_Category porque el Idcategory referenciado no
            // existía. Ahora se recorren TODOS los headers (nuevos o existentes) para poder
            // detectar sus items nuevos, pero el header en sí sólo se guarda si es nuevo.
            foreach (var header in state.Details.Where(c => c.IsHeader))
            {
                if (header.IsNewItem)
                {
                    await SaveCategoryAsync(ecLocal, header, Guid.Empty, state.Budget.IdBuilding, performedBy);
                }

                foreach (var subItem in state.Details.Where(c => c.IdSection == header.IdSection && !c.IsHeader && c.IsNewItem))
                {
                    await SaveCategoryAsync(ecLocal, subItem, header.IdCategory, state.Budget.IdBuilding, performedBy);
                }
            }
        }

        private async Task SaveCategoryAsync(BDLayout ecLocal, BudgetDetail item, Guid parentId, Guid IdBuilding, string performedBy)
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
            await ecLocal.StampAuditAsync(AuditableEntity.Category, category.IdCategory, performedBy, isCreate: true);
        }

        private async Task CreateNewBudgetAsync(BDLayout ecLocal, BudgetState state, string performedBy)
        {

            await ecLocal.AddNewRecordAsync(state.Budget);
            await ecLocal.StampAuditAsync(AuditableEntity.BudgetHeader, state.Budget.IdBudgetHeader, performedBy, isCreate: true);

            foreach (var item in state.Budget.Details.Where(c => c.IsHeader || c.MonthlyAmount > 0))
            {
                item.IdBudgetHeader = state.Budget.IdBudgetHeader;
                await ecLocal.AddNewRecordAsync(item);
            }

            state.IsNewBudget = false;
        }

        private async Task UpdateExistingBudgetAsync(BDLayout ecLocal, BudgetState state, string performedBy)
        {
            await ecLocal.DeleteRecordAsync(state.Budget.IdBudgetHeader);

            foreach (var item in state.Budget.Details.Where(c => c.IsHeader || c.MonthlyAmount > 0))
            {
                item.IdBudgetHeader = state.Budget.IdBudgetHeader;
                await ecLocal.AddNewRecordAsync(item);
            }

            await ecLocal.UpdateRecordAsync(state.Budget);
            await ecLocal.StampAuditAsync(AuditableEntity.BudgetHeader, state.Budget.IdBudgetHeader, performedBy, isCreate: false);
        }

        private async Task SaveInstallment(BDLayout ecLocal, BudgetState state, string performedBy)
        {

            foreach (var item in state.Installments)
                await ecLocal.AddNewRecordAsync(item);


            var ServiceHeader = state.WaterReadings.FirstOrDefault();
            ServiceReading UpdStatus = new ServiceReading();

            UpdStatus.IdServiceReading = ServiceHeader!.IdServiceReading;
            UpdStatus.Status = 2;

            //Actualizar lectura de Agua
            await ecLocal.UpdateRecordAsync(UpdStatus);
            await ecLocal.StampAuditAsync(AuditableEntity.ServiceReading, UpdStatus.IdServiceReading, performedBy, isCreate: false);

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
    }

}