using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class InstallmentPaid
    {
        public Guid IdPaid { get; set; }
        public Guid IdInstallment { get; set; }
        public DateTime PaymentDate { get; set; }
        public Guid IdTransaction { get; set; }
        // Vienen por JOIN a Installment en GET_InstallmentPaid -- la cuota puede ya
        // no estar en GetPendingInstallmentsAsync (queda pagada), así que la pantalla
        // de detalle de conciliación no tiene otra forma de mostrar propietario/unidad
        // para pagos ya conciliados.
        public string UnitName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;

        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        public bool IsAutoReconcile { get; set; } = false;
        public bool IsPartialPayment { get; set; } = false;
    }
}
