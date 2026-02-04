using System;
using System.ComponentModel.DataAnnotations;
using SpiderHood.Models;

namespace SpiderHood.Models
{
    public class Workflow
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(20)]
        public string Version { get; set; } = "1.0";

        [StringLength(50)]
        public string Status { get; set; } = "Activo";

        public DateTime CreatedDate { get; set; }

        public DateTime? UpdatedDate { get; set; }

        // Imagen del workflow (almacenada como base64 o ruta)
        public byte[] WorkflowImage { get; set; } = [];

        [StringLength(50)]
        public string ImageContentType { get; set; } = string.Empty;

        // Nombre del archivo original
        [StringLength(255)]
        public string OriginalFileName { get; set; } = string.Empty;
    }

    public class WorkflowDetail
    {
        public int Id { get; set; }

        public int WorkflowId { get; set; }

        public Workflow Workflow { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string StepName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string StepType { get; set; } = string.Empty;// "Inicio", "Proceso", "Decisión", "Fin"

        public int Order { get; set; }

        [StringLength(100)]
        public string NextStepIfYes { get; set; } = string.Empty;// Para pasos de decisión

        [StringLength(100)]
        public string NextStepIfNo { get; set; } = string.Empty;// Para pasos de decisión
    }
}

namespace SpiderHood.Services
{
    public interface IWorkflowService
    {
        Task<List<Workflow>> GetAllWorkflowsAsync();
        Task<Workflow> GetWorkflowByIdAsync(int id);
        Task<List<WorkflowDetail>> GetWorkflowDetailsAsync(int workflowId);
        Task<Workflow> AddWorkflowAsync(Workflow workflow, byte[] imageData, string contentType, string fileName);
        Task UpdateWorkflowAsync(Workflow workflow);
        Task DeleteWorkflowAsync(int id);
        Task<string> GetWorkflowImageAsBase64Async(int workflowId);
    }
    public class WorkflowService : IWorkflowService
    {
        private List<Workflow> workflows = new List<Workflow>();
        private List<WorkflowDetail> workflowDetails = new List<WorkflowDetail>();
        private int nextWorkflowId = 1;
        private int nextDetailId = 1;

        public WorkflowService()
        {
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            // Datos de ejemplo para el workflow de presupuesto
            var budgetWorkflow = new Workflow
            {
                Id = nextWorkflowId++,
                Name = "Presupuesto",
                Description = "Workflow para creación y aprobación de presupuestos",
                Version = "1.0",
                Status = "Activo",
                CreatedDate = DateTime.Now.AddDays(-30),
                UpdatedDate = DateTime.Now,
                ImageContentType = "image/png",
                OriginalFileName = "presupuesto_workflow.png"
            };

            workflows.Add(budgetWorkflow);

            // Detalles del workflow de presupuesto
            var details = new List<WorkflowDetail>
            {
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "ADMINISTRACIÓN", Description = "Módulo de administración", StepType = "Inicio", Order = 1 },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "INICIO", Description = "Punto de inicio del workflow", StepType = "Proceso", Order = 2 },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "CREA PRESUPUESTO", Description = "Etapa de creación del presupuesto", StepType = "Proceso", Order = 3 },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "¿REHIZA ADO?", Description = "Verificación si requiere revisión", StepType = "Decisión", Order = 4, NextStepIfYes = "INICIO", NextStepIfNo = "APROBAR" },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "APROBAR", Description = "Etapa de aprobación del presupuesto", StepType = "Proceso", Order = 5 },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "ACTIVO", Description = "Estado final cuando el presupuesto está activo", StepType = "Proceso", Order = 6 },
                new WorkflowDetail { Id = nextDetailId++, WorkflowId = budgetWorkflow.Id, StepName = "FIN", Description = "Finalización del workflow", StepType = "Fin", Order = 7 }
            };

            workflowDetails.AddRange(details);

            // Otros workflows de ejemplo
            workflows.Add(new Workflow
            {
                Id = nextWorkflowId++,
                Name = "Aprobación de Gastos",
                Description = "Workflow para aprobación de gastos operativos",
                Version = "2.1",
                Status = "Activo",
                CreatedDate = DateTime.Now.AddDays(-45),
                UpdatedDate = DateTime.Now.AddDays(-10),
                ImageContentType = "image/png",
                OriginalFileName = "gastos_workflow.png"
            });

            workflows.Add(new Workflow
            {
                Id = nextWorkflowId++,
                Name = "Contratación",
                Description = "Workflow para proceso de contratación de personal",
                Version = "1.5",
                Status = "En revisión",
                CreatedDate = DateTime.Now.AddDays(-15),
                UpdatedDate = DateTime.Now,
                ImageContentType = "image/png",
                OriginalFileName = "contratacion_workflow.png"
            });
        }

        public Task<List<Workflow>> GetAllWorkflowsAsync()
        {
            return Task.FromResult(workflows);
        }

        public Task<Workflow> GetWorkflowByIdAsync(int id)
        {
            var workflow = workflows.FirstOrDefault(w => w.Id == id);
            return Task.FromResult(workflow);
        }

        public Task<List<WorkflowDetail>> GetWorkflowDetailsAsync(int workflowId)
        {
            var details = workflowDetails
                .Where(d => d.WorkflowId == workflowId)
                .OrderBy(d => d.Order)
                .ToList();

            return Task.FromResult(details);
        }

        public Task<Workflow> AddWorkflowAsync(Workflow workflow, byte[] imageData, string contentType, string fileName)
        {
            workflow.Id = nextWorkflowId++;
            workflow.CreatedDate = DateTime.Now;
            workflow.WorkflowImage = imageData;
            workflow.ImageContentType = contentType;
            workflow.OriginalFileName = fileName;

            workflows.Add(workflow);
            return Task.FromResult(workflow);
        }

        public Task UpdateWorkflowAsync(Workflow workflow)
        {
            var existing = workflows.FirstOrDefault(w => w.Id == workflow.Id);
            if (existing != null)
            {
                existing.Name = workflow.Name;
                existing.Description = workflow.Description;
                existing.Version = workflow.Version;
                existing.Status = workflow.Status;
                existing.UpdatedDate = DateTime.Now;
            }

            return Task.CompletedTask;
        }

        public Task DeleteWorkflowAsync(int id)
        {
            var workflow = workflows.FirstOrDefault(w => w.Id == id);
            if (workflow != null)
            {
                workflows.Remove(workflow);
                // También eliminar detalles relacionados
                workflowDetails.RemoveAll(d => d.WorkflowId == id);
            }

            return Task.CompletedTask;
        }

        public Task<string> GetWorkflowImageAsBase64Async(int workflowId)
        {
            var workflow = workflows.FirstOrDefault(w => w.Id == workflowId);
            if (workflow?.WorkflowImage != null)
            {
                string base64String = Convert.ToBase64String(workflow.WorkflowImage);
                string imageSrc = $"data:{workflow.ImageContentType};base64,{base64String}";
                return Task.FromResult(imageSrc);
            }

            // Si no hay imagen, devolver una imagen por defecto
            return Task.FromResult<string>(null);
        }
    }
}