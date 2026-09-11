using System.Text.RegularExpressions;

namespace SpiderHood.Services
{
    // Storage de archivos generados/subidos por la app -- primer uso: PDFs de
    // Recibos (Docs/Pendientes-Negocio-Consolidado.md #18b); más adelante,
    // fotos/video de Incidencias (#18a) reusan el mismo servicio. Implementación
    // local a disco para el piloto, fuera de wwwroot a propósito (estos archivos
    // pueden tener datos personales/financieros -- nunca deben quedar
    // alcanzables como estático sin pasar por la app). Migrar a Azure Blob si el
    // piloto escala o el hosting pasa a contenedores efímeros sin disco
    // persistente entre despliegues (ver el documento de arriba).
    public interface IFileStorageService
    {
        // category agrupa en subcarpetas -- acepta más de un nivel separado por
        // '/' (ej. "receipts/{idBuilding}") para no amontonar TODOS los archivos
        // de TODOS los edificios en una sola carpeta plana. Cada segmento (de
        // category y fileName) se sanitiza acá adentro -- el caller arma el
        // "/" para separar niveles, nunca ".." ni una ruta ya resuelta. Devuelve
        // la ruta RELATIVA (para persistir en BD), nunca la ruta absoluta en disco.
        Task<string> SaveAsync(string category, string fileName, byte[] content);

        // Null si el archivo no existe (ej. se perdió entre despliegues, o la
        // ruta ya no es válida) -- el caller decide qué hacer, nunca tira excepción
        // por esto.
        Task<byte[]?> ReadAsync(string relativePath);
    }

    public class LocalFileStorageService : IFileStorageService
    {
        private readonly string _basePath;
        private readonly ILogger<LocalFileStorageService> _logger;

        public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
        {
            _logger = logger;
            // Default fuera de wwwroot (AppContext.BaseDirectory es la carpeta de
            // publicación de la app, no la del código fuente) -- configurable por si
            // el hosting real necesita un disco/mount aparte. "" en appsettings.json
            // (valor default, sin configurar todavía) cuenta como "no configurado",
            // igual que null.
            var configurado = configuration["Storage:LocalBasePath"];
            _basePath = string.IsNullOrWhiteSpace(configurado)
                ? Path.Combine(AppContext.BaseDirectory, "App_Data", "storage")
                : configurado;
            Directory.CreateDirectory(_basePath);
        }

        public async Task<string> SaveAsync(string category, string fileName, byte[] content)
        {
            // Cada nivel de category se sanitiza POR SEPARADO (no la cadena entera
            // de una), así "receipts/{idBuilding}" arma dos carpetas anidadas en
            // vez de una sola carpeta llamada literalmente "receipts_{idBuilding}".
            var safeCategoryParts = category
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(SanitizeSegment)
                .ToArray();
            if (safeCategoryParts.Length == 0)
                throw new ArgumentException("category no puede estar vacío.", nameof(category));

            var safeFileName = SanitizeSegment(fileName);

            var directory = Path.Combine(_basePath, Path.Combine(safeCategoryParts));
            Directory.CreateDirectory(directory);

            var fullPath = Path.Combine(directory, safeFileName);
            await File.WriteAllBytesAsync(fullPath, content);

            var relativePath = string.Join('/', safeCategoryParts.Append(safeFileName));
            _logger.LogInformation("Archivo guardado en storage: {RelativePath} ({Size} bytes)", relativePath, content.Length);
            return relativePath;
        }

        public async Task<byte[]?> ReadAsync(string relativePath)
        {
            var fullPath = ResolveSafePath(relativePath);
            if (fullPath is null || !File.Exists(fullPath))
                return null;

            return await File.ReadAllBytesAsync(fullPath);
        }

        // Evita path traversal (".." o separadores propios) en cualquier segmento
        // que termine armando una ruta en disco. Hoy category/fileName los arma el
        // propio backend (nunca un nombre de archivo que trajo el usuario), pero no
        // vale la pena arriesgar -- sobre todo pensando en 18a (fotos de
        // Incidencias), donde el nombre original del archivo sí puede venir de
        // afuera.
        private static string SanitizeSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                throw new ArgumentException("El segmento de ruta no puede estar vacío.", nameof(segment));

            var cleaned = Regex.Replace(segment, @"[^A-Za-z0-9._-]", "_");
            return cleaned.Replace("..", "_");
        }

        // Vuelve a armar la ruta absoluta y confirma que sigue adentro de
        // _basePath -- una relativePath que viniera manipulada ("../../otra_cosa")
        // no debería poder leer nada fuera de la carpeta de storage.
        private string? ResolveSafePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var combined = Path.GetFullPath(Path.Combine(_basePath, relativePath));
            var baseFull = Path.GetFullPath(_basePath) + Path.DirectorySeparatorChar;

            return combined.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) ? combined : null;
        }
    }
}
