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
                    Status = ConcilationType.NoConciliada //Created
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
                    _total = Math.Round(_total,2);
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
                    Status = ConcilationType.NoConciliada // Created
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


