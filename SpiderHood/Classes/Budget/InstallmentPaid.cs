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
        // También vienen del JOIN a Installment (+ vw_SUM_InstallmentPaid) en
        // GET_InstallmentPaid -- monto total de la cuota y su saldo pendiente A LA FECHA
        // (sumando TODOS los pagos que tenga, no solo este), para poder mostrar en el
        // detalle "de cuánto era la cuota" y "cuánto falta", no solo lo que aportó este pago.
        [Precision(18, 2)]
        public decimal InstallmentAmount { get; set; }
        [Precision(18, 2)]
        public decimal InstallmentDebt { get; set; }
        public DateTime Period { get; set; }

        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        public bool IsAutoReconcile { get; set; } = false;
        public bool IsPartialPayment { get; set; } = false;
    }
}
