namespace SpiderHood.Models
{
    // Resultado de generar una cuota extraordinaria: cuántas unidades quedaron con
    // cargo y el total repartido, para mostrar una confirmación en la UI.
    public class CuotaExtraordinariaResultado
    {
        public bool Exito { get; set; }
        public string Mensaje { get; set; } = string.Empty;
        public Guid IdBudgetHeader { get; set; }
        public int UnidadesConCargo { get; set; }
        public decimal TotalRepartido { get; set; }
    }
}
