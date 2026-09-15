namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, Fase 3 -- borrador
    // autogenerado a partir de Meeting+AgendaItem+Attendance+VotingRound (ver
    // IMeetingService.GenerarBorradorMeetingMinutesAsync). Se puede regenerar mientras
    // está en Borrador; una vez Firmada queda inmutable.
    public enum MeetingMinutesStatus
    {
        Borrador = 1,
        Firmada = 2
    }

    public class MeetingMinutes
    {
        public Guid IdMeetingMinutes { get; set; }
        public Guid IdMeeting { get; set; }
        public string ContenidoGenerado { get; set; } = string.Empty;
        public MeetingMinutesStatus Estado { get; set; }

        public string? NombrePresidente { get; set; }
        public DateTime? FirmaPresidenteEn { get; set; }
        public string? NombreSecretario { get; set; }
        public DateTime? FirmaSecretarioEn { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
