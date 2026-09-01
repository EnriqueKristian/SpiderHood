using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace SpiderHood.Models
{
    public class BudgetHeader
    {
        public Guid IdBudgetHeader { get; set; }
        public string BudgetName { get; set; } = string.Empty;
        // Un new BudgetHeader() recién creado (flujo de "nuevo presupuesto") queda con
        // BudgetDate = default(DateTime) = 0001-01-01 si no se inicializa aquí. SQL Server
        // sólo acepta datetime desde 1753-01-01, así que cualquier consulta que use esa
        // fecha antes de que el usuario la elija en el modal "Nuevo Cálculo" (p.ej. cargar
        // gastos pendientes de conciliación) truena con SqlTypeException y tumba el circuito
        // entero de Blazor Server.
        public DateTime BudgetDate { get; set; } = DateTime.Now;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public string BudgetType { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public Guid? IdPeriod { get; set; }
        public DateTime CreatedOn { get; set; }
        [NotMapped]
        public int Month { get { return BudgetDate.Month; } }
        [NotMapped]
        public int Year { get { return BudgetDate.Year; } }
        public BudgetStatus Status { get; set; }
        public string Mes => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);

        [NotMapped]
        public List<BudgetDetail> Details { get; set; } = [];
    }
}
