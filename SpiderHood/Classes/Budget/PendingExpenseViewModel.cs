using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class PendingExpenseViewModel
    {
        public Guid Id { get; set; }
        public string CategoriaNombre { get; set; } = string.Empty;
        public int CategoriaId { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Monto { get; set; }
        public DateTime FechaGasto { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public TipoDistribucion TipoDistribucion { get; set; }
        public bool Seleccionado { get; set; }
        public bool Pagado { get; set; }
        public bool ConsideradoEnCuota { get; set; }
        public string Observaciones { get; set; } = string.Empty;

        // Propiedades calculadas
        public string DisplayFecha => FechaGasto.ToString("dd/MM/yyyy");
        public string DisplayMonto => Monto.ToString("C");
        public string DisplayTipoDistribucion => TipoDistribucion.ToString();
        public string EstadoColor => Pagado ? "success" : ConsideradoEnCuota ? "warning" : "danger";
        public string EstadoTexto => Pagado ? "Pagado" : ConsideradoEnCuota ? "En Cuota" : "Pendiente";
    }
}
