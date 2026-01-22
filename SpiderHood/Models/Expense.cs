using Microsoft.EntityFrameworkCore;

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

}
