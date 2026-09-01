using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class CuotaViewModel
    {
        public int Id { get; set; }
        public string Periodo { get; set; } = string.Empty;
        public string MesNombre { get; set; } = string.Empty;
        public int Mes { get; set; }
        public int Anio { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public string DisplayFechaGeneracion => FechaGeneracion.ToString("dd/MM/yyyy HH:mm");
        [Precision(18, 2)]
        public decimal TotalDistribuido { get; set; }
        public string DisplayTotal => TotalDistribuido.ToString("C");
        public string UsuarioGeneracion { get; set; } = string.Empty;
        public bool Procesada { get; set; }
        public string Estado => Procesada ? "Procesada" : "Pendiente";
        public string EstadoColor => Procesada ? "success" : "warning";
        public int TotalDepartamentos { get; set; }
        public int TotalGastos { get; set; }

        // Para filtros
        public DateTime? FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
    }
}
