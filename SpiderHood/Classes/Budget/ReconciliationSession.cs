using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class ReconciliationSession
    {
        // Docs/Pendientes-Negocio-Conciliacion.md #3 -- antes Id era int (nunca se
        // guardaba de verdad, así que no importaba), ahora que se persiste de verdad
        // se genera acá igual que WorkflowAuditEntry.Id, no como identity de la BD.
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid IdBankAccount { get; set; }
        // Para poder filtrar/scopear por edificio -- antes no existía este campo.
        public Guid IdBuilding { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int ProcessedTransactions { get; set; }
        public int ReconciledTransactions { get; set; }
        [Precision(18, 2)]
        public decimal Difference { get; set; }
        public bool Completed { get; set; }
        public DateTime Date { get; set; } = DateTime.Now;
        public string PerformedBy { get; set; } = "";
        // Nullable de verdad -- la columna es NVARCHAR(500) NULL (nadie completa notas
        // todavía, SaveReconciliationAsync siempre manda NULL, ver BDLayout.Add.cs) y
        // FromSqlRaw exige que la nulabilidad del CLR type coincida con la columna real;
        // con Notes como string no-nullable, EF tiraba "Data is Null. This method or
        // property cannot be called on Null values." al leer GET_LastReconciliationSession
        // apenas había una fila real. Mismo patrón que WorkflowAuditEntry.Comment.
        public string? Notes { get; set; }
    }
}
