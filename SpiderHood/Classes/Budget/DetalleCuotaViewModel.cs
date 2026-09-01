using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    // ViewModel para Detalle Cuota
    public class DetalleCuotaViewModel
    {
        public int Id { get; set; }
        public int CuotaId { get; set; }
        public int DepartamentoId { get; set; }
        public string DepartamentoNombre { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal AreaM2 { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeArea { get; set; }
        public int GastoId { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public string DescripcionGasto { get; set; } = string.Empty;
        public TipoDistribucion TipoDistribucion { get; set; }
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public bool Pagado { get; set; }
    }
}
