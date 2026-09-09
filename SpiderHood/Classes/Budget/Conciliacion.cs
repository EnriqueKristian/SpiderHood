using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class Conciliacion
    {
        // Docs/Pendientes-Negocio-Conciliacion.md #3 -- antes Id era int (nunca se
        // guardaba de verdad, así que no importaba), ahora que se persiste de verdad
        // se genera acá igual que WorkflowAuditEntry.Id, no como identity de la BD.
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid CuentaBancariaId { get; set; }
        // Para poder filtrar/scopear por edificio -- antes no existía este campo.
        public Guid IdBuilding { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
        public int TransaccionesProcesadas { get; set; }
        public int TransaccionesConciliadas { get; set; }
        [Precision(18, 2)]
        public decimal Diferencia { get; set; }
        public bool Completada { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Usuario { get; set; } = "";
        // Nullable de verdad -- la columna es NVARCHAR(500) NULL (nadie completa notas
        // todavía, GuardarConciliacionAsync siempre manda NULL, ver BDLayout.Add.cs) y
        // FromSqlRaw exige que la nulabilidad del CLR type coincida con la columna real;
        // con Notas como string no-nullable, EF tiraba "Data is Null. This method or
        // property cannot be called on Null values." al leer GET_LastReconciliationSession
        // apenas había una fila real. Mismo patrón que WorkflowAuditEntry.Comment.
        public string? Notas { get; set; }
    }
}
