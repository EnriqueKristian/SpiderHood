using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    // ViewModel para Gasto
    public class GastoViewModel
    {
        public int Id { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaGasto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public TipoDistribucion TipoDistribucion { get; set; }
        public bool Pagado { get; set; }
    }
}
