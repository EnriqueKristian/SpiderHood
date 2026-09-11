namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #17 -- diseño cerrado 2026-09-11.
    // Sin "Público Global" (SysAdmin a todos los edificios, feature de
    // plataforma, no de condominio) ni mensajería vecino-a-vecino (se
    // descartó explícitamente: "conllevaría a un chat y no es el objetivo").
    public enum AlcanceComunicado
    {
        Publico = 1,   // Administrador o Junta -> todo el edificio
        Reservado = 2, // Administrador o Junta -> un rol específico (RolReservado)
        Privado = 3    // Administrador o Junta -> una o varias unidades puntuales
    }

    // Estado de entrega por canal y por destinatario -- separado en dos
    // enums (WhatsApp/Correo) porque un mismo comunicado puede intentar
    // ambos canales por destinatario, con resultados distintos.
    public enum EstadoEnvioWhatsApp
    {
        NoAplica = 0,
        Simulado = 1,  // Twilio:Simulate (sin credenciales reales) -- ver WhatsAppService
        Enviado = 2,
        Fallido = 3
    }

    public enum EstadoEnvioCorreo
    {
        NoAplica = 0,
        Enviado = 1,
        Fallido = 2
    }

    // Cabecera -- qué se publicó, con qué alcance, quién lo publicó. Mismo
    // patrón cabecera+detalle que BudgetHeader/Installment.
    public class Comunicado
    {
        public Guid IdComunicado { get; set; }
        public Guid IdBuilding { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Cuerpo { get; set; } = string.Empty;

        // Parameter.IdTabla del grupo "Categoría de Comunicado" -- define qué
        // plantilla de WhatsApp usa (ver Docs/Pendientes-Negocio-Consolidado.md #17).
        public int IdCategoria { get; set; }

        public AlcanceComunicado Alcance { get; set; }

        // Sólo tiene valor cuando Alcance == Reservado (Administrador/Junta/Residente).
        public string? RolReservado { get; set; }

        public bool EnviarPorCorreo { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }

        // Los siguientes dos campos vienen resueltos por el JOIN de
        // GET_ComunicadosByBuilding/GET_ComunicadosParaUsuario -- no se
        // guardan, sólo se leen.
        public string CreatedByName { get; set; } = string.Empty;
        public int TotalDestinatarios { get; set; }
    }

    // Detalle -- a quién se le mandó y qué pasó con el envío por cada canal.
    public class ComunicadoDestinatario
    {
        public Guid IdComunicadoDestinatario { get; set; }
        public Guid IdComunicado { get; set; }

        // Null para un destinatario resuelto por rol (Reservado) sin unidad
        // asociada (ej. un Administrador no tiene GroupUnit propio).
        public Guid? IdGroupUnit { get; set; }

        public string NombreDestinatario { get; set; } = string.Empty;
        public string? Telefono { get; set; }
        public string? Email { get; set; }

        public EstadoEnvioWhatsApp EstadoWhatsApp { get; set; }
        public EstadoEnvioCorreo EstadoCorreo { get; set; }
        public DateTime FechaEnvio { get; set; }
    }

    // Resultado de PublicarComunicadoAsync -- para mostrarle a quien publica
    // cuántos destinatarios recibieron el comunicado y por qué canal, sin
    // tener que ir a mirar la consola del servidor (mismo espíritu que
    // CuotaExtraordinariaResultado).
    public class PublicarComunicadoResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdComunicado { get; set; }
        public int TotalDestinatarios { get; set; }
        public int EnviadosWhatsApp { get; set; }
        public int SimuladosWhatsApp { get; set; }
        public int FallidosWhatsApp { get; set; }
        public int EnviadosCorreo { get; set; }
        public int FallidosCorreo { get; set; }
    }
}
