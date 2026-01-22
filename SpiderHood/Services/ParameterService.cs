using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Diagnostics.Metrics;
using System.Globalization;

namespace SpiderHood.Services
{
    public class ParameterService : IDisposable
    {
        
        private List<Parameter> _listParameters = new();
        private Guid _idBuilding;
        private Guid _idUser;
        private decimal _TotalArea;
        private string _role;
        private string _username;
        private int _dueday;
        private int _nroGroupUnit;
        private int _minAgua;
        private bool _disposed = false;

        // Cache para evitar cargas innecesarias
        private readonly Dictionary<Guid, (List<Parameter> Parameters, DateTime LastUpdated)> _cache
            = new Dictionary<Guid, (List<Parameter>, DateTime)>();
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        // Exponer BDLayout como público para ser usado en todas las llamadas
        public BDLayout ec { get; private set; }
        public SpiderHoodContext context { get; private set; }

        public IReadOnlyList<Parameter> ListParameters => _listParameters.AsReadOnly();
        public Guid IdBuilding => _idBuilding;
        public Guid IdUser => _idUser;
        public decimal TotalAera => _TotalArea;
        public string Role => _role;
        public string UserName => _username;
        public int DueDay => _dueday;
        public int nroGroupUnit => _nroGroupUnit;

        public int MinAgua => _minAgua;

        public event Action? OnChange;

        public ParameterService(SpiderHoodContext _context)
        {
            context = _context ?? throw new ArgumentNullException(nameof(_context));
            ec = new BDLayout(context);
            _role = "Admin";
            _TotalArea = 2861.9m;
            _username = "eechevarria";
            _dueday = 21;
            _nroGroupUnit = 30;
            _minAgua = 10;
        }

        /// <summary>
        /// Carga inicial desde la base de datos con caché
        /// </summary>
        public async Task LoadParametersAsync(Guid idBuilding, bool forceReload = false)
        {
            if (idBuilding == Guid.Empty)
                throw new ArgumentException("IdBuilding cannot be empty", nameof(idBuilding));

            // Verificar caché si no es recarga forzada
            if (!forceReload && _cache.TryGetValue(idBuilding, out var cached)
                && DateTime.UtcNow - cached.LastUpdated < CacheDuration)
            {
                _listParameters = new List<Parameter>(cached.Parameters);
                _idBuilding = idBuilding;
                await SetDefaultUserIdAsync();
                NotifyStateChanged();
                return;
            }

            // Carga desde base de datos usando el EC público
            try
            {
                var parameters = await ec.GetParametersByBuildingAsync(idBuilding);

                _listParameters = parameters ?? new List<Parameter>();
                _idBuilding = idBuilding;
                await SetDefaultUserIdAsync();

                // Actualizar caché
                UpdateCache(idBuilding, _listParameters);

                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                // Log error (considerar usar ILogger)
                throw new InvalidOperationException($"Error loading parameters for building {idBuilding}", ex);
            }
        }

        /// <summary>
        /// Recarga forzada de parámetros
        /// </summary>
        public async Task ReloadParametersAsync(Guid idBuilding)
        {
            await LoadParametersAsync(idBuilding, forceReload: true);
        }

        /// <summary>
        /// Guarda un parámetro (crea o actualiza)
        /// </summary>
        public async Task<OperationResult> SaveParameterAsync(Parameter parameter)
        {
            if (parameter == null)
                throw new ArgumentNullException(nameof(parameter));

            if (parameter.IdBuilding != _idBuilding)
                throw new InvalidOperationException("Parameter belongs to a different building");

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync();

                var existing = await context.Parameter
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdTabla == parameter.IdTabla);

                Parameter savedParameter;

                if (existing != null)
                {
                    // Actualización
                    //parameter.ModifiedDate = DateTime.UtcNow;
                    //parameter.ModifiedBy = _idUser;

                    context.Parameter.Update(parameter);
                    savedParameter = parameter;
                }
                else
                {
                    // Nuevo registro
                    //parameter.Id = Guid.NewGuid();
                    //parameter.CreatedDate = DateTime.UtcNow;
                    //parameter.CreatedBy = _idUser;

                    await context.Parameter.AddAsync(parameter);
                    savedParameter = parameter;
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Actualizar lista local y caché
                await UpdateLocalParameter(savedParameter);
                ClearCacheForBuilding(_idBuilding);

                return OperationResult.Success(savedParameter);
            }
            catch (DbUpdateException ex)
            {
                // Log error
                return OperationResult.Failure($"Database error: {ex.Message}");
            }
            catch (Exception ex)
            {
                // Log error
                return OperationResult.Failure($"Unexpected error: {ex.Message}");
            }
        }

        /// <summary>
        /// Guarda múltiples parámetros en una transacción
        /// </summary>
        public async Task<OperationResult> SaveParametersAsync(IEnumerable<Parameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var parameterList = parameters.ToList();
            if (!parameterList.Any())
                return OperationResult.Success();

            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync();

                var now = DateTime.UtcNow;
                foreach (var parameter in parameterList)
                {
                    var existing = await context.Parameter
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdTabla == parameter.IdTabla);

                    if (existing != null)
                    {
                        //parameter.ModifiedDate = now;
                        //parameter.ModifiedBy = _idUser;
                        context.Parameter.Update(parameter);
                    }
                    else
                    {
                        //parameter.Id = Guid.NewGuid();
                        //parameter.CreatedDate = now;
                        //parameter.CreatedBy = _idUser;
                        await context.Parameter.AddAsync(parameter);
                    }
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Recargar todos los parámetros del edificio
                await ReloadParametersAsync(_idBuilding);

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                // Log error
                return OperationResult.Failure($"Error saving multiple parameters: {ex.Message}");
            }
        }



        public async Task<List<Parameter>> GetParametersByParentAsync(int idParent)
        {
            return await context.Parameter
                .AsNoTracking()
                .Where(p => p.IdParent == idParent)
                .ToListAsync();
        }

        public async Task<List<Parameter>> GetParametersByBuildingAsync(Guid idBuilding)
        {
            return await context.Parameter
                .AsNoTracking()
                .Where(p => p.IdBuilding == idBuilding)
                .ToListAsync();
        }

        public async Task<List<SelectListItem>> GetMonthList(string _culture)
        {
            var cultura = new CultureInfo(_culture);
            List<SelectListItem> meses = cultura.DateTimeFormat.MonthNames
                                .Where(m => !string.IsNullOrEmpty(m))
                                .Select((m, index) => new SelectListItem
                                {
                                    Value = (index + 1).ToString(),
                                    Text = m
                                })
                                .ToList();
            return meses;
        }


        public async Task<List<SelectListItem>> GetYearList(int start, int end)
        {
            List<SelectListItem> years = new List<SelectListItem>();

            for (int i = start; i <= end; i++)
            {
                years.Add(new SelectListItem
                {
                    Value = i.ToString(),
                    Text = i.ToString()
                });
            }
            return years;
        }

        /// <summary>
        /// Obtiene un parámetro específico por su IdTabla
        /// </summary>
        public async Task<Parameter?> GetParameterAsync(int idTabla)
        {
            return await context.Parameter
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.IdTabla == idTabla);
        }

        /// <summary>
        /// Elimina un parámetro
        /// </summary>
        public async Task<OperationResult> DeleteParameterAsync(int idTabla)
        {
            try
            {
                var parameter = await context.Parameter
                    .FirstOrDefaultAsync(p => p.IdTabla == idTabla);

                if (parameter == null)
                    return OperationResult.Failure("Parameter not found");

                context.Parameter.Remove(parameter);
                await context.SaveChangesAsync();

                // Actualizar lista local
                _listParameters.RemoveAll(p => p.IdTabla == idTabla);
                ClearCacheForBuilding(_idBuilding);
                NotifyStateChanged();

                return OperationResult.Success();
            }
            catch (Exception ex)
            {
                // Log error
                return OperationResult.Failure($"Error deleting parameter: {ex.Message}");
            }
        }

        /// <summary>
        /// Método para acceder directamente a funcionalidades específicas de BDLayout
        /// </summary>
        public async Task<TResult> ExecuteThroughECAsync<TResult>(Func<BDLayout, Task<TResult>> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            try
            {
                return await operation(ec);
            }
            catch (Exception ex)
            {
                // Log error
                throw new InvalidOperationException("Error executing operation through EC", ex);
            }
        }

        /// <summary>
        /// Reinicia la instancia de EC (útil para resetear estado interno si es necesario)
        /// </summary>
        public void ResetEC()
        {
            ec = new BDLayout(context);
        }

        /// <summary>
        /// Limpia el caché para un edificio específico
        /// </summary>
        public void ClearCacheForBuilding(Guid buildingId)
        {
            _cache.Remove(buildingId);
        }

        /// <summary>
        /// Limpia todo el caché
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }

        private async Task SetDefaultUserIdAsync()
        {
            // Si necesitas obtener el usuario de forma asíncrona
            _idUser = new Guid("7968FA90-D02A-4567-B325-E1229CC034CE");

            // Alternativa: buscar usuario en base de datos si es dinámico
            // var user = await _context.Users.FirstOrDefaultAsync(u => u.IsDefault);
            // _idUser = user?.Id ?? Guid.Empty;
        }

        private void UpdateCache(Guid buildingId, List<Parameter> parameters)
        {
            _cache[buildingId] = (new List<Parameter>(parameters), DateTime.UtcNow);
        }

        private async Task UpdateLocalParameter(Parameter parameter)
        {
            var index = _listParameters.FindIndex(p => p.IdTabla == parameter.IdTabla);
            if (index >= 0)
            {
                _listParameters[index] = parameter;
            }
            else
            {
                _listParameters.Add(parameter);
            }
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnChange?.Invoke();

        #region IDisposable Implementation
        public void Dispose()
        {
            //Dispose(true);
            //GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Liberar recursos manejados
                    _cache.Clear();
                    _listParameters.Clear();

                    // Si BDLayout implementa IDisposable
                    if (ec is IDisposable disposableEC)
                    {
                        disposableEC.Dispose();
                    }
                }
                _disposed = true;
            }
        }
        #endregion

        public string GetChildParameterDescription(int parentId, int value)
        {
            var param = _listParameters.FirstOrDefault(c => c.IdParent == parentId && c.Value == value);
            return param?.ShortDescription ?? "No se encontro coincidencia";
        }

        public string GetParentParameterDescription(int IdTabla)
        {
            var param = _listParameters.FirstOrDefault(c => c.IdTabla == IdTabla);
            return param?.ShortDescription ?? "No se encontro coincidencia";
        }


    }

    /// <summary>
    /// Clase auxiliar para resultados de operaciones
    /// </summary>
    public class OperationResult
    {
        public bool IsSuccess { get; }
        public string? ErrorMessage { get; }
        public object? Data { get; }

        private OperationResult(bool isSuccess, string? errorMessage = null, object? data = null)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Data = data;
        }

        public static OperationResult Success(object? data = null)
            => new OperationResult(true, data: data);

        public static OperationResult Failure(string errorMessage)
            => new OperationResult(false, errorMessage);
    }
}