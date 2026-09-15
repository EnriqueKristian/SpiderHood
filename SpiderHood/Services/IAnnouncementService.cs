using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #17 -- diseño cerrado 2026-09-11.
    // Módulo chico a propósito: 3 alcances (Público/Reservado/Privado), sin
    // mensajería vecino-a-vecino ni "Público Global" de SysAdmin.
    public interface IAnnouncementService
    {
        Task<List<Announcement>> GetAnnouncementsAsync(Guid idBuilding);

        Task<List<AnnouncementRecipient>> GetRecipientsAsync(Guid idAnnouncement);

        // Announcements visibles para un residente/rol puntual dentro de un edificio --
        // Públicos siempre, Reservados si el rol matchea, Privados si su unidad
        // aparece como destinatario.
        Task<List<Announcement>> GetAnnouncementsParaUsuarioAsync(Guid idBuilding, string rolUsuario, Guid? idGroupUnit);

        // Resuelve destinatarios según el Alcance, crea el Announcement + sus
        // AnnouncementRecipient, y manda por WhatsApp (siempre, en modo Simulado si
        // no hay credenciales reales de Twilio) y por correo (sólo si
        // comunicado.EnviarPorCorreo está marcado). idGroupUnitsPrivado sólo se usa
        // cuando Alcance == Privado.
        Task<PublishAnnouncementResult> PublicarAnnouncementAsync(Announcement comunicado, List<Guid>? idGroupUnitsPrivado);
    }

    public class AnnouncementService : IAnnouncementService
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AnnouncementService> _logger;
        private BDLayout ec { get; set; }

        public AnnouncementService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IWhatsAppService whatsAppService,
            IEmailService emailService,
            ILogger<AnnouncementService> logger)
        {
            _whatsAppService = whatsAppService;
            _emailService = emailService;
            _logger = logger;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Announcement>> GetAnnouncementsAsync(Guid idBuilding)
            => await ec.GetAnnouncementsByBuildingAsync(idBuilding);

        public async Task<List<AnnouncementRecipient>> GetRecipientsAsync(Guid idAnnouncement)
            => await ec.GetAnnouncementRecipientsAsync(idAnnouncement);

        public async Task<List<Announcement>> GetAnnouncementsParaUsuarioAsync(Guid idBuilding, string rolUsuario, Guid? idGroupUnit)
            => await ec.GetAnnouncementsParaUsuarioAsync(idBuilding, rolUsuario, idGroupUnit);

        public async Task<PublishAnnouncementResult> PublicarAnnouncementAsync(Announcement comunicado, List<Guid>? idGroupUnitsPrivado)
        {
            var resultado = new PublishAnnouncementResult();

            if (string.IsNullOrWhiteSpace(comunicado.Titulo) || string.IsNullOrWhiteSpace(comunicado.Cuerpo))
            {
                resultado.Mensaje = "Título y Cuerpo son obligatorios.";
                return resultado;
            }

            if (comunicado.Alcance == AnnouncementScope.Reservado && string.IsNullOrWhiteSpace(comunicado.RolReservado))
            {
                resultado.Mensaje = "Seleccione a qué rol le llega el comunicado.";
                return resultado;
            }

            if (comunicado.Alcance == AnnouncementScope.Privado && (idGroupUnitsPrivado == null || !idGroupUnitsPrivado.Any()))
            {
                resultado.Mensaje = "Seleccione al menos una unidad para un comunicado Privado.";
                return resultado;
            }

            try
            {
                var destinatarios = await ResolverRecipientsAsync(comunicado, idGroupUnitsPrivado);
                if (!destinatarios.Any())
                {
                    resultado.Mensaje = "No se encontró ningún destinatario para el alcance seleccionado.";
                    return resultado;
                }

                comunicado.IdAnnouncement = Guid.NewGuid();
                await ec.AddNewRecordAsync(comunicado);

                foreach (var destinatario in destinatarios)
                {
                    destinatario.IdAnnouncementRecipient = Guid.NewGuid();
                    destinatario.IdAnnouncement = comunicado.IdAnnouncement;

                    await EnviarPorWhatsAppAsync(destinatario, comunicado);
                    resultado.TotalRecipients++;
                    switch (destinatario.EstadoWhatsApp)
                    {
                        case WhatsAppDeliveryStatus.Enviado: resultado.EnviadosWhatsApp++; break;
                        case WhatsAppDeliveryStatus.Simulado: resultado.SimuladosWhatsApp++; break;
                        case WhatsAppDeliveryStatus.Fallido: resultado.FallidosWhatsApp++; break;
                    }

                    if (comunicado.EnviarPorCorreo)
                    {
                        await EnviarPorCorreoAsync(destinatario, comunicado);
                        switch (destinatario.EstadoCorreo)
                        {
                            case EmailDeliveryStatus.Enviado: resultado.EnviadosCorreo++; break;
                            case EmailDeliveryStatus.Fallido: resultado.FallidosCorreo++; break;
                        }
                    }

                    await ec.AddNewRecordAsync(destinatario);
                }

                resultado.Exito = true;
                resultado.IdAnnouncement = comunicado.IdAnnouncement;
                resultado.Mensaje = "Comunicado publicado.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al publicar comunicado");
                resultado.Exito = false;
                resultado.Mensaje = $"Error al publicar el comunicado: {ex.Message}";
            }

            return resultado;
        }

        // Público/Privado resuelven contra los propietarios titulares del edificio
        // (mismo filtro Role==1 && TypeUnit==1 que ya usa IExtraChargeService.
        // GetUnitsAsync para cuotas extraordinarias) -- Reservado resuelve contra
        // UserBuildingAssociation (rol de portal, no el rol de propietario dentro de
        // la unidad) y pide el teléfono aparte por usuario (aceptable: la audiencia de
        // un Reservado suele ser chica, ej. sólo la Junta).
        private async Task<List<AnnouncementRecipient>> ResolverRecipientsAsync(Announcement comunicado, List<Guid>? idGroupUnitsPrivado)
        {
            var destinatarios = new List<AnnouncementRecipient>();

            if (comunicado.Alcance == AnnouncementScope.Publico || comunicado.Alcance == AnnouncementScope.Privado)
            {
                var unidades = (await ec.GetOwnersByBuildingAsync(comunicado.IdBuilding))
                    .Where(u => u.Role == 1 && u.TypeUnit == 1);

                if (comunicado.Alcance == AnnouncementScope.Privado)
                {
                    var idsSeleccionados = idGroupUnitsPrivado!.ToHashSet();
                    unidades = unidades.Where(u => idsSeleccionados.Contains(u.IdGroupUnit));
                }

                destinatarios.AddRange(unidades.Select(u => new AnnouncementRecipient
                {
                    IdGroupUnit = u.IdGroupUnit,
                    NombreRecipient = $"{u.FirstName} {u.LastName}".Trim(),
                    Telefono = u.PhoneNumber,
                    Email = u.Email
                }));
            }
            else if (comunicado.Alcance == AnnouncementScope.Reservado)
            {
                var asignaciones = (await ec.GetAllUserBuildingRolesAsync())
                    .Where(a => a.IdBuilding == comunicado.IdBuilding && a.IsApproved
                        && string.Equals(a.Role, comunicado.RolReservado, StringComparison.OrdinalIgnoreCase));

                foreach (var asignacion in asignaciones)
                {
                    var usuario = await ec.GetUserByIdAsync(asignacion.IdUser);
                    destinatarios.Add(new AnnouncementRecipient
                    {
                        IdGroupUnit = asignacion.IdGroupUnit,
                        NombreRecipient = $"{usuario.FirstName} {usuario.LastName}".Trim(),
                        Telefono = usuario.PhoneNumber,
                        Email = usuario.Email
                    });
                }
            }

            return destinatarios;
        }

        private async Task EnviarPorWhatsAppAsync(AnnouncementRecipient destinatario, Announcement comunicado)
        {
            if (string.IsNullOrWhiteSpace(destinatario.Telefono))
            {
                destinatario.EstadoWhatsApp = WhatsAppDeliveryStatus.NoAplica;
                return;
            }

            try
            {
                // Hoy manda el cuerpo como texto libre (SendMessageAsync) -- no hay
                // ninguna Content Template aprobada por Meta todavía (ver Docs/
                // Pendientes-Negocio-Consolidado.md #17). Cuando exista una por
                // categoría, cambiar acá a SendTemplateMessageAsync con su contentSid.
                var mensaje = $"*{comunicado.Titulo}*\n\n{comunicado.Cuerpo}";
                var ok = await _whatsAppService.SendMessageAsync(destinatario.Telefono, mensaje);

                destinatario.EstadoWhatsApp = !ok
                    ? WhatsAppDeliveryStatus.Fallido
                    : _whatsAppService.IsSimulate ? WhatsAppDeliveryStatus.Simulado : WhatsAppDeliveryStatus.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mandando WhatsApp del comunicado {IdAnnouncement} a {Telefono}",
                    comunicado.IdAnnouncement, destinatario.Telefono);
                destinatario.EstadoWhatsApp = WhatsAppDeliveryStatus.Fallido;
            }
        }

        // Mismo patrón "best effort" que el resto de la app (IAuthService.
        // SendWelcomeEmailAsync, ICalendarService.NotifyBuildingAsync, etc.): un
        // fallo de correo nunca debe tirar abajo la publicación del comunicado.
        private async Task EnviarPorCorreoAsync(AnnouncementRecipient destinatario, Announcement comunicado)
        {
            if (string.IsNullOrWhiteSpace(destinatario.Email))
            {
                destinatario.EstadoCorreo = EmailDeliveryStatus.NoAplica;
                return;
            }

            try
            {
                await _emailService.SendEmailAsync(destinatario.Email, comunicado.Titulo, comunicado.Cuerpo);
                destinatario.EstadoCorreo = EmailDeliveryStatus.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mandando correo del comunicado {IdAnnouncement} a {Email}",
                    comunicado.IdAnnouncement, destinatario.Email);
                destinatario.EstadoCorreo = EmailDeliveryStatus.Fallido;
            }
        }
    }
}
