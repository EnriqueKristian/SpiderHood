using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class ResumenCuota
    {
        public int CuotaId { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public int TotalDepartamentos { get; set; }
        public int TotalGastos { get; set; }
        [Precision(18, 2)]
        public decimal TotalMonto { get; set; }
        [Precision(18, 2)]
        public decimal PromedioPorDepartamento { get; set; }
        public Dictionary<string, decimal>? DistribucionPorCategoria { get; set; }
        public Dictionary<string, decimal>? DistribucionPorDepartamento { get; set; }
        public List<GastoResumen>? GastosPrincipales { get; set; }
    }
}
