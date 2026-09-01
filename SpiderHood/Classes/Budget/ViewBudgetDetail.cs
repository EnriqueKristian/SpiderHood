using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class ViewBudgetDetail
    {
        public Guid IdBudgetDetail { get; set; }
        public Guid IdCategory { get; set; }
        public int IdSection { get; set; }
        [Precision(18, 2)]
        public decimal ItemNumber { get; set; }
        public string Description { get; set; } = string.Empty;
        public string ShortDescrition { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal MonthlyAmount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public int Frequency { get; set; } = 1;
        public int Type { get; set; } = 1;
        public bool IsHeader { get; set; } = false;

        public Guid IdParent { get; set; }
    }
}
