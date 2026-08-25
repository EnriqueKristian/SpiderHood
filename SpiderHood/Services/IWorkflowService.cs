using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IWorkflowService
    {
        Task<List<Workflow>> GetWorkflowsAsync();
        Task<Workflow> CreateWorkflowAsync(Workflow workflow);
        Task UpdateWorkflowAsync(Workflow workflow);
        Task DeleteWorkflowAsync(Workflow workflow);

        Task<List<WorkflowStep>> GetStepsAsync(Guid idWorkflow);
        Task<WorkflowStep> AddStepAsync(WorkflowStep step);
        Task UpdateStepAsync(WorkflowStep step);
        Task DeleteStepAsync(WorkflowStep step);
    }

    public class WorkflowService : IWorkflowService
    {
        private BDLayout ec { get; set; }

        public WorkflowService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<Workflow>> GetWorkflowsAsync()
        {
            var workflows = await ec.GetWorkflowsAsync();

            foreach (var workflow in workflows)
            {
                workflow.Steps = await ec.GetWorkflowStepsByWorkflowAsync(workflow.IdWorkflow);
            }

            return workflows;
        }

        public async Task<Workflow> CreateWorkflowAsync(Workflow workflow)
        {
            if (workflow.IdWorkflow == Guid.Empty)
                workflow.IdWorkflow = Guid.NewGuid();

            await ec.AddNewRecordAsync(workflow);
            return workflow;
        }

        public async Task UpdateWorkflowAsync(Workflow workflow)
        {
            await ec.UpdateRecordAsync(workflow);
        }

        public async Task DeleteWorkflowAsync(Workflow workflow)
        {
            await ec.DeleteRecordAsync(workflow);
        }

        public async Task<List<WorkflowStep>> GetStepsAsync(Guid idWorkflow)
        {
            return await ec.GetWorkflowStepsByWorkflowAsync(idWorkflow);
        }

        public async Task<WorkflowStep> AddStepAsync(WorkflowStep step)
        {
            if (step.IdWorkflowStep == Guid.Empty)
                step.IdWorkflowStep = Guid.NewGuid();

            await ec.AddNewRecordAsync(step);
            return step;
        }

        public async Task UpdateStepAsync(WorkflowStep step)
        {
            await ec.UpdateRecordAsync(step);
        }

        public async Task DeleteStepAsync(WorkflowStep step)
        {
            await ec.DeleteRecordAsync(step);
        }
    }
}
