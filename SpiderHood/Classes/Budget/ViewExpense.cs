using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class ViewExpense
    {
        public Guid IdExpense { get; set; }
        public string Description { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public Guid IdCategory { get; set; }
        public string Supplier { get; set; } = string.Empty;
        // Antes un enum fijo de 5 valores -- ahora referencia Parameter.Value dentro del
        // grupo Mixto "Método de Pago" (mismo criterio que Incident.Type/Priority), para que
        // un administrador pueda agregar sus propios métodos desde /parameter (Yape, Plin,
        // depósito en agencia, etc.) sin depender de un release. GET_ExpensesByBuilding sigue
        // devolviendo la columna Expense.PaymentMethod tal cual (INT, sin CHECK constraint
        // conocido) -- el nombre a mostrar se resuelve del lado de la UI contra
        // ParameterService.GetGroupChildren("Método de Pago", ...), no viene por JOIN.
        public int PaymentMethod { get; set; }
        public StatusExpense Status { get; set; }  // Pending, Approved, Rejected
        public bool Reconciled { get; set; } = false;
        public Guid? ReconciledTransactionId { get; set; }
        public TypeDistribution? Distribution { get; set; }
        public Guid? IdBuilding { get; set; }
        public string Notes { get; set; } = string.Empty;
        //public DateTime CreatedDate { get; set; } = DateTime.Now;
        public bool AutoReconcile { get; set; } = true;
        public DateTime ExpenseDate { get; set; }
        public bool IncludeInQuota { get; set; }
        //public DateTime? PaymentDate { get; set; }
    }
}
