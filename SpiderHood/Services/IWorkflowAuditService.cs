using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IWorkflowAuditService
    {
        Task LogAsync(string module, Guid entityId, WorkflowAction action, string performedBy, Guid idBuilding, string? comment = null);
        Task<List<WorkflowAuditEntry>> GetHistoryAsync(string module, Guid entityId);
    }

    public class WorkflowAuditService : IWorkflowAuditService
    {
        private readonly BDLayout ec;

        public WorkflowAuditService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task LogAsync(string module, Guid entityId, WorkflowAction action, string performedBy, Guid idBuilding, string? comment = null)
        {
            await ec.AddNewRecordAsync(new WorkflowAuditEntry
            {
                Module = module,
                EntityId = entityId,
                Action = action,
                PerformedBy = performedBy,
                IdBuilding = idBuilding,
                Comment = comment
            });
        }

        public async Task<List<WorkflowAuditEntry>> GetHistoryAsync(string module, Guid entityId)
            => await ec.GetWorkflowAuditLogAsync(module, entityId);
    }
}
