using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IIncidentService
    {
        Task<List<Incident>> GetIncidentsByBuildingAsync(Guid idBuilding);
        Task<List<Incident>> GetMyIncidentsAsync(Guid idUser);
        Task<Incident> GetByIdAsync(Guid idIncident);
        Task<List<IncidentComment>> GetCommentsAsync(Guid idIncident);
        Task<Incident> ReportAsync(Incident incident);
        Task ChangeStatusAsync(Guid idIncident, IncidentStatus newStatus, Guid? assignedTo, string performedBy, Guid idBuilding, string? comment = null);
        Task AddCommentAsync(Guid idIncident, Guid authorId, string text, bool isInternal);

        // Docs/Pendientes-Negocio-Consolidado.md #18a
        Task<List<IncidentAttachment>> GetAttachmentsAsync(Guid idIncident);

        // Valida tipo/tamaño y guarda el archivo -- tira ArgumentException con un
        // mensaje para mostrar al usuario si no pasa la validación (extensión no
        // permitida, o excede el tamaño máximo configurado).
        Task<IncidentAttachment> UploadAttachmentAsync(
            Guid idIncident, Guid idBuilding, string fileName, string contentType, byte[] content, Guid uploadedBy);

        // Null si el archivo ya no está en storage (mismo criterio que
        // IFileStorageService.ReadAsync -- nunca tira excepción por esto).
        Task<byte[]?> GetAttachmentContentAsync(IncidentAttachment attachment);
    }

    public class IncidentService : IIncidentService
    {
        // Extensiones permitidas para adjuntos de Incidencias -- fotos (el caso
        // de uso principal, según Docs/Design-Piloto-Mobile-Android.md) + video
        // corto. Whitelist deliberada, no blacklist -- cualquier extensión no
        // listada se rechaza.
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".heic", ".mp4", ".mov"
        };

        private readonly BDLayout ec;
        private readonly IWorkflowAuditService _workflowAuditService;
        private readonly IEmailService _emailService;
        private readonly IFileStorageService _fileStorage;
        private readonly int _maxAttachmentSizeBytes;
        private readonly ILogger<IncidentService> _logger;

        public IncidentService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IWorkflowAuditService workflowAuditService,
            IEmailService emailService,
            IFileStorageService fileStorage,
            IConfiguration configuration,
            ILogger<IncidentService> logger)
        {
            ec = new BDLayout(contextFactory);
            _workflowAuditService = workflowAuditService ?? throw new ArgumentNullException(nameof(workflowAuditService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _fileStorage = fileStorage ?? throw new ArgumentNullException(nameof(fileStorage));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            // 15 MB por default -- cubre una foto de celular actual (3-8 MB) con
            // margen, y un video corto chico. Video más largo/pesado que esto se
            // rechaza a propósito: mandarlo entero como base64 por el circuito de
            // Blazor Server (ver IncidentDetail.razor) ya empieza a sentirse
            // pesado más allá de este orden de magnitud -- si hace falta subir
            // videos más grandes, el paso siguiente es streaming real (Fase B
            // del piloto móvil / API), no subir este límite sin más.
            _maxAttachmentSizeBytes = configuration.GetValue<int?>("IncidentAttachments:MaxSizeBytes") ?? 15 * 1024 * 1024;
        }

        public async Task<List<Incident>> GetIncidentsByBuildingAsync(Guid idBuilding)
            => await ec.GetIncidentsByBuildingAsync(idBuilding);

        public async Task<List<Incident>> GetMyIncidentsAsync(Guid idUser)
            => await ec.GetIncidentsByReporterAsync(idUser);

        public async Task<Incident> GetByIdAsync(Guid idIncident)
            => await ec.GetIncidentByIdAsync(idIncident);

        public async Task<List<IncidentComment>> GetCommentsAsync(Guid idIncident)
            => await ec.GetIncidentCommentsAsync(idIncident);

        public async Task<List<IncidentAttachment>> GetAttachmentsAsync(Guid idIncident)
            => await ec.GetIncidentAttachmentsAsync(idIncident);

        public async Task<IncidentAttachment> UploadAttachmentAsync(
            Guid idIncident, Guid idBuilding, string fileName, string contentType, byte[] content, Guid uploadedBy)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Tipo de archivo no permitido ({(string.IsNullOrEmpty(extension) ? "sin extensión" : extension)}). " +
                    $"Formatos aceptados: {string.Join(", ", AllowedExtensions.Order())}.");
            }

            if (content.Length == 0)
                throw new ArgumentException("El archivo está vacío.");

            if (content.Length > _maxAttachmentSizeBytes)
            {
                throw new ArgumentException(
                    $"El archivo pesa {content.Length / 1024 / 1024} MB -- el máximo permitido es {_maxAttachmentSizeBytes / 1024 / 1024} MB.");
            }

            var attachment = new IncidentAttachment
            {
                IdIncident = idIncident,
                IdBuilding = idBuilding,
                FileName = fileName,
                ContentType = contentType,
                FileSizeBytes = content.Length,
                UploadedBy = uploadedBy
            };

            // Edificio -> Incidente (carpeta por incidente, no por fecha -- ver
            // Docs/Pendientes-Negocio-Consolidado.md #18a). Nombre de archivo en
            // disco = IdAttachment + extensión real, nunca el nombre original tal
            // cual (evita colisiones entre dos fotos con el mismo nombre de
            // cámara, ej. "IMG_0001.jpg" subidas por dos personas distintas).
            attachment.FilePath = await _fileStorage.SaveAsync(
                new[] { "incidents", idBuilding.ToString(), idIncident.ToString() },
                $"{attachment.IdAttachment}{extension}",
                content);

            await ec.AddNewRecordAsync(attachment);
            return attachment;
        }

        public async Task<byte[]?> GetAttachmentContentAsync(IncidentAttachment attachment)
            => await _fileStorage.ReadAsync(attachment.FilePath);

        public async Task<Incident> ReportAsync(Incident incident)
        {
            if (incident.IdIncident == Guid.Empty)
                incident.IdIncident = Guid.NewGuid();

            await ec.AddNewRecordAsync(incident);

            await _workflowAuditService.LogAsync(
                "Incident", incident.IdIncident, WorkflowAction.Submitted, incident.CreatedBy, incident.IdBuilding);

            await NotifyBuildingAdminsAsync(incident);

            return incident;
        }

        public async Task ChangeStatusAsync(Guid idIncident, IncidentStatus newStatus, Guid? assignedTo, string performedBy, Guid idBuilding, string? comment = null)
        {
            await ec.UpdateIncidentStatusAsync(idIncident, newStatus, assignedTo, performedBy);

            var action = newStatus switch
            {
                IncidentStatus.InReview => WorkflowAction.Reviewed,
                IncidentStatus.InProgress => WorkflowAction.Assigned,
                IncidentStatus.Resolved => WorkflowAction.Resolved,
                IncidentStatus.Closed => WorkflowAction.Closed,
                IncidentStatus.Rejected => WorkflowAction.Rejected,
                IncidentStatus.Reopened => WorkflowAction.Reopened,
                _ => WorkflowAction.Submitted
            };

            await _workflowAuditService.LogAsync("Incident", idIncident, action, performedBy, idBuilding, comment);

            await NotifyReporterAsync(idIncident, newStatus);
        }

        public async Task AddCommentAsync(Guid idIncident, Guid authorId, string text, bool isInternal)
        {
            await ec.AddNewRecordAsync(new IncidentComment
            {
                IdIncident = idIncident,
                AuthorId = authorId,
                Text = text,
                IsInternal = isInternal
            });
        }

        // Best effort: un fallo notificando por correo no puede tumbar la operación
        // real (reportar el incidente / cambiar el estado) -- se loguea y sigue.
        private async Task NotifyBuildingAdminsAsync(Incident incident)
        {
            try
            {
                var roles = await ec.GetAllUserBuildingRolesAsync();
                var admins = roles
                    .Where(r => r.IdBuilding == incident.IdBuilding && r.Role == "Administrador")
                    .Select(r => r.UserEmail)
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Distinct();

                foreach (var email in admins)
                {
                    await _emailService.SendEmailAsync(
                        email,
                        $"Nuevo incidente: {incident.Title}",
                        $"<p>Se reportó un nuevo incidente ({incident.Priority}): <strong>{incident.Title}</strong></p><p>{incident.Description}</p>");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notificando a los administradores del incidente {IdIncident}", incident.IdIncident);
            }
        }

        private async Task NotifyReporterAsync(Guid idIncident, IncidentStatus newStatus)
        {
            try
            {
                var incident = await ec.GetIncidentByIdAsync(idIncident);
                var reporter = await ec.GetUserByIdAsync(incident.ReportedBy);

                if (string.IsNullOrWhiteSpace(reporter?.Email))
                    return;

                await _emailService.SendEmailAsync(
                    reporter.Email,
                    $"Actualización de tu incidente: {incident.Title}",
                    $"<p>Tu incidente <strong>{incident.Title}</strong> cambió de estado a <strong>{newStatus}</strong>.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notificando al reportante del incidente {IdIncident}", idIncident);
            }
        }
    }
}
