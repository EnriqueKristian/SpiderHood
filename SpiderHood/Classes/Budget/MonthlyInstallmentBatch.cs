using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class MonthlyInstallmentBatch
    {
        public int Id { get; set; }
        public int Mes { get; set; }
        public int Anio { get; set; }
        public DateTime FechaGeneracion { get; set; }
        [Precision(18, 2)]
        public decimal TotalDistribuido { get; set; }
        public string UsuarioGeneracion { get; set; } = string.Empty;
        public List<InstallmentBatchDetail> Detalles { get; set; } = [];
        public bool Procesada { get; set; }
    }
}
