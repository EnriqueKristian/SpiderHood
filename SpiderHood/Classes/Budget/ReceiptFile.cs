namespace SpiderHood.Models
{
    // Docs/Pendientes-Negocio-Consolidado.md #18b -- registro de dónde quedó
    // guardado el PDF ya generado de una cuota (ver IReceiptStorageService).
    // Inmutable una vez creado: no hay UPD_ReceiptFile a propósito, un recibo
    // ya generado nunca se vuelve a generar con datos distintos.
    public class ReceiptFile
    {
        public Guid IdReceiptFile { get; set; } = Guid.NewGuid();
        public Guid IdInstallment { get; set; }
        public Guid IdBuilding { get; set; }
        // Ruta relativa dentro del storage configurado (IFileStorageService) --
        // nunca una ruta absoluta ni una URL pública directa.
        public string FilePath { get; set; } = string.Empty;
        public int FileSizeBytes { get; set; }
        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
    }
}
