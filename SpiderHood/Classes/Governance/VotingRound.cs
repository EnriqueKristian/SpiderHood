using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, Fase 2 -- votación ponderada
    // por alícuota sobre un AgendaItem.Tipo=SujetoAVotacion de una Meeting en
    // curso. Cada intento (ronda) es una fila nueva -- ver
    // IMeetingService.CerrarVotingRoundAsync para la lógica de mayoría y
    // revotación.
    public enum VotingRoundStatus
    {
        Abierta = 1,
        Cerrada = 2
    }

    public enum VoteOption
    {
        AFavor = 1,
        EnContra = 2,
        Abstencion = 3
    }

    public class VotingRound
    {
        public Guid IdVotingRound { get; set; }
        public Guid IdAgendaItem { get; set; }
        public int NroRonda { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public VotingRoundStatus Estado { get; set; }

        [Precision(9, 6)] public decimal? AlicuotaAFavor { get; set; }
        [Precision(9, 6)] public decimal? AlicuotaEnContra { get; set; }
        [Precision(9, 6)] public decimal? AlicuotaAbstencion { get; set; }
        public bool? MayoriaAlcanzada { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        // Poblado del lado C# al componer la VotingRound completa -- no viene de
        // GET_VotingRoundsByAgendaItem.
        [NotMapped] public List<Vote> Votes { get; set; } = new();
    }

    public class Vote
    {
        public Guid IdVote { get; set; }
        public Guid IdVotingRound { get; set; }
        public Guid IdGroupUnit { get; set; }
        public VoteOption Opcion { get; set; }
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public Guid RegistradoPor { get; set; }
        public DateTime FechaVote { get; set; }

        // Resuelto del lado C# (roster/asistentes) -- sólo lectura.
        [NotMapped] public string NombreUnidad { get; set; } = string.Empty;
    }

    public class CloseVotingRoundResult
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool MayoriaAlcanzada { get; set; }
        [Precision(9, 6)] public decimal AlicuotaAFavor { get; set; }
        [Precision(9, 6)] public decimal AlicuotaEnContra { get; set; }
        [Precision(9, 6)] public decimal AlicuotaAbstencion { get; set; }
    }
}
