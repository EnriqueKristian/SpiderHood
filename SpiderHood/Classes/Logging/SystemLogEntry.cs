namespace SpiderHood.Models
{
    // Fila de dbo.SystemLog. Level es el nombre de Microsoft.Extensions.Logging.LogLevel
    // (Critical/Error/Warning/Information) -- se reusa esa escala en vez de inventar una
    // propia. Ver Services/Logging/DatabaseLoggerProvider.cs.
    public class SystemLogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Level { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public Guid? IdUser { get; set; }
        public string? UserName { get; set; }
        public Guid? IdBuilding { get; set; }
    }
}
