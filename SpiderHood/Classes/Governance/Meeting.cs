using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21, "Gobernanza -- Meetings,
    // Votación y MeetingMinutes como un solo flujo" -- diseño cerrado 2026-09-11.
    // Fase 1 (esta): Convocatoria + Agenda + Attendance + Quórum + Segunda
    // Convocatoria. Votación (Fase 2) y MeetingMinutes (Fase 3) se construyen sobre
    // esta base -- por eso los puntos "Sujeto a Votación" quedan en
    // Estado=Pendiente hasta que exista la mecánica de voto.
    public enum MeetingStatus
    {
        Convocada = 1,
        EnCurso = 2,
        QuorumNoAlcanzado = 3,
        Finalizada = 4,
        Cancelada = 5
    }

    public enum MeetingType
    {
        Ordinaria = 1,
        Extraordinaria = 2
    }

    public enum MeetingModality
    {
        Presencial = 1,
        Virtual = 2,
        Hibrida = 3
    }

    public enum AgendaItemType
    {
        Informativo = 1,
        SujetoAVotacion = 2
    }

    public enum AgendaVotingType
    {
        Nominal = 1,
        Secreta = 2
    }

    // Legal75 = el 75% fijado por el Art. 14.1 del D.L. 1568 para
    // desafectar bienes comunes -- no configurable. Simple/Calificada
    // quedan delegados al Reglamento Interno de cada edificio.
    public enum MajorityType
    {
        Simple = 1,
        Calificada = 2,
        Legal75 = 3
    }

    public enum AgendaItemStatus
    {
        Pendiente = 1,
        Informado = 2,
        Aprobado = 3,
        Rechazado = 4
    }

    public class Meeting
    {
        public Guid IdMeeting { get; set; }
        public Guid IdBuilding { get; set; }
        public MeetingType Tipo { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public DateTime FechaConvocatoria { get; set; }
        public DateTime FechaMeeting { get; set; }
        public MeetingModality Modalidad { get; set; }
        public string? LugarOVinculo { get; set; }

        [Precision(9, 6)] public decimal QuorumRequerido { get; set; }
        [Precision(9, 6)] public decimal? QuorumAlcanzado { get; set; }

        // Fijada una sola vez por IniciarMeetingAsync al pasar a EnCurso -- distinta de
        // FechaMeeting (la hora programada) porque la reunión puede arrancar antes o
        // después. Se usa para calcular la ventana de asistencia tardía.
        public DateTime? FechaInicioReal { get; set; }

        // Minutos desde FechaInicioReal durante los que todavía se puede registrar
        // asistencia con la reunión En Curso (a criterio de quien la dirige) -- pasado
        // ese límite, RegistrarAttendanceAsync la rechaza igual que antes.
        public int MinutosLimiteAsistenciaTardia { get; set; } = 20;

        public MeetingStatus Estado { get; set; }
        public Guid? IdMeetingOrigen { get; set; }
        public Guid? IdCalendarItem { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? UpdatedOn { get; set; }

        // Resuelto por el JOIN de GET_Meetings* -- sólo lectura.
        public string CreatedByName { get; set; } = string.Empty;

        // Poblados del lado C# al componer una Meeting completa (ver
        // IMeetingService.GetMeetingByIdAsync) -- no vienen de GET_MeetingById.
        [NotMapped] public List<AgendaItem> Agenda { get; set; } = new();
        [NotMapped] public List<Attendance> Asistentes { get; set; } = new();
    }

    public class AgendaItem
    {
        public Guid IdAgendaItem { get; set; }
        public Guid IdMeeting { get; set; }
        public int Orden { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public AgendaItemType Tipo { get; set; }
        public AgendaVotingType? VotingType { get; set; }
        public MajorityType? MajorityType { get; set; }
        [Precision(9, 6)] public decimal? PorcentajeMayoriaCalificada { get; set; }
        public bool PermiteRevotacion { get; set; }
        public AgendaItemStatus Estado { get; set; }
        public DateTime CreatedOn { get; set; }

        // Notas de quien dirige la reunión sobre lo conversado en este punto --
        // separadas de Descripcion (que se fija al crear el punto, antes de la
        // reunión). Editable mientras el MeetingMinutes no esté Firmada (ver
        // IMeetingService.ActualizarNotasAgendaItemAsync); se incluye en el Acta.
        public string? Notas { get; set; }
    }

    public class Attendance
    {
        public Guid IdAttendance { get; set; }
        public Guid IdMeeting { get; set; }
        public Guid IdGroupUnit { get; set; }
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public Guid RegistradoPor { get; set; }
        public DateTime FechaRegistro { get; set; }

        // Resuelto del lado C# (roster de OwnerUnitView) -- sólo lectura, no
        // viene de GET_AttendancesByMeeting.
        [NotMapped] public string NombreUnidad { get; set; } = string.Empty;
    }

    // Roster de unidades de un edificio con su alícuota ya calculada -- lo
    // que consume la pantalla de "Registrar Attendance" para elegir a
    // quién marcar presente.
    public class UnitWithShare
    {
        public Guid IdGroupUnit { get; set; }
        public string NombreUnidad { get; set; } = string.Empty;
        [Precision(9, 6)] public decimal Alicuota { get; set; }
        public bool Presente { get; set; }
    }

    public class ConvokeMeetingResult
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdMeeting { get; set; }
    }

    public class StartMeetingResult
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public bool QuorumAlcanzado { get; set; }
        [Precision(9, 6)] public decimal PorcentajeAlcanzado { get; set; }
    }
}
