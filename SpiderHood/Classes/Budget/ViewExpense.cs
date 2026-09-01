using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class ViewExpense
    {
        public Guid IdExpense { get; set; }
        public string Description { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public Guid IdCategory { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public PaymentMethod PaymentMethod { get; set; }
        public StatusExpense Status { get; set; }  // Pending, Approved, Rejected
        public bool Reconciled { get; set; } = false;
        public Guid? ReconciledTransactionId { get; set; }
        public TypeDistribution? Distribution { get; set; }
        public Guid? IdBuilding { get; set; }
        public string Notes { get; set; } = string.Empty;
        //public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool AutoReconcile { get; set; } = true;
        public DateTime ExpenseDate { get; set; }
        public bool IncludeInQuota { get; set; }
        //public DateTime? PaymentDate { get; set; }
    }
}
