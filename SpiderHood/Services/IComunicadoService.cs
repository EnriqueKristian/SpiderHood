using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #17 -- diseño cerrado 2026-09-11.
    // Módulo chico a propósito: 3 alcances (Público/Reservado/Privado), sin
    // mensajería vecino-a-vecino ni "Público Global" de SysAdmin.
    public interface IComunicadoService
    {
        Task<List<Comunicado>> GetComunicadosAsync(Guid idBuilding);

        Task<List<ComunicadoDestinatario>> GetDestinatariosAsync(Guid idComunicado);

        // Comunicados visibles para un residente/rol puntual dentro de un edificio --
        // Públicos siempre, Reservados si el rol matchea, Privados si su unidad
        // aparece como destinatario.
        Task<List<Comunicado>> GetComunicadosParaUsuarioAsync(Guid idBuilding, string rolUsuario, Guid? idGroupUnit);

        // Resuelve destinatarios según el Alcance, crea el Comunicado + sus
        // ComunicadoDestinatario, y manda por WhatsApp (siempre, en modo Simulado si
        // no hay credenciales reales de Twilio) y por correo (sólo si
        // comunicado.EnviarPorCorreo está marcado). idGroupUnitsPrivado sólo se usa
        // cuando Alcance == Privado.
        Task<PublicarComunicadoResultado> PublicarComunicadoAsync(Comunicado comunicado, List<Guid>? idGroupUnitsPrivado);
    }

    public class ComunicadoService : IComunicadoService
    {
        private readonly IWhatsAppService _whatsAppService;
        private readonly IEmailService _emailService;
        private readonly ILogger<ComunicadoService> _logger;
        private BDLayout ec { get; set; }

        public ComunicadoService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IWhatsAppService whatsAppService,
            IEmailService emailService,
            ILogger<ComunicadoService> logger)
        {
            _whatsAppService = whatsAppService;
            _emailService = emailService;
            _logger = logger;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Comunicado>> GetComunicadosAsync(Guid idBuilding)
            => await ec.GetComunicadosByBuildingAsync(idBuilding);

        public async Task<List<ComunicadoDestinatario>> GetDestinatariosAsync(Guid idComunicado)
            => await ec.GetComunicadoDestinatariosAsync(idComunicado);

        public async Task<List<Comunicado>> GetComunicadosParaUsuarioAsync(Guid idBuilding, string rolUsuario, Guid? idGroupUnit)
            => await ec.GetComunicadosParaUsuarioAsync(idBuilding, rolUsuario, idGroupUnit);

        public async Task<PublicarComunicadoResultado> PublicarComunicadoAsync(Comunicado comunicado, List<Guid>? idGroupUnitsPrivado)
        {
            var resultado = new PublicarComunicadoResultado();

            if (string.IsNullOrWhiteSpace(comunicado.Titulo) || string.IsNullOrWhiteSpace(comunicado.Cuerpo))
            {
                resultado.Mensaje = "Título y Cuerpo son obligatorios.";
                return resultado;
            }

            if (comunicado.Alcance == AlcanceComunicado.Reservado && string.IsNullOrWhiteSpace(comunicado.RolReservado))
            {
                resultado.Mensaje = "Seleccione a qué rol le llega el comunicado.";
                return resultado;
            }

            if (comunicado.Alcance == AlcanceComunicado.Privado && (idGroupUnitsPrivado == null || !idGroupUnitsPrivado.Any()))
            {
                resultado.Mensaje = "Seleccione al menos una unidad para un comunicado Privado.";
                return resultado;
            }

            try
            {
                var destinatarios = await ResolverDestinatariosAsync(comunicado, idGroupUnitsPrivado);
                if (!destinatarios.Any())
                {
                    resultado.Mensaje = "No se encontró ningún destinatario para el alcance seleccionado.";
                    return resultado;
                }

                comunicado.IdComunicado = Guid.NewGuid();
                await ec.AddNewRecordAsync(comunicado);

                foreach (var destinatario in destinatarios)
                {
                    destinatario.IdComunicadoDestinatario = Guid.NewGuid();
                    destinatario.IdComunicado = comunicado.IdComunicado;

                    await EnviarPorWhatsAppAsync(destinatario, comunicado);
                    resultado.TotalDestinatarios++;
                    switch (destinatario.EstadoWhatsApp)
                    {
                        case EstadoEnvioWhatsApp.Enviado: resultado.EnviadosWhatsApp++; break;
                        case EstadoEnvioWhatsApp.Simulado: resultado.SimuladosWhatsApp++; break;
                        case EstadoEnvioWhatsApp.Fallido: resultado.FallidosWhatsApp++; break;
                    }

                    if (comunicado.EnviarPorCorreo)
                    {
                        await EnviarPorCorreoAsync(destinatario, comunicado);
                        switch (destinatario.EstadoCorreo)
                        {
                            case EstadoEnvioCorreo.Enviado: resultado.EnviadosCorreo++; break;
                            case EstadoEnvioCorreo.Fallido: resultado.FallidosCorreo++; break;
                        }
                    }

                    await ec.AddNewRecordAsync(destinatario);
                }

                resultado.Exito = true;
                resultado.IdComunicado = comunicado.IdComunicado;
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
        // GetUnidadesAsync para cuotas extraordinarias) -- Reservado resuelve contra
        // UserBuildingAssociation (rol de portal, no el rol de propietario dentro de
        // la unidad) y pide el teléfono aparte por usuario (aceptable: la audiencia de
        // un Reservado suele ser chica, ej. sólo la Junta).
        private async Task<List<ComunicadoDestinatario>> ResolverDestinatariosAsync(Comunicado comunicado, List<Guid>? idGroupUnitsPrivado)
        {
            var destinatarios = new List<ComunicadoDestinatario>();

            if (comunicado.Alcance == AlcanceComunicado.Publico || comunicado.Alcance == AlcanceComunicado.Privado)
            {
                var unidades = (await ec.GetOwnersByBuildingAsync(comunicado.IdBuilding))
                    .Where(u => u.Role == 1 && u.TypeUnit == 1);

                if (comunicado.Alcance == AlcanceComunicado.Privado)
                {
                    var idsSeleccionados = idGroupUnitsPrivado!.ToHashSet();
                    unidades = unidades.Where(u => idsSeleccionados.Contains(u.IdGroupUnit));
                }

                destinatarios.AddRange(unidades.Select(u => new ComunicadoDestinatario
                {
                    IdGroupUnit = u.IdGroupUnit,
                    NombreDestinatario = $"{u.FirstName} {u.LastName}".Trim(),
                    Telefono = u.PhoneNumber,
                    Email = u.Email
                }));
            }
            else if (comunicado.Alcance == AlcanceComunicado.Reservado)
            {
                var asignaciones = (await ec.GetAllUserBuildingRolesAsync())
                    .Where(a => a.IdBuilding == comunicado.IdBuilding && a.IsApproved
                        && string.Equals(a.Role, comunicado.RolReservado, StringComparison.OrdinalIgnoreCase));

                foreach (var asignacion in asignaciones)
                {
                    var usuario = await ec.GetUserByIdAsync(asignacion.IdUser);
                    destinatarios.Add(new ComunicadoDestinatario
                    {
                        IdGroupUnit = asignacion.IdGroupUnit,
                        NombreDestinatario = $"{usuario.FirstName} {usuario.LastName}".Trim(),
                        Telefono = usuario.PhoneNumber,
                        Email = usuario.Email
                    });
                }
            }

            return destinatarios;
        }

        private async Task EnviarPorWhatsAppAsync(ComunicadoDestinatario destinatario, Comunicado comunicado)
        {
            if (string.IsNullOrWhiteSpace(destinatario.Telefono))
            {
                destinatario.EstadoWhatsApp = EstadoEnvioWhatsApp.NoAplica;
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
                    ? EstadoEnvioWhatsApp.Fallido
                    : _whatsAppService.IsSimulate ? EstadoEnvioWhatsApp.Simulado : EstadoEnvioWhatsApp.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mandando WhatsApp del comunicado {IdComunicado} a {Telefono}",
                    comunicado.IdComunicado, destinatario.Telefono);
                destinatario.EstadoWhatsApp = EstadoEnvioWhatsApp.Fallido;
            }
        }

        // Mismo patrón "best effort" que el resto de la app (IAuthService.
        // SendWelcomeEmailAsync, ICalendarService.NotifyBuildingAsync, etc.): un
        // fallo de correo nunca debe tirar abajo la publicación del comunicado.
        private async Task EnviarPorCorreoAsync(ComunicadoDestinatario destinatario, Comunicado comunicado)
        {
            if (string.IsNullOrWhiteSpace(destinatario.Email))
            {
                destinatario.EstadoCorreo = EstadoEnvioCorreo.NoAplica;
                return;
            }

            try
            {
                await _emailService.SendEmailAsync(destinatario.Email, comunicado.Titulo, comunicado.Cuerpo);
                destinatario.EstadoCorreo = EstadoEnvioCorreo.Enviado;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error mandando correo del comunicado {IdComunicado} a {Email}",
                    comunicado.IdComunicado, destinatario.Email);
                destinatario.EstadoCorreo = EstadoEnvioCorreo.Fallido;
            }
        }
    }
}
