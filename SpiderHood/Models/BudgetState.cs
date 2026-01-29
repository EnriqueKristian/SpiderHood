using Microsoft.EntityFrameworkCore;

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

        public decimal TotalMonthly { get; private set; }
        public decimal TotalAnnual { get; private set; }
        public decimal QuotaPerApartment { get; private set; }
        public decimal TotalInstallments { get; private set; }
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
            Installments.Clear();
            TotalInstallments = _calculator.CalculateQuota(TotalApartments);
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

        public decimal CalculateQuota1(int totalApartments)
        {
            decimal _totalInstallments = 0;
            decimal _totalWaterConsumption = _state.WaterReadings.Sum(c => c.CalculatedAmount); ;

            _state.Installments.Clear();

            Guid idAgua = Guid.Parse("CB42DE58-8C94-4CAA-82CF-4E5D0F6B2B8C");

            ServiceReadingDetail wateritem = new();

            foreach (var unit in _state.Owners)
            {
                Installment _dpto = new Installment
                {
                    IdInstallment = Guid.NewGuid(),
                    IdBudgetHeader = _state.Budget.IdBudgetHeader,
                    CreationDate = DateTime.Now, //_Budget.BudgetDate;
                    TotalArea = unit.TotalArea,
                    UnitName = unit.UnitNumber,
                    OwnerName = unit.FirstName,
                    IdGroupUnit = unit.IdGroupUnit,
                    CreatedBy = "eechevarria", //UserName;
                    DueDate = DateTime.Now.AddDays(10),//DateTime.Now.AddDays(ParameterService.DueDay);
                    Status = 1 //Created
                };

                decimal _total = 0;
                decimal _distr = unit.TotalArea / _state.TotalArea; // ParameterService.TotalAera;

                if (_state.WaterReadings.Count > 0)
                {
                    wateritem = _state.WaterReadings.Where(c => c.IdGroupUnit == unit.IdGroupUnit).FirstOrDefault()!;
                    _total += wateritem.CalculatedAmount;
                }

                foreach (var item in _state.Budget.Details)
                {
                    if (item.IdCategory == idAgua && _state.WaterReadings!.Count > 0 )
                    {
                        _total += Math.Abs(item.MonthlyAmount - _totalWaterConsumption ) / totalApartments;//wateritem!.CalculatedAmount;
                    }
                    else
                    {
                        _total += item.Type == 1 ? item.MonthlyAmount / totalApartments : item.MonthlyAmount * _distr;
                    }
                }

                _dpto.Amount = _total;
                _dpto.Percent = 100 * _distr;
                _totalInstallments = _totalInstallments + _total;

                _state.Installments.Add(_dpto);
            }
            return _totalInstallments;
        }
        
        public decimal CalculateQuota(int totalApartments)
        {
            const string WATER_GUID = "CB42DE58-8C94-4CAA-82CF-4E5D0F6B2B8C";
            const string CREATED_BY = "eechevarria";

            // Pre-cálculos
            decimal totalWaterConsumption = _state.WaterReadings.Sum(c => c.CalculatedAmount);
            bool hasWaterReadings = _state.WaterReadings.Count > 0;
            Guid waterCategoryId = Guid.Parse(WATER_GUID);

            // Diccionario de lecturas de agua
            var waterReadingsDict = hasWaterReadings
                ? _state.WaterReadings.ToDictionary(w => w.IdGroupUnit)
                : null;

            // Separar items del presupuesto
            var budgetDetails = _state.Budget.Details.ToList();
            var waterBudgetItem = budgetDetails.FirstOrDefault(d => d.IdCategory == waterCategoryId);
            var otherBudgetItems = budgetDetails.Where(d => d.IdCategory != waterCategoryId).ToList();

            // Calcular diferencia de agua una sola vez
            decimal waterDifferencePerApartment = hasWaterReadings && waterBudgetItem != null
                ? Math.Abs(waterBudgetItem.MonthlyAmount - totalWaterConsumption) / totalApartments
                : 0;

            // Pre-calcular valores constantes por tipo de item
            var type1Items = otherBudgetItems.Where(i => i.Type == 1).ToList();
            var typeNot1Items = otherBudgetItems.Where(i => i.Type != 1).ToList();

            decimal type1Total = type1Items.Sum(i => i.MonthlyAmount) / totalApartments;

            _state.Installments.Clear();
            var now = DateTime.Now;
            var dueDate = now.AddDays(10);

            // Usar for en lugar de foreach para mejor performance
            var owners = _state.Owners;
            decimal totalInstallments = 0;

            for (int i = 0; i < owners.Count; i++)
            {
                var unit = owners[i];
                decimal distributionFactor = unit.TotalArea / _state.TotalArea;
                decimal unitTotal = 0;

                // Agua
                if (hasWaterReadings)
                {
                    if (waterReadingsDict != null && waterReadingsDict.TryGetValue(unit.IdGroupUnit, out var waterItem))
                    {
                        unitTotal += waterItem.CalculatedAmount;
                    }

                    if (waterBudgetItem != null)
                    {
                        unitTotal += waterDifferencePerApartment;
                    }
                }

                // Items tipo 1 (suma fija por departamento)
                unitTotal += type1Total;

                // Items no tipo 1 (proporcionales al área)
                foreach (var item in typeNot1Items)
                {
                    unitTotal += item.MonthlyAmount * distributionFactor;
                }

                // Crear installment con object initializer
                _state.Installments.Add(new Installment
                {
                    IdInstallment = Guid.NewGuid(),
                    IdBudgetHeader = _state.Budget.IdBudgetHeader,
                    CreationDate = now,
                    TotalArea = unit.TotalArea,
                    UnitName = unit.UnitNumber,
                    OwnerName = unit.FirstName,
                    IdGroupUnit = unit.IdGroupUnit,
                    CreatedBy = CREATED_BY,
                    DueDate = dueDate,
                    Status = 1,
                    Amount = unitTotal,
                    Percent = 100 * distributionFactor
                });

                totalInstallments += unitTotal;
            }

            return totalInstallments;
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
}


