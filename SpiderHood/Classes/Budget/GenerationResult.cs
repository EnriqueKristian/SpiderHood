using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class GenerationResult
    {
        public bool Exito { get; set; }
        public int CuotaId { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal TotalGenerado { get; set; }
        public int TotalDepartamentos { get; set; }
        public int TotalGastosIncluidos { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public Dictionary<string, decimal> ResumenPorCategoria { get; set; } = [];
        public List<string> Errores { get; set; } = [];
        public List<string> Advertencias { get; set; } = [];

        public static GenerationResult Exitoso(int cuotaId, string mensaje = "Cuota generada exitosamente")
        {
            return new GenerationResult
            {
                Exito = true,
                CuotaId = cuotaId,
                Mensaje = mensaje,
                FechaGeneracion = DateTime.Now
            };
        }

        public static GenerationResult Error(string mensaje, List<string>? errores = null)
        {
            return new GenerationResult
            {
                Exito = false,
                Mensaje = mensaje,
                FechaGeneracion = DateTime.Now,
                Errores = errores ?? []
            };
        }

        public void AgregarError(string error)
        {
            Errores.Add(error);
            Exito = false;
        }

        public void AgregarAdvertencia(string advertencia)
        {
            Advertencias.Add(advertencia);
        }
    }
}
