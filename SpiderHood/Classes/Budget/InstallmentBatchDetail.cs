using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class InstallmentBatchDetail
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public int DepartamentoId { get; set; }
        public Guid GastoId { get; set; }
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public bool Pagado { get; set; }
    }
}
