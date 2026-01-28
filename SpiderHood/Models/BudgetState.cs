using BlazorBootstrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Models;
using SpiderHood.Services;
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

        public decimal TotalMonthly { get; private set; }
        public decimal TotalAnnual { get; private set; }
        public decimal QuotaPerApartment { get; private set; }
        public decimal TotalInstallments { get; private set; }

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
        //public bool IsDisabled { get; set; } = true;
        public DateTime lastPeriod { get; set; }
        public bool AddNewSection => Status == BudgetStatus.Rejected || Status == BudgetStatus.Created;
        public bool AddSampleData => Status == BudgetStatus.Rejected || Status == BudgetStatus.Created;
        public bool LoadServiceReading => !(Budget.Details.Count > 0);
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
            Installments.Clear();
            TotalInstallments = _calculator.CalculateQuota(TotalApartments);

            //if (Installments.Any())
            //{
            //Installments = _calculator.UpdateInstallments(Installments);
            //}
        }
    }

    // BudgetCalculator.cs
    public class BudgetCalculator
    {
        private readonly BudgetState _state;

        public BudgetCalculator(BudgetState state)
        {
            _state = state;
        }

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
            /*return totalApartments > 0
                ? Math.Round(_state.TotalMonthly / totalApartments, 2)
                : 0;*/

            decimal _quotaPerApartment = totalApartments > 0
            ? Math.Round(_state.TotalMonthly / totalApartments, 2)
            : 0;

            decimal _totalInstallments = 0;

            _state.Installments.Clear();

            Guid idAgua = Guid.Parse("CB42DE58-8C94-4CAA-82CF-4E5D0F6B2B8C");

            ServiceReadingDetail wateritem = new ServiceReadingDetail();

            foreach (var unit in _state.Owners)
            {
                Installment _dpto = new Installment();
                _dpto.IdInstallment = Guid.NewGuid();
                _dpto.IdBudgetHeader = _state.Budget.IdBudgetHeader;
                _dpto.CreationDate = DateTime.Now; //_Budget.BudgetDate;
                _dpto.TotalArea = unit.TotalArea;
                _dpto.UnitName = unit.UnitNumber;
                _dpto.OwnerName = unit.FirstName;
                _dpto.IdGroupUnit = unit.IdGroupUnit;
                _dpto.CreatedBy = "eechevarria"; //UserName;
                _dpto.DueDate = DateTime.Now.AddDays(10);//DateTime.Now.AddDays(ParameterService.DueDay);
                _dpto.Status = 1; //Created

                decimal _total = 0;
                int _nroAparments = 30;//ParameterService.nroGroupUnit;
                decimal _distr = unit.TotalArea / (decimal)2861.9; // ParameterService.TotalAera;

                if (_state.WaterReadings != null )
                     wateritem = _state.WaterReadings.Where(c => c.IdGroupUnit == unit.IdGroupUnit).FirstOrDefault()!;

                foreach (var item in _state.Budget.Details)
                {
                    if (item.IdCategory == idAgua && _state.WaterReadings!.Count > 0 )
                    {
                        _total += wateritem!.CalculatedAmount;
                    }
                    else
                    {
                        _total += item.Type == 1 ? item.MonthlyAmount / _nroAparments : item.MonthlyAmount * _distr;
                    }
                }

                _dpto.Amount = _total;
                _dpto.Percent = 100 * _distr;
                _totalInstallments = _totalInstallments + _total;

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
}


