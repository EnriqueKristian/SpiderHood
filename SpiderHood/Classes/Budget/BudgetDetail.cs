using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    public class BudgetDetail
    {
        public Guid IdBudgetDetail { get; set; }
        public Guid IdCategory { get; set; }
        public int IdSection { get; set; }
        [Precision(18, 2)]
        public decimal ItemNumber { get; set; }
        public string Description { get; set; } = "";
        [Precision(18, 2)]
        public decimal MonthlyAmount { get; set; }
        [Precision(18, 2)]
        public decimal AnnualAmount { get; set; }
        public int Frequency { get; set; }
        public int Type { get; set; }
        public bool IsHeader { get; set; } = false;
        public Guid IdBudgetHeader { get; set; }
        public bool IsNewItem { get; set; } = false;
        public Guid IdParent { get; set; }

        // Congelado por BudgetCalculator.CalculateQuota() al calcular/guardar un
        // presupuesto en estado Created: cuántas unidades Depto/Oficina dividieron
        // esta categoría después de exoneraciones (totalApartments - nroExcepciones).
        // Solo aplica a categorías con divisor por unidad (Agua y Fija) -- null para
        // las %-based (por área) y para presupuestos guardados antes de este campo.
        // Ver Detalle y el PDF lo usan en vez de recalcular con la composición ACTUAL
        // del edificio, para que un presupuesto YA PUBLICADO no cambie de desglose si
        // luego se agregan/quitan unidades o exoneraciones.
        public int? NroApartments { get; set; }
    }
}
