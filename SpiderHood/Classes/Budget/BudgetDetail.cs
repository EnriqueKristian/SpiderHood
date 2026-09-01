using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class BudgetDetail
    {
        public Guid IdBudgetDetail { get; set; }
        public Guid IdCategory { get; set; }
        public int IdSection { get; set; }
        [Precision(18, 2)]
        public decimal ItemNumber { get; set; }
        public string Description { get; set; } = "";
        [Precision(18, 2)]
        public decimal MonthlyAmount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public int Frequency { get; set; }
        public int Type { get; set; }
        public bool IsHeader { get; set; } = false;
        public Guid IdBudgetHeader { get; set; }
        public bool IsNewItem { get; set; } = false;
        public Guid IdParent { get; set; }
    }
}
