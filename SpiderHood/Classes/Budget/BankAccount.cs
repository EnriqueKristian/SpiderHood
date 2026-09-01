using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class BankAccount
    {
        public Guid IdBankAccount { get; set; }
        public string AccountName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string CCI { get; set; } = "";
        public string BankName { get; set; } = "";
        public int AccountType { get; set; }  // Ahorros, Corriente, etc.
        [Precision(18, 2)]
        public decimal CurrentBalance { get; set; }
        [Precision(18, 2)]
        public decimal ReconciledBalance { get; set; }
        public DateTime LastReconciliation { get; set; }
        public int Status { get; set; }
        public Guid IdBuilding { get; set; }

        public BankAccount Clone() => (BankAccount)this.MemberwiseClone();
    }
}
