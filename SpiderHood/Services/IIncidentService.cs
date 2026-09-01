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
    }

    public class IncidentService : IIncidentService
    {
        private readonly BDLayout ec;
        private readonly IWorkflowAuditService _workflowAuditService;
        private readonly IEmailService _emailService;
        private readonly ILogger<IncidentService> _logger;

        public IncidentService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IWorkflowAuditService workflowAuditService,
            IEmailService emailService,
            ILogger<IncidentService> logger)
        {
            ec = new BDLayout(contextFactory);
            _workflowAuditService = workflowAuditService ?? throw new ArgumentNullException(nameof(workflowAuditService));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<Incident>> GetIncidentsByBuildingAsync(Guid idBuilding)
            => await ec.GetIncidentsByBuildingAsync(idBuilding);

        public async Task<List<Incident>> GetMyIncidentsAsync(Guid idUser)
            => await ec.GetIncidentsByReporterAsync(idUser);

        public async Task<Incident> GetByIdAsync(Guid idIncident)
            => await ec.GetIncidentByIdAsync(idIncident);

        public async Task<List<IncidentComment>> GetCommentsAsync(Guid idIncident)
            => await ec.GetIncidentCommentsAsync(idIncident);

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
