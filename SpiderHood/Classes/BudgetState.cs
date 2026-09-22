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

        public bool IsNewBudget { get; set; } = true;
        // "Sin guardar todavía" en BudgetHeaderComponent -- a diferencia de SaveBudget()
        // (que sólo indica que el presupuesto TODAVÍA es editable, Status Created/Rejected,
        // y se queda en ese Status aún después de guardar con éxito hasta que se envía a
        // aprobación), esto sí refleja si hay cambios en memoria sin persistir: arranca en
        // true (nada guardado todavía), se apaga al cargar un presupuesto ya existente y al
        // terminar un guardado exitoso, y se prende de nuevo con cualquier edición.
        public bool HasUnsavedChanges { get; set; } = true;
        public DateTime LastPeriod { get; set; }
        public bool AddSampleData => Status == BudgetStatus.Rejected || Status == BudgetStatus.Created;
        public bool LoadServiceReading => !(Budget.Details.Count > 0);
        public bool IsWaterReadingReady => (WaterReadings.Count > 0);

        // Sólo un presupuesto que de verdad incluye la categoría de agua del edificio
        // (Configuration.WaterReadingDefault) entre sus Details depende de tener lectura
        // cargada -- uno sin esa sección (ej. Cuotas Extraordinarias, o un Ordinario que
        // todavía no la agregó) puede calcular cuotas igual, ver CalculateQuota().
        public bool RequiereLecturaAgua =>
            Configuration.WaterReadingDefault.HasValue &&
            Budget.Details.Any(d => !d.IsHeader && d.IdCategory == Configuration.WaterReadingDefault.Value);
        public bool SaveBudget()
        {
            return (Status == BudgetStatus.Rejected || Status == BudgetStatus.Created)
                   && Budget.Details.Count > 0;
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
            // Antes este guard bloqueaba el cálculo COMPLETO de cuotas (todas las
            // categorías, no sólo Agua) para cualquier presupuesto sin lectura de agua
            // cargada -- incluso uno que no tiene ninguna sección de Agua y nunca la va a
            // tener (ver el mismo fix ya aplicado en BudgetGenerator.ValidarPresupuestoParaAprobacion).
            // BudgetCalculator.CalculateQuota() ya maneja WaterReadings vacío
            // correctamente (sólo omite el aporte de agua) -- lo único que de verdad
            // necesita la lectura cargada es la categoría configurada como
            // Configuration.WaterReadingDefault, así que sólo bloqueamos si el
            // presupuesto realmente la incluye.
            if (RequiereLecturaAgua && !IsWaterReadingReady) return;

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

            // Agrupar por Grupo de Unidades -- _state.Owners puede traer más de una fila
            // para el mismo grupo por dos motivos distintos: copropietarios (2 filas,
            // mismo IdUnit) o un grupo con más de un Depto/Oficina adentro (ej. el grupo
            // "Inmobiliaria" con varias unidades sin vender, Docs/Pendientes-Negocio-
            // Consolidado.md #1). Antes se generaba una Installment POR FILA -- para un
            // grupo con más de una unidad eso contaba el área proporcional del grupo
            // (TotalArea, ya vista completa en cada fila) una vez de más por cada unidad
            // extra, y para Fija cada fila pagaba como si fuera la única unidad del grupo
            // en vez de la parte que realmente le toca. Ahora se genera UNA sola cuota por
            // grupo, con un "peso" = cantidad de Depto/Oficina distintos que tiene.
            var grupos = _state.Owners.GroupBy(o => o.IdGroupUnit);

            foreach (var grupo in grupos)
            {
                var primero = grupo.First();
                var unidadesDelGrupo = grupo.Select(u => u.UnitNumber).Distinct().ToList();
                int pesoFija = grupo.Select(u => u.IdUnit).Distinct().Count();

                // Crear nueva cuota
                Installment _dpto = new Installment
                {
                    IdInstallment = Guid.NewGuid(),
                    IdBudgetHeader = _state.Budget.IdBudgetHeader,
                    CreationDate = DateTime.Now, //_Budget.BudgetDate;
                    Period = _state.Budget.BudgetDate,
                    TotalArea = primero.TotalArea,
                    UnitName = string.Join(", ", unidadesDelGrupo),
                    Number = primero.Number,
                    OwnerName = primero.FirstName,
                    IdGroupUnit = primero.IdGroupUnit,
                    CreatedBy = _state.Budget.CreatedBy,
                    DueDate = DateTime.Now.AddDays(_state.Configuration.DueDay),//DateTime.Now.AddDays(ParameterService.DueDay);
                    Status = ReconciliationType.NoConciliada //Created
                };

                decimal _total = 0;

                // Calcular distribución por área (si totalArea > 0)
                decimal _distr = primero.TotalArea / _state.TotalArea;

                // 1. AGREGAR CONSUMO DE AGUA (si aplica)
                if (_state.WaterReadings.Count > 0)
                {
                    var wateritem = _state.WaterReadings.Where(c => c.IdGroupUnit == primero.IdGroupUnit).FirstOrDefault();
                    if (wateritem != null)
                        _total += wateritem.CalculatedAmount;
                }

                // 2. PROCESAR DETALLES DEL PRESUPUESTO
                foreach (var item in _state.Budget.Details)
                {
                    bool esCategoriaAgua = item.IdCategory == _state.Configuration.WaterReadingDefault && _state.WaterReadings!.Count > 0;

                    //Obtener cuantos grupos tienen exoneracion en esta categoria -- ahora
                    //aplica también a Agua (antes Agua ignoraba las exoneraciones por
                    //completo y siempre dividía entre el total de unidades sin restar
                    //excepciones).
                    var _nroException = exceptions.Count(c => c.IdCategory == item.IdCategory);

                    //Verificar que el grupo tenga esta exoneración -- Any() sobre TODAS las
                    //exoneraciones de la categoría, no sólo la primera que aparezca (antes
                    //comparaba contra exceptions...FirstOrDefault(), así que con más de un
                    //grupo exonerado de la misma categoría sólo el primero se libraba de
                    //verdad).
                    bool exonerado = exceptions.Any(c => c.IdCategory == item.IdCategory && c.IdGroupUnit == primero.IdGroupUnit);

                    // Docs/Pendientes-Negocio-Consolidado.md #28 -- congela acá cuántas
                    // unidades realmente dividieron esta categoría (después de
                    // exoneraciones), para que Ver Detalle/el PDF de un presupuesto YA
                    // PUBLICADO lean este valor en vez de recalcular con la composición
                    // ACTUAL del edificio (que puede haber cambiado desde entonces). Solo
                    // aplica a categorías con divisor por unidad (Agua y Fija) -- las
                    // %-based (Type != 1) no usan "número de unidades".
                    if (esCategoriaAgua || item.Type == 1)
                        item.NroApartments = totalApartments - _nroException;

                    if (exonerado)
                    {
                        _total += 0;
                    }
                    else if (esCategoriaAgua)
                    {
                        // Distribuir el consumo general menos lo ya asignado individualmente,
                        // pesado por cuántas unidades Depto/Oficina tiene este grupo (mismo
                        // criterio que Fija más abajo). Math.Max(0, ...) en vez de Math.Abs:
                        // si el consumo medido total ya supera el presupuesto de la categoría,
                        // no queda "común" por repartir -- Abs convertía ese excedente en un
                        // cobro ADICIONAL positivo, cobrando dos veces el mismo excedente
                        // (ver CalculateQuota_WhenMeteredConsumptionExceedsBudget_* test).
                        _total += Math.Max(0, item.MonthlyAmount - _totalWaterConsumption) / (totalApartments - _nroException) * pesoFija;
                    }
                    else
                    {
                        // Distribuir según tipo -- Fija pesada por cantidad real de
                        // Depto/Oficina del grupo, no 1 fijo por grupo.
                        _total += item.Type == 1 ? item.MonthlyAmount / (totalApartments - _nroException) * pesoFija : item.MonthlyAmount * _distr;
                    }
                    _total = Math.Round(_total,2);
                }

                _dpto.Amount = _total;
                _dpto.Percent = 100 * _distr;
                _totalInstallments = _totalInstallments + Math.Round(_total,2);

                _state.Installments.Add(_dpto);
            }
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
        [Precision(18, 2)]
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

    }











}


