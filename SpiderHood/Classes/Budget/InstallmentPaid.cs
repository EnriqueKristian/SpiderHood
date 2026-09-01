using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class InstallmentPaid
    {
        public Guid IdPaid { get; set; }
        public Guid IdInstallment { get; set; }
        public DateTime PaymentDate { get; set; }
        public Guid IdTransaction { get; set; }

        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        public bool IsAutoReconcile { get; set; } = false;
        public bool IsPartialPayment { get; set; } = false;
    }
}
