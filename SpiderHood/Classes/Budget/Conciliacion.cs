using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class Conciliacion
    {
        public int Id { get; set; }
        public Guid CuentaBancariaId { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int TransaccionesProcesadas { get; set; }
        public int TransaccionesConciliadas { get; set; }
        [Precision(18, 2)]
        public decimal Diferencia { get; set; }
        public bool Completada { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Usuario { get; set; } = "";
        public string Notas { get; set; } = "";
    }
}
