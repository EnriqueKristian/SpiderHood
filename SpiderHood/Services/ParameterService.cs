using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Reflection.Metadata.Ecma335;

namespace SpiderHood.Services
{
    public class ParameterService : IDisposable
    {
        
        private List<Parameter> _listParameters = [];
        private Guid _idUser;
        private string _role;
        private string _username;
        private List<Period> _periods = [];
        private bool _disposed = false;


        // Cache para evitar cargas innecesarias
        private readonly Dictionary<Guid, (List<Parameter> Parameters, DateTime LastUpdated, List<Period> Periodos)> _cache
            = [];

        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        // Exponer BDLayout como público para ser usado en todas las llamadas
        public BDLayout ec { get; private set; }
        public SpiderHoodContext Context { get; private set; }

        public IReadOnlyList<Parameter> ListParameters => _listParameters.AsReadOnly();

        public Building CurrentBuilding { get; set; } = null!;


        public Guid IdUser => _idUser;
        public string Role => _role;
        public string UserName => _username;
        
        public event Action? OnChange;

        public ParameterService(SpiderHoodContext _context)
        {
            Context = _context ?? throw new ArgumentNullException(nameof(_context));
            ec = new BDLayout(Context);
            _role = "Admin";
            _username = "eechevarria";
        }

        /// <summary>
        /// Carga inicial desde la base de datos con caché
        /// </summary>
        public async Task LoadParametersAsync(Guid IdBuilding, bool forceReload = false)
        {
            if ( CurrentBuilding != null)
                return;
                //throw new ArgumentException("IdBuilding cannot be empty", nameof(IdBuilding));

            // Verificar caché si no es recarga forzada
            if (!forceReload && _cache.TryGetValue(IdBuilding, out var cached)
                && DateTime.UtcNow - cached.LastUpdated < CacheDuration)
            {
                _listParameters = [.. cached.Parameters];
                _periods = [.. cached.Periodos];

                await SetDefaultUserIdAsync();
                NotifyStateChanged();
                return;
            }

            // Carga desde base de datos usando el EC público
            try
            {
                var parameters = await ec.GetParametersByBuildingAsync(IdBuilding);
                var periodos =  await ec.GetPeriodByBuilding(IdBuilding);
                var building = await ec.GetBuildingById(IdBuilding);
                if (building is null)
                    throw new InvalidOperationException($"Building with Id {IdBuilding} not found.");
                CurrentBuilding = building;
                var listconfig = await ec.GetBuildingConfiguration(IdBuilding);
                CurrentBuilding!.Configuration = listconfig.FirstOrDefault()!;

                _listParameters = parameters ?? [];
                _periods = periodos;
                await SetDefaultUserIdAsync();

                // Actualizar caché
                UpdateCache(IdBuilding, _listParameters,_periods);

                NotifyStateChanged();
            }
            catch (Exception ex)
            {
                // Log error (considerar usar ILogger)
                throw new InvalidOperationException($"Error loading parameters for building {IdBuilding}", ex);
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

            if (parameter.IdBuilding != CurrentBuilding.IdBuilding)
                throw new InvalidOperationException("Parameter belongs to a different building");

            try
            {
                await using var transaction = await Context.Database.BeginTransactionAsync();

                var existing = await Context.Parameter
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.IdTabla == parameter.IdTabla);

                Parameter savedParameter;

                if (existing != null)
                {
                    // Actualización
                    //parameter.ModifiedDate = DateTime.UtcNow;
                    //parameter.ModifiedBy = _idUser;

                    Context.Parameter.Update(parameter);
                    savedParameter = parameter;
                }
                else
                {
                    // Nuevo registro
                    //parameter.Id = Guid.NewGuid();
                    //parameter.CreatedDate = DateTime.UtcNow;
                    //parameter.CreatedBy = _idUser;

                    await Context.Parameter.AddAsync(parameter);
                    savedParameter = parameter;
                }

                await Context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Actualizar lista local y caché
                await UpdateLocalParameter(savedParameter);
                ClearCacheForBuilding(CurrentBuilding.IdBuilding);

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
                await using var transaction = await Context.Database.BeginTransactionAsync();

                var now = DateTime.UtcNow;
                foreach (var parameter in parameterList)
                {
                    var existing = await Context.Parameter
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.IdTabla == parameter.IdTabla);

                    if (existing != null)
                    {
                        //parameter.ModifiedDate = now;
                        //parameter.ModifiedBy = _idUser;
                        Context.Parameter.Update(parameter);
                    }
                    else
                    {
                        //parameter.Id = Guid.NewGuid();
                        //parameter.CreatedDate = now;
                        //parameter.CreatedBy = _idUser;
                        await Context.Parameter.AddAsync(parameter);
                    }
                }

                await Context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Recargar todos los parámetros del edificio
                await ReloadParametersAsync(CurrentBuilding.IdBuilding);

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
            return await Context.Parameter
                .AsNoTracking()
                .Where(p => p.IdParent == idParent)
                .ToListAsync();
        }

        public async Task<List<Parameter>> GetParametersByBuildingAsync(Guid idBuilding)
        {
            return await Context.Parameter
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

        public async Task<List<Period>> GetPeriodsAsync()
        {
            return await ec.GetPeriodByBuilding(CurrentBuilding!.IdBuilding);
        }

        public async Task<Models.Period> CreateNewPeriod(DateTime lastPeriod)
        {
            return new Models.Period
            {
                IdPeriod = Guid.NewGuid(),
                IdBuilding = CurrentBuilding.IdBuilding,
                Name = lastPeriod.ToString("MMMM-yy", CultureInfo.InvariantCulture),
                PeriodType = 1,
                StartDate = lastPeriod,
                EndDate = new DateTime(lastPeriod.Year, lastPeriod.Month, DateTime.DaysInMonth(lastPeriod.Year, lastPeriod.Month)),
                ClosingDate = new DateTime(lastPeriod.Year, lastPeriod.Month, DateTime.DaysInMonth(lastPeriod.Year, lastPeriod.Month)),
                Status = 1,
                IsCurrentPeriod = true,
                IsNewPeriod = true
            };
        }

        /// <summary>
        /// Obtiene un parámetro específico por su IdTabla
        /// </summary>
        public async Task<Parameter?> GetParameterAsync(int idTabla)
        {
            return await Context.Parameter
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
                var parameter = await Context.Parameter
                    .FirstOrDefaultAsync(p => p.IdTabla == idTabla);

                if (parameter == null)
                    return OperationResult.Failure("Parameter not found");

                Context.Parameter.Remove(parameter);
                await Context.SaveChangesAsync();

                // Actualizar lista local
                _listParameters.RemoveAll(p => p.IdTabla == idTabla);
                ClearCacheForBuilding(CurrentBuilding.IdBuilding);
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
            ec = new BDLayout(Context);
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

        private void UpdateCache(Guid buildingId, List<Parameter> parameters, List<Period> periodos)
        {
            _cache[buildingId] = (new List<Parameter>(parameters), DateTime.UtcNow, new List<Period>(periodos));
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
            Dispose(true);
            GC.SuppressFinalize(this);
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