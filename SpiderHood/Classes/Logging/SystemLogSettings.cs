namespace SpiderHood.Models
{
    // Configuración global (una sola fila) que habilita/deshabilita el logging a BD y
    // define el nivel mínimo y la retención -- la edita el Super Usuario (SysAdmin) desde
    // /Settings/SystemLogs. Apagado por defecto para no llenar la BD con nadie pidiéndolo.
    public class SystemLogSettings
    {
        public bool IsEnabled { get; set; }
        public string MinLevel { get; set; } = "Error";
        public int RetentionDays { get; set; } = 30;
        public string? UpdatedBy { get; set; }
        public DateTime? UpdatedOn { get; set; }
    }
}
