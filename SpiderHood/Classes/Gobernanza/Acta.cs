namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, Fase 3 -- borrador
    // autogenerado a partir de Reunion+AgendaItem+Asistencia+Votacion (ver
    // IReunionService.GenerarBorradorActaAsync). Se puede regenerar mientras
    // está en Borrador; una vez Firmada queda inmutable.
    public enum EstadoActa
    {
        Borrador = 1,
        Firmada = 2
    }

    public class Acta
    {
        public Guid IdActa { get; set; }
        public Guid IdReunion { get; set; }
        public string ContenidoGenerado { get; set; } = string.Empty;
        public EstadoActa Estado { get; set; }

        public string? NombrePresidente { get; set; }
        public DateTime? FirmaPresidenteEn { get; set; }
        public string? NombreSecretario { get; set; }
        public DateTime? FirmaSecretarioEn { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
