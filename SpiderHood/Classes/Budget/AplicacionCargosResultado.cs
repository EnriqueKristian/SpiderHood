namespace SpiderHood.Models
{
    // Resultado de correr el proceso de Multas y Mora: qué se generó en esta corrida
    // (no es un acumulado histórico, solo lo que se creó ahora).
    public class AplicacionCargosResultado
    {
        public bool Exito { get; set; } = true;
        public string Mensaje { get; set; } = string.Empty;
        public int CuotasRevisadas { get; set; }
        public int UnidadesConMulta { get; set; }
        public int UnidadesConMora { get; set; }
        public decimal TotalMultas { get; set; }
        public decimal TotalMora { get; set; }
        public List<string> Detalle { get; set; } = [];
    }
}
