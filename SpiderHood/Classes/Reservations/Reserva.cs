using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #21 -- diseño cerrado 2026-09-11.
    // Flujo: Pendiente -> (Junta) Aprobada/Rechazada -> Cancelada/NoPresentado
    // (penalidad según config del Área Común) -> Entregada (check-in) ->
    // Finalizada (check-out) -> Cerrada (garantía liquidada). El Administrador
    // hace el check-in/check-out; la Junta aprueba/rechaza.
    public enum ReservaEstado
    {
        PendienteDeAprobacion = 1,
        Aprobada = 2,
        Rechazada = 3,
        Cancelada = 4,
        NoPresentado = 5,
        Entregada = 6,
        Finalizada = 7,
        Cerrada = 8
    }

    public enum ChecklistEtapa
    {
        Entrega = 1,
        Devolucion = 2
    }

    public enum ChecklistEstado
    {
        Ok = 1,
        Danado = 2,
        Falta = 3
    }

    // Configuración por edificio -- vive fuera de BuildingConfiguration a
    // propósito (tabla y persistencia propias, no se mete en el Clone()/UPD
    // gigante de BuildingConfiguration). Se administra desde una pestaña
    // nueva "Áreas Comunes" en BuildingConfig (item #24).
    public class AreaComun
    {
        public Guid IdAreaComun { get; set; }
        public Guid IdBuilding { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public int? AforoMaximo { get; set; }
        public int? DuracionMinMinutos { get; set; }
        public int? DuracionMaxMinutos { get; set; }
        public int BufferMinutos { get; set; }
        public int AnticipacionMinHoras { get; set; }
        public int? AnticipacionMaxDias { get; set; }
        public int? TopeReservasActivasPorUnidad { get; set; }

        // Garantía/Alquiler como par {Internos, Externos} -- decisión cerrada
        // 2026-09-11: la garantía de un externo suele ser mucho mayor que la
        // de un propietario. Limpieza queda como un solo monto.
        [Precision(18, 2)] public decimal GarantiaInternos { get; set; }
        [Precision(18, 2)] public decimal GarantiaExternos { get; set; }
        [Precision(18, 2)] public decimal AlquilerInternos { get; set; }
        [Precision(18, 2)] public decimal AlquilerExternos { get; set; }
        [Precision(18, 2)] public decimal Limpieza { get; set; }

        // Penalidad -- dos toggles independientes, apagados por default.
        // Ambos retienen la garantía completa cuando aplican (sin % parcial
        // configurable en esta primera versión).
        public bool PenalidadCancelacionHabilitada { get; set; }
        public int? DiasMinimosSinPenalidad { get; set; }
        public bool PenalidadNoPresentadoHabilitada { get; set; }

        public bool Activo { get; set; } = true;
        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        public AreaComun Clone() => new()
        {
            IdAreaComun = IdAreaComun,
            IdBuilding = IdBuilding,
            Nombre = Nombre,
            Descripcion = Descripcion,
            AforoMaximo = AforoMaximo,
            DuracionMinMinutos = DuracionMinMinutos,
            DuracionMaxMinutos = DuracionMaxMinutos,
            BufferMinutos = BufferMinutos,
            AnticipacionMinHoras = AnticipacionMinHoras,
            AnticipacionMaxDias = AnticipacionMaxDias,
            TopeReservasActivasPorUnidad = TopeReservasActivasPorUnidad,
            GarantiaInternos = GarantiaInternos,
            GarantiaExternos = GarantiaExternos,
            AlquilerInternos = AlquilerInternos,
            AlquilerExternos = AlquilerExternos,
            Limpieza = Limpieza,
            PenalidadCancelacionHabilitada = PenalidadCancelacionHabilitada,
            DiasMinimosSinPenalidad = DiasMinimosSinPenalidad,
            PenalidadNoPresentadoHabilitada = PenalidadNoPresentadoHabilitada,
            Activo = Activo,
            CreatedBy = CreatedBy,
            CreatedOn = CreatedOn
        };
    }

    public class Reserva
    {
        public Guid IdReserva { get; set; }
        public Guid IdBuilding { get; set; }
        public Guid IdAreaComun { get; set; }

        // Propietario responsable -- siempre obligatorio, incluso si la
        // reserva es para un evento externo (respalda financiera y
        // legalmente el checklist de daños).
        public Guid IdGroupUnit { get; set; }

        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }

        public bool EsExterno { get; set; }
        public string? OrganizadorNombre { get; set; }
        public string? OrganizadorDocumento { get; set; }
        public string? OrganizadorTelefono { get; set; }

        public ReservaEstado Estado { get; set; }
        [Precision(18, 2)] public decimal MontoGarantia { get; set; }
        [Precision(18, 2)] public decimal MontoAlquiler { get; set; }
        [Precision(18, 2)] public decimal MontoLimpieza { get; set; }
        [Precision(18, 2)] public decimal? MontoRetenido { get; set; }
        public string? MotivoRechazo { get; set; }
        public Guid? AprobadoPor { get; set; }
        public DateTime? FechaAprobacion { get; set; }

        // CalendarItem creado junto con la Reserva -- feedback del usuario tras probar
        // en vivo (2026-09-11): sin esto la Reserva no aparecía en el Calendario general,
        // así que otro propietario no veía que el área ya estaba comprometida para ese
        // horario. Se borra si la Reserva se Rechaza/Cancela/marca NoPresentado.
        public Guid? IdCalendarItem { get; set; }

        // Confirmación manual de pago -- no hay pasarela de pago ni conciliación
        // bancaria conectada todavía (Docs/Pendientes-Negocio-Consolidado.md #21,
        // "Cobro"). El Administrador tilda esto a mano antes de poder hacer
        // check-in (ver IReservaService.HacerCheckInAsync) -- feedback del usuario
        // 2026-09-12.
        public bool PagoConfirmado { get; set; }
        [Precision(18, 2)] public decimal? MontoPagoConfirmado { get; set; }
        public DateTime? FechaPagoConfirmado { get; set; }
        public Guid? PagoConfirmadoPor { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        // Resueltos por el JOIN de los SPs GET_Reservas* -- sólo lectura.
        public string NombreAreaComun { get; set; } = string.Empty;
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class ReservaChecklistItem
    {
        public Guid IdChecklistItem { get; set; }
        public Guid IdReserva { get; set; }
        public ChecklistEtapa Etapa { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public ChecklistEstado Estado { get; set; }
        public string? Observacion { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class ReservaAttachment
    {
        public Guid IdAttachment { get; set; }
        public Guid IdReserva { get; set; }
        public ChecklistEtapa Etapa { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int FileSizeBytes { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public Guid UploadedBy { get; set; }
        public DateTime UploadedOn { get; set; }
    }

    // Registro simple de Alquiler/Limpieza/Garantía retenida -- ver la nota en
    // Database/Scripts/2026-09-11_98_Reserva.sql: NO conectado todavía al
    // Reporte de Ingresos y Egresos (100% conciliación bancaria hoy).
    public class IngresoComunidad
    {
        public Guid IdIngreso { get; set; }
        public Guid IdBuilding { get; set; }
        public string Concepto { get; set; } = string.Empty;
        [Precision(18, 2)] public decimal Monto { get; set; }
        public Guid? IdReserva { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }

    public class SolicitarReservaResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdReserva { get; set; }
    }

    public class CheckInResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class CerrarReservaResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public decimal MontoDevuelto { get; set; }
        public decimal MontoRetenido { get; set; }
        public bool SeGeneroCuotaExtraordinaria { get; set; }
        public decimal MontoCuotaExtraordinaria { get; set; }
    }
}
