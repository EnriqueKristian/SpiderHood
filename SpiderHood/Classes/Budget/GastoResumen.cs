using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class GastoResumen
    {
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeDelTotal { get; set; }
    }
}
