using BlazorBootstrap;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Expense
    {
        public Guid IdExpense { get; set; }
        public string Category { get; set; } = null!;
        public string SubCategory { get; set; } = null!;
        public string ExpenseDescription { get; set; } = null!;
        public DateTime DueDate { get; set; }
        public TypeDistribution IdDistribution { get; set; }
        public string Distribution { get; set; } = null!;
        [Precision(18, 2)]
        public decimal TotalAmount { get; set; }
        public Guid IdBuilding { get; set; }
        public Guid IdCategory { get; set; }
        public Guid IdSubCategory { get; set; }

        public int Month => DueDate.Month;
        public int Year => DueDate.Year;

        public string FilterbyMonth => $"{Year}-{Month:D2}";
        public string FullCategory => $"{Category} - {SubCategory}";
        public bool IsOverdue => DueDate < DateTime.Today;
        public bool Pagado { get; set; }
        public string Observaciones { get; set; } = string.Empty;
        public bool IsReconciled  { get; set; } 
        public string Provider { get; set; } = string.Empty;
        public Guid? IdMovDetail { get; set; }
    }

    public class BudgetSumCategory
    {
        public Guid IdBudgetHeader { get; set; }
        public Guid IdParent { get; set; }
        public decimal SumCategory { get; set; }
    }

    public class Period
    {
        public Guid IdPeriod { get; set; }
        public Guid IdBuilding { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PeriodType { get; set; } 
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime ClosingDate { get; set; }
        public int Status { get; set; } 
        public bool IsCurrentPeriod { get; set; }
        [NotMapped]
        public string Description { get; set; } = string.Empty;
        [NotMapped]
        public bool IsNewPeriod { get; set; } = false;

        // Navigation properties
        [NotMapped]
        public Building? Building { get; set; }
        public ICollection<ServiceReading>? Readings { get; set; }
        //public ICollection<Fee> Fees { get; set; }
        //public ICollection<CommonExpense> Expenses { get; set; }
        public ICollection<BudgetHeader>? Budgets { get; set; }
    }


}
