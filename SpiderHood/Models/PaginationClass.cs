using SpiderHood.Models;

namespace SpiderHood.Utilities
{
    public class PaginationClass<T> where T : class
    {
        // Propiedades públicas
        public int CurrentPage { get; private set; } = 1;
        public int PageSize { get; private set; } = 25;
        public int TotalPages { get; private set; } = 0;
        public int TotalRecords { get; private set; } = 0;
        public int StartRecord { get; private set; } = 0;
        public int EndRecord { get; private set; } = 0;
        public List<T> CurrentPageData { get; private set; } = new();
        public List<T> AllData { get; set; } = new();
        public List<T> FilteredData { get; private set; } = new();
        public string SortColumn { get; private set; } = string.Empty;
        public bool SortAscending { get; private set; } = true;
        public string SearchTerm { get; private set; } = string.Empty;

        // Configuraciones
        private Dictionary<string, string> _filterOptions = new();
        private Dictionary<string, Func<T, object>> _sortExpressions = new();
        private string _defaultSortColumn = string.Empty;
        private Func<T, bool> _customFilter = null;

        // Eventos
        public event Action OnPaginationChanged;

        // Constructores
        public PaginationClass()
        {
            // Constructor por defecto
        }

        public PaginationClass(
            Dictionary<string, string> filterOptions,
            Dictionary<string, Func<T, object>> sortExpressions,
            string defaultSortColumn = "")
        {
            InitializeConfiguration(filterOptions, sortExpressions, defaultSortColumn);
        }

        // Método para inicializar configuración
        public void InitializeConfiguration(
            Dictionary<string, string> filterOptions,
            Dictionary<string, Func<T, object>> sortExpressions,
            string defaultSortColumn = "")
        {
            _filterOptions = filterOptions ?? new Dictionary<string, string>();
            _sortExpressions = sortExpressions ?? new Dictionary<string, Func<T, object>>();
            _defaultSortColumn = defaultSortColumn;

            if (!string.IsNullOrEmpty(_defaultSortColumn) && !_sortExpressions.ContainsKey(_defaultSortColumn))
            {
                throw new ArgumentException($"La columna de ordenamiento por defecto '{_defaultSortColumn}' no está en las expresiones de ordenamiento.");
            }
        }

        // Inicializar con datos
        public void Initialize(List<T> data = null)
        {
            if (data != null)
            {
                AllData = data;
            }

            CurrentPage = 1;
            ApplyFilterAndSort();
            UpdatePagination();
            NotifyChange();
        }

        // Método principal para aplicar filtros y ordenamiento
        private void ApplyFilterAndSort()
        {
            if (!AllData.Any())
            {
                FilteredData = new List<T>();
                return;
            }

            // Aplicar filtro personalizado
            var filtered = _customFilter != null
                ? AllData.Where(_customFilter).ToList()
                : AllData.ToList();

            // Aplicar búsqueda
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                filtered = ApplySearch(filtered, SearchTerm);
            }

            FilteredData = filtered;

            // Aplicar ordenamiento
            if (!string.IsNullOrWhiteSpace(SortColumn) && _sortExpressions.ContainsKey(SortColumn))
            {
                var sortExpression = _sortExpressions[SortColumn];
                FilteredData = SortAscending
                    ? FilteredData.OrderBy(sortExpression).ToList()
                    : FilteredData.OrderByDescending(sortExpression).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(_defaultSortColumn) &&
                     _sortExpressions.ContainsKey(_defaultSortColumn))
            {
                var defaultSort = _sortExpressions[_defaultSortColumn];
                FilteredData = FilteredData.OrderBy(defaultSort).ToList();
            }
        }

        // Método virtual para búsqueda (puede ser sobrescrito)
        protected virtual List<T> ApplySearch(List<T> data, string searchTerm)
        {
            // Implementación por defecto - buscar en todos los campos string
            return data.Where(item =>
                item.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        // Actualizar paginación
        private void UpdatePagination()
        {
            TotalRecords = FilteredData.Count;
            TotalPages = (int)Math.Ceiling((double)TotalRecords / PageSize);

            // Ajustar página actual
            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }
            else if (CurrentPage < 1 && TotalPages > 0)
            {
                CurrentPage = 1;
            }

            // Calcular registros
            StartRecord = TotalRecords > 0 ? ((CurrentPage - 1) * PageSize) + 1 : 0;
            EndRecord = Math.Min(CurrentPage * PageSize, TotalRecords);

            // Obtener datos de la página actual
            CurrentPageData = FilteredData
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }

        // Métodos de navegación
        public void GoToPage(int page)
        {
            if (page >= 1 && page <= TotalPages)
            {
                CurrentPage = page;
                UpdatePagination();
                NotifyChange();
            }
        }
        public void GoToFirstPage() => GoToPage(1);
        public void GoToPreviousPage() => GoToPage(CurrentPage - 1);
        public void GoToNextPage() => GoToPage(CurrentPage + 1);
        public void GoToLastPage() => GoToPage(TotalPages);

        // Cambiar tamaño de página
        public void ChangePageSize(int newSize)
        {
            if (newSize > 0)
            {
                PageSize = newSize;
                CurrentPage = 1;
                UpdatePagination();
                NotifyChange();
            }
        }

        // Ordenar por columna
        public void SortByColumn(string column)
        {
            if (_sortExpressions.ContainsKey(column))
            {
                if (SortColumn == column)
                {
                    SortAscending = !SortAscending;
                }
                else
                {
                    SortColumn = column;
                    SortAscending = true;
                }

                ApplyFilterAndSort();
                CurrentPage = 1;
                UpdatePagination();
                NotifyChange();
            }
        }

        // Buscar
        public void Search(string term)
        {
            SearchTerm = term;
            ApplyFilterAndSort();
            CurrentPage = 1;
            UpdatePagination();
            NotifyChange();
        }

        // Aplicar filtro personalizado
        public void ApplyCustomFilter(Func<T, bool> filter)
        {
            _customFilter = filter;
            ApplyFilterAndSort();
            CurrentPage = 1;
            UpdatePagination();
            NotifyChange();
        }

        // Limpiar filtros
        public void ClearFilters()
        {
            _customFilter = null;
            SearchTerm = string.Empty;
            ApplyFilterAndSort();
            CurrentPage = 1;
            UpdatePagination();
            NotifyChange();
        }

        // Obtener opciones de filtro
        public Dictionary<string, string> GetFilterOptions()
        {
            return _filterOptions;
        }

        // Obtener números de página
        public List<int> GetPageNumbers(int maxPagesToShow = 5)
        {
            var pages = new List<int>();

            if (TotalPages <= maxPagesToShow)
            {
                for (int i = 1; i <= TotalPages; i++)
                {
                    pages.Add(i);
                }
            }
            else
            {
                if (CurrentPage <= 3)
                {
                    for (int i = 1; i <= 4; i++) pages.Add(i);
                    pages.Add(-1);
                    pages.Add(TotalPages);
                }
                else if (CurrentPage >= TotalPages - 2)
                {
                    pages.Add(1);
                    pages.Add(-1);
                    for (int i = TotalPages - 3; i <= TotalPages; i++) pages.Add(i);
                }
                else
                {
                    pages.Add(1);
                    pages.Add(-1);
                    for (int i = CurrentPage - 1; i <= CurrentPage + 1; i++) pages.Add(i);
                    pages.Add(-1);
                    pages.Add(TotalPages);
                }
            }

            return pages;
        }

        // Métodos de utilidad
        public string GetPaginationInfo()
        {
            return TotalRecords > 0
                ? $"Mostrando {StartRecord}-{EndRecord} de {TotalRecords} registros"
                : "No hay registros";
        }

        public string GetSortIndicator(string column)
        {
            if (SortColumn == column)
            {
                return SortAscending ? "↑" : "↓";
            }
            return "";
        }

        // Notificar cambios
        private void NotifyChange()
        {
            OnPaginationChanged?.Invoke();
        }
    }

    public static class PaginationFactory
    {
        private static readonly Dictionary<Type, Func<object>> _typeFactories = new();

        static PaginationFactory()
        {
            // Registrar factories para tipos específicos
            _typeFactories[typeof(ServiceReadingDetail)] = () => new WaterReadingPagination();
            _typeFactories[typeof(MovementDetail)] = () => new MovementDetailPagination();
            _typeFactories[typeof(Installment)] = () => new InstallmentPagination();
        }

        public static PaginationClass<T> CreateForType<T>() where T : class
        {
            var type = typeof(T);

            if (_typeFactories.TryGetValue(type, out var factory))
            {
                return factory() as PaginationClass<T>;
            }

            // Crear una instancia genérica si no hay factory específica
            return new PaginationClass<T>();
        }

        public static PaginationClass<T> CreateForType<T>(
            Dictionary<string, string> filterOptions,
            Dictionary<string, Func<T, object>> sortExpressions,
            string defaultSortColumn = "") where T : class
        {
            var pagination = new PaginationClass<T>(
                filterOptions,
                sortExpressions,
                defaultSortColumn);
            return pagination;
        }

        public static void RegisterFactory<T>(Func<PaginationClass<T>> factory) where T : class
        {
            _typeFactories[typeof(T)] = factory;
        }
    }

    public class WaterReadingPagination : PaginationClass<ServiceReadingDetail>
    {
        public WaterReadingPagination() : base()
        {
            // Configurar opciones específicas para lecturas de agua
            var filterOptions = new Dictionary<string, string>
        {
            { "all", "Todas las lecturas" },
            { "processed", "Procesadas" },
            { "unprocessed", "Sin procesar" },
            { "minimum", "Consumo mínimo" },
            { "error", "Con errores" }
        };

            var sortExpressions = new Dictionary<string, Func<ServiceReadingDetail, object>>
        {
            { "GroupNumber", x => x.GroupNumber },
            { "Consumption", x => x.Consumption },
            { "ReadingDate", x => x.ReadingDate },
            { "CalculatedAmount", x => x.CalculatedAmount },
            { "Procesed", x => x.Procesed }
        };

            // Inicializar configuración usando el método de la clase base
            InitializeConfiguration(filterOptions, sortExpressions, "GroupNumber");
        }

        // Sobrescribir búsqueda para lecturas de agua
        protected override List<ServiceReadingDetail> ApplySearch(List<ServiceReadingDetail> data, string searchTerm)
        {
            return data.Where(item => (item.GroupNumber.ToString().Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // Métodos específicos para lecturas de agua
        public void FilterByApartment(string apartmentCode)
        {
            if (string.IsNullOrEmpty(apartmentCode))
            {
                ClearFilters();
            }
            else
            {
                ApplyCustomFilter(x =>
                    !string.IsNullOrEmpty(x.GroupNumber.ToString()) &&
                    x.GroupNumber.ToString().Contains(apartmentCode, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void FilterByProcessingStatus(bool isProcessed)
        {
            ApplyCustomFilter(x => x.Procesed == isProcessed);
        }

        public void FilterByGroupNumber(int? groupNumber)
        {
            if (!groupNumber.HasValue)
            {
                ClearFilters();
            }
            else
            {
                ApplyCustomFilter(x => x.GroupNumber == groupNumber.Value);
            }
        }

        public void FilterMinimumConsumption()
        {
            ApplyCustomFilter(x => x.Minimum);
        }

        public Dictionary<string, string> GetAvailableFilters()
        {
            return GetFilterOptions();
        }
    }

    public class MovementDetailPagination : PaginationClass<MovementDetail>
    {
        public MovementDetailPagination() : base()
        {
            var filterOptions = new Dictionary<string, string>
        {
            { "all", "Todos" },
            { "valid", "Válidos" },
            { "duplicate", "Duplicados" },
            { "error", "Con error" }
        };

            var sortExpressions = new Dictionary<string, Func<MovementDetail, object>>
        {
            { "MovDate", x => x.MovDate },
            { "Description", x => x.Description },
            { "ITF", x => x.ITF },
            { "Currency", x => x.Currency },
            { "Amount", x => x.Amount },
            { "Validation", x => x.Validation }
        };

            InitializeConfiguration(filterOptions, sortExpressions, "MovDate");
        }

        protected override List<MovementDetail> ApplySearch(List<MovementDetail> data, string searchTerm)
        {
            return data.Where(item =>
                (!string.IsNullOrEmpty(item.Description) &&
                 item.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(item.Currency) &&
                 item.Currency.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(item.Validation) &&
                 item.Validation.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        public void ApplyValidationFilter(string validationType)
        {
            if (validationType == "all")
            {
                ClearFilters();
            }
            else
            {
                ApplyCustomFilter(x =>
                    !string.IsNullOrEmpty(x.Validation) &&
                    x.Validation.Equals(validationType, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    public class InstallmentPagination : PaginationClass<Installment>
    {
        public InstallmentPagination() : base()
        {
            var filterOptions = new Dictionary<string, string>
        {
            { "all", "Todos" },
            { "valid", "Válidos" },
            { "duplicate", "Duplicados" },
            { "error", "Con error" }
        };

            var sortExpressions = new Dictionary<string, Func<Installment, object>>
        {
            { "UnitName", x => x.UnitName },
            { "OwnerName", x => x.OwnerName },
            { "TotalArea", x => x.TotalArea },
            { "Percent", x => x.Percent },
            { "Amount", x => x.Amount },
            { "IsPaid", x => x.IsPaid }
        };

            InitializeConfiguration(filterOptions, sortExpressions, "UnitName");
        }

        protected override List<Installment> ApplySearch(List<Installment> data, string searchTerm)
        {
            var term = searchTerm.ToLower();

            return data
                .Where(x => x.UnitName.ToLower().Contains(term) ||
                           x.OwnerName.ToLower().Contains(term))
                .ToList();

        }

        public void ApplyStatusFilter(string validationType)
        {
            if (validationType == "all")
            {
                ClearFilters();
            }
            else
            {
                ApplyCustomFilter(x => x.IsPaid);
            }
        }

    }
}