using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    // Catálogo/documentación de los flujos de negocio de la app (ej: aprobación de
    // Presupuesto) y en qué estado de implementación está cada uno — no maneja lógica
    // de negocio en vivo (eso lo sigue haciendo cada módulo con su propio estado, como
    // BudgetStatus para Presupuesto). Es una herramienta de referencia/seguimiento
    // para el equipo, no un motor de workflow configurable.
    public enum WorkflowImplementationStatus
    {
        Pendiente = 0,
        EnDesarrollo = 1,
        Implementado = 2,
        Descartado = 3
    }

    public class Workflow
    {
        public Guid IdWorkflow { get; set; } = Guid.NewGuid();

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        public WorkflowImplementationStatus Status { get; set; } = WorkflowImplementationStatus.Pendiente;

        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public DateTime? UpdatedOn { get; set; }

        [NotMapped]
        public List<WorkflowStep> Steps { get; set; } = new();

        [NotMapped]
        public int TotalSteps => Steps.Count;
        [NotMapped]
        public int ImplementedSteps => Steps.Count(s => s.IsImplemented);
    }

    public class WorkflowStep
    {
        public Guid IdWorkflowStep { get; set; } = Guid.NewGuid();
        public Guid IdWorkflow { get; set; }

        public int StepOrder { get; set; }

        [Required(ErrorMessage = "El nombre del paso es obligatorio")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Quién ejecuta el paso: "Administrador", "Junta", "Sistema", "Propietario", etc.
        // Texto libre a propósito — no todos los flujos comparten los mismos roles.
        [StringLength(100)]
        public string Responsible { get; set; } = string.Empty;

        public bool IsImplemented { get; set; }
    }
}
