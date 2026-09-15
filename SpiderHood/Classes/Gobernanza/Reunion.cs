using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, "Gobernanza -- Reuniones,
    // Votación y Actas como un solo flujo" -- diseño cerrado 2026-09-11.
    // Fase 1 (esta): Convocatoria + Agenda + Asistencia + Quórum + Segunda
    // Convocatoria. Votación (Fase 2) y Actas (Fase 3) se construyen sobre
    // esta base -- por eso los puntos "Sujeto a Votación" quedan en
    // Estado=Pendiente hasta que exista la mecánica de voto.
    public enum EstadoReunion
    {
        Convocada = 1,
        EnCurso = 2,
        QuorumNoAlcanzado = 3,
        Finalizada = 4,
        Cancelada = 5
    }

    public enum TipoReunion
    {
        Ordinaria = 1,
        Extraordinaria = 2
    }

    public enum ModalidadReunion
    {
        Presencial = 1,
        Virtual = 2,
        Hibrida = 3
    }

    public enum TipoAgendaItem
    {
        Informativo = 1,
        SujetoAVotacion = 2
    }

    public enum TipoVotacionAgenda
    {
        Nominal = 1,
        Secreta = 2
    }

    // Legal75 = el 75% fijado por el Art. 14.1 del D.L. 1568 para
    // desafectar bienes comunes -- no configurable. Simple/Calificada
    // quedan delegados al Reglamento Interno de cada edificio.
    public enum TipoMayoria
    {
        Simple = 1,
        Calificada = 2,
        Legal75 = 3
    }

    public enum EstadoAgendaItem
    {
        Pendiente = 1,
        Informado = 2,
        Aprobado = 3,
        Rechazado = 4
    }

    public class Reunion
    {
        public Guid IdReunion { get; set; }
        public Guid IdBuilding { get; set; }
        public TipoReunion Tipo { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime FechaConvocatoria { get; set; }
        public DateTime FechaReunion { get; set; }
        public ModalidadReunion Modalidad { get; set; }
        public string? LugarOVinculo { get; set; }

        [Precision(9, 6)] public decimal QuorumRequerido { get; set; }
        [Precision(9, 6)] public decimal? QuorumAlcanzado { get; set; }

        public EstadoReunion Estado { get; set; }
        public Guid? IdReunionOrigen { get; set; }
        public Guid? IdCalendarItem { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }

        // Resuelto por el JOIN de GET_Reuniones* -- sólo lectura.
        public string CreatedByName { get; set; } = string.Empty;

        // Poblados del lado C# al componer una Reunion completa (ver
        // IReunionService.GetReunionByIdAsync) -- no vienen de GET_ReunionById.
        [NotMapped] public List<AgendaItem> Agenda { get; set; } = new();
        [NotMapped] public List<Asistencia> Asistentes { get; set; } = new();
    }

    public class AgendaItem
    {
        public Guid IdAgendaItem { get; set; }
        public Guid IdReunion { get; set; }
        public int Orden { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public TipoAgendaItem Tipo { get; set; }
        public TipoVotacionAgenda? TipoVotacion { get; set; }
        public TipoMayoria? TipoMayoria { get; set; }
        [Precision(9, 6)] public decimal? PorcentajeMayoriaCalificada { get; set; }
        public bool PermiteRevotacion { get; set; }
        public EstadoAgendaItem Estado { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class Asistencia
    {
        public Guid IdAsistencia { get; set; }
        public Guid IdReunion { get; set; }
        public Guid IdGroupUnit { get; set; }
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public Guid RegistradoPor { get; set; }
        public DateTime FechaRegistro { get; set; }

        // Resuelto del lado C# (roster de OwnerUnitView) -- sólo lectura, no
        // viene de GET_AsistenciasByReunion.
        [NotMapped] public string NombreUnidad { get; set; } = string.Empty;
    }

    // Roster de unidades de un edificio con su alícuota ya calculada -- lo
    // que consume la pantalla de "Registrar Asistencia" para elegir a
    // quién marcar presente.
    public class UnidadConAlicuota
    {
        public Guid IdGroupUnit { get; set; }
        public string NombreUnidad { get; set; } = string.Empty;
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public bool Presente { get; set; }
    }

    public class ConvocarReunionResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdReunion { get; set; }
    }

    public class IniciarReunionResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool QuorumAlcanzado { get; set; }
        [Precision(9, 6)] public decimal PorcentajeAlcanzado { get; set; }
    }
}
