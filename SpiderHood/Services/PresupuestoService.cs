using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System;

namespace SpiderHood.Services
{

    // Services/IPresupuestoService.cs
    public interface IPresupuestoService
    {
        Task<List<BudgetHeader>> GetPresupuestosAsync(Guid IdBuilding, string? search = null,  string? mes = null, BudgetStatus? estado = null);
        Task<BudgetHeader?> GetPresupuestoByIdAsync(Guid id);
        Task<BudgetHeader> CreatePresupuestoAsync(BudgetHeader presupuesto);
        Task UpdatePresupuestoAsync(BudgetHeader presupuesto);
        Task DeletePresupuestoAsync(Guid id);

        // Categorías
        Task<List<Category>> GetCategoriasAsync(Guid IdBuilding, bool? activas = true);
        Task<Category?> GetCategoriaByIdAsync(Guid id);
        Task<Category> CreateCategoriaAsync(Category categoria);
        Task UpdateCategoriaAsync(Category categoria);

        // Detalles
        Task<List<BudgetDetail>> GetDetallesByPresupuestoAsync(Guid presupuestoId);
        Task AddDetalleToPresupuestoAsync(BudgetDetail detalle);
        Task UpdateDetalleAsync(BudgetDetail detalle);
        Task DeleteDetalleAsync(Guid detalleId);

        // Categorías del presupuesto
        Task<List<PresupuestoCategoria>> GetCategoriasByPresupuestoAsync(Guid presupuestoId);
        Task UpdateCategoriaPresupuestoAsync(PresupuestoCategoria presupuestoCategoria);
    }


    public class BudgetService : IPresupuestoService
    {
        public SpiderHoodContext _context = default!;
        private readonly ILogger<BudgetService> _logger;
        private BDLayout ec { get; set; }

        public BudgetService(SpiderHoodContext context, ILogger<BudgetService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(context);
        }

        #region Presupuestos

        public async Task<List<BudgetHeader>> GetPresupuestosAsync(Guid IdBuilding, string? search = null,  string? mes = null, BudgetStatus? estado = null)
        {
            try
            {
                var query = ec.GetBudgets(IdBuilding);

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

                if ( estado != null)
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

        public async Task<BudgetHeader?> GetPresupuestoByIdAsync(Guid id)
        {
            try
            {
                var presupuesto = ec.GetBudgetById(id);
                var detail = await ec.GetBudgetDetail(id);
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
            /*using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validar código único
                var existeCodigo = await _context.Presupuestos
                    .AnyAsync(p => p.Codigo == presupuesto.Codigo);

                if (existeCodigo)
                {
                    throw new InvalidOperationException($"Ya existe un presupuesto con el código {presupuesto.Codigo}");
                }

                // Establecer valores por defecto
                presupuesto.CreatedOn = DateTime.Now;
                presupuesto.Status = 1; // presupuesto.Status ?? 1;

                // Agregar presupuesto
                _context.Presupuestos.Add(presupuesto);
                await _context.SaveChangesAsync();

                // Crear relaciones con categorías activas
                await CrearRelacionesCategoriasAsync(presupuesto.Id);

                await transaction.CommitAsync();

                _logger.LogInformation("Presupuesto creado: {Codigo}", presupuesto.Codigo);

                return presupuesto;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al crear presupuesto");
                throw;
            }
            */
            return presupuesto;
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
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Verificar si existe
                var presupuesto = await _context.BudgetHeader
                    .FirstOrDefaultAsync(p => p.IdBudgetHeader == id);

                if (presupuesto == null)
                {
                    throw new KeyNotFoundException($"Presupuesto con ID {id} no encontrado");
                }

                // Eliminar detalles asociados
                await ec.DeleteRecordAsync(presupuesto);
                
                // Eliminar presupuesto
                //_context.BudgetHeader.Remove(presupuesto);
                await _context.SaveChangesAsync();

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

        #endregion

        #region Categorías

        public async Task<List<Category>> GetCategoriasAsync(Guid IdBuilding,bool? activas = true)
        {
            try
            {

                //var query = _context.Categorias.AsQueryable(); 
                //var query = ec.GetCategorias(Guid.NewGuid()).AsQueryable();
                var query = ec.GetCategories(IdBuilding).AsQueryable();

                if (activas.HasValue)
                {
                    query = query.Where(c => c.Nivel == 0);
                }

                query = query.OrderBy(c => c.Nivel).ThenBy(c => c.ShortDescript);

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
                return await _context.Category
                    .FirstOrDefaultAsync(c => c.IdCategory == id);
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

        public async Task<List<BudgetDetail>> GetDetallesByPresupuestoAsync(Guid presupuestoId)
        {
            try
            {
                return await ec.GetBudgetDetail(presupuestoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del presupuesto ID: {Id}", presupuestoId);
                throw;
            }
        }

        public async Task AddDetalleToPresupuestoAsync(BudgetDetail detalle)
        {
            /*
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validar que existe el presupuesto
                var presupuesto = await _context.Presupuestos
                    .FirstOrDefaultAsync(p => p.Id == detalle.PresupuestoId);

                if (presupuesto == null)
                {
                    throw new KeyNotFoundException($"Presupuesto con ID {detalle.PresupuestoId} no encontrado");
                }

                // Validar que existe la categoría
                var categoria = await _context.Categorias
                    .FirstOrDefaultAsync(c => c.Id == detalle.CategoriaId);

                if (categoria == null)
                {
                    throw new KeyNotFoundException($"Categoría con ID {detalle.CategoriaId} no encontrada");
                }

                // Validar que la categoría esté activa
                if (!categoria.Activo)
                {
                    throw new InvalidOperationException($"La categoría {categoria.Nombre} no está activa");
                }

                // Agregar detalle
                _context.PresupuestoDetalles.Add(detalle);
                await _context.SaveChangesAsync();

                // Actualizar total del presupuesto
                presupuesto.Amount += detalle.Monto;
                _context.Presupuestos.Update(presupuesto);

                // Actualizar relación presupuesto-categoría
                await ActualizarRelacionCategoriaAsync(presupuesto.Id, categoria.Id, detalle.Monto);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Detalle agregado al presupuesto {PresupuestoId}", detalle.PresupuestoId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al agregar detalle al presupuesto");
                throw;
            }
            */
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
                return ec.GetPresupuestoCategoria(presupuestoId).ToList();
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
            try
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
            }
        }

        #endregion

        #region Métodos Privados de Ayuda

        private async Task CrearRelacionesCategoriasAsync(Guid presupuestoId, Guid IdBuilding)
        {
            try
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
            }
        }

        private async Task ActualizarRelacionCategoriaAsync(Guid presupuestoId, Guid categoriaId, decimal monto)
        {
            try
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
            }
        }

        private async Task AjustarRelacionesCategoriasAsync(Guid presupuestoId, Guid nuevaCategoriaId,
                                                           decimal montoViejo, decimal montoNuevo)
        {
            try
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
            }
        }

        #endregion
    }
}



