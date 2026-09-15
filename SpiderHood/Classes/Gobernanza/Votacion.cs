using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, Fase 2 -- votación ponderada
    // por alícuota sobre un AgendaItem.Tipo=SujetoAVotacion de una Reunion en
    // curso. Cada intento (ronda) es una fila nueva -- ver
    // IReunionService.CerrarVotacionAsync para la lógica de mayoría y
    // revotación.
    public enum EstadoVotacion
    {
        Abierta = 1,
        Cerrada = 2
    }

    public enum OpcionVoto
    {
        AFavor = 1,
        EnContra = 2,
        Abstencion = 3
    }

    public class Votacion
    {
        public Guid IdVotacion { get; set; }
        public Guid IdAgendaItem { get; set; }
        public int NroRonda { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime? FechaFin { get; set; }
        public EstadoVotacion Estado { get; set; }

        [Precision(9, 6)] public decimal? AlicuotaAFavor { get; set; }
        [Precision(9, 6)] public decimal? AlicuotaEnContra { get; set; }
        [Precision(9, 6)] public decimal? AlicuotaAbstencion { get; set; }
        public bool? MayoriaAlcanzada { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        // Poblado del lado C# al componer la Votacion completa -- no viene de
        // GET_VotacionesByAgendaItem.
        [NotMapped] public List<Voto> Votos { get; set; } = new();
    }

    public class Voto
    {
        public Guid IdVoto { get; set; }
        public Guid IdVotacion { get; set; }
        public Guid IdGroupUnit { get; set; }
        public OpcionVoto Opcion { get; set; }
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public Guid RegistradoPor { get; set; }
        public DateTime FechaVoto { get; set; }

        // Resuelto del lado C# (roster/asistentes) -- sólo lectura.
        [NotMapped] public string NombreUnidad { get; set; } = string.Empty;
    }

    public class CerrarVotacionResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool MayoriaAlcanzada { get; set; }
        [Precision(9, 6)] public decimal AlicuotaAFavor { get; set; }
        [Precision(9, 6)] public decimal AlicuotaEnContra { get; set; }
        [Precision(9, 6)] public decimal AlicuotaAbstencion { get; set; }
    }
}
