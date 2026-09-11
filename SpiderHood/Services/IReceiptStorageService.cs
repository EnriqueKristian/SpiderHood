using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #18b -- capa de persistencia sobre
    // InstallmentExportService (Classes/Utilities.cs), que sólo sabe RENDERIZAR
    // un PDF a partir de datos, sin guardar nada. Acá se decide si hace falta
    // generarlo de nuevo o si ya existe uno guardado -- y, si ya existe, SIEMPRE
    // se sirve ese (nunca se regenera), para que un recibo no cambie de
    // contenido si el edificio cambia de banco/administrador/pie de recibo
    // después de emitido (bug de integridad documentado en el punto de arriba).
    public interface IReceiptStorageService
    {
        Task<byte[]> GetOrGenerateReceiptAsync(
            InstallmentExportService exportService, Installment installment, Guid idBuilding, string performedBy);

        // Genera (o reusa) el recibo de cada cuota y los junta en un solo ZIP --
        // reemplaza a InstallmentExportService.GenerateAllReceiptsZip para que el
        // momento de publicar un presupuesto (BudgetGenerator.razor), que ya
        // recorre todas las cuotas del periodo, sea también el momento en que
        // cada recibo queda guardado de una vez.
        Task<byte[]> GetOrGenerateAllReceiptsZipAsync(
            InstallmentExportService exportService, List<Installment> installments, Guid idBuilding, string performedBy);
    }

    public class ReceiptStorageService : IReceiptStorageService
    {
        private readonly BDLayout _ec;
        private readonly IFileStorageService _fileStorage;
        private readonly ILogger<ReceiptStorageService> _logger;

        public ReceiptStorageService(
            IDbContextFactory<SpiderHoodContext> contextFactory, IFileStorageService fileStorage, ILogger<ReceiptStorageService> logger)
        {
            _ec = new BDLayout(contextFactory);
            _fileStorage = fileStorage;
            _logger = logger;
        }

        public async Task<byte[]> GetOrGenerateReceiptAsync(
            InstallmentExportService exportService, Installment installment, Guid idBuilding, string performedBy)
        {
            var existente = await _ec.GetReceiptFileByInstallmentAsync(installment.IdInstallment);
            if (existente is not null)
            {
                var bytesGuardados = await _fileStorage.ReadAsync(existente.FilePath);
                if (bytesGuardados is not null)
                    return bytesGuardados;

                _logger.LogWarning(
                    "ReceiptFile {IdReceiptFile} registrado para Installment {IdInstallment} pero el archivo no está en storage ({Path}) -- se regenera como respaldo.",
                    existente.IdReceiptFile, installment.IdInstallment, existente.FilePath);
            }

            var pdfBytes = exportService.GenerateReceipt(installment);

            // Edificio -> Unidad -> Año -> Mes (pedido del usuario 2026-09-11,
            // calcado del orden que ya usan a mano en Google Drive: Edificio >
            // DPTO > Año > Mes). Mes lleva el número adelante ("04-Abril", no
            // sólo "Abril") -- el ejemplo de Drive ordena los meses
            // ALFABÉTICAMENTE (ABRIL, AGOSTO, ENERO...) porque el nombre del mes
            // solo no ordena cronológicamente; con el número adelante, la carpeta
            // sí queda en orden Ene-Dic. Nombre del mes siempre en español
            // (CultureInfo explícito "es-PE", no CurrentCulture -- el locale del
            // servidor no está garantizado en Program.cs, no vale la pena
            // arriesgar que salga "April" en vez de "Abril" en un server con
            // locale en inglés).
            var nombreMes = installment.Period.ToString("MMMM", System.Globalization.CultureInfo.GetCultureInfo("es-PE"));
            nombreMes = char.ToUpper(nombreMes[0]) + nombreMes[1..];
            var carpetaMes = $"{installment.Period:MM}-{nombreMes}";

            // Cada nivel es un elemento del array (no un string armado a mano con
            // "/"), así un nombre de unidad con "/" (ej. "Cochera 12/A") no puede
            // crear un nivel de más por accidente -- SaveAsync sanitiza cada
            // elemento como una unidad. El nombre del archivo queda simple (sólo
            // el Guid) porque Edificio/Unidad/Año/Mes ya quedan expresados en la
            // carpeta.
            var relativePath = await _fileStorage.SaveAsync(
                new[] { "receipts", idBuilding.ToString(), installment.UnitName, installment.Period.Year.ToString(), carpetaMes },
                $"{installment.IdInstallment}.pdf",
                pdfBytes);

            try
            {
                await _ec.AddNewRecordAsync(new ReceiptFile
                {
                    IdInstallment = installment.IdInstallment,
                    IdBuilding = idBuilding,
                    FilePath = relativePath,
                    FileSizeBytes = pdfBytes.Length,
                    GeneratedBy = performedBy
                });
            }
            catch (RepositoryException ex)
            {
                // IX_ReceiptFile_Installment (UNIQUE) rechazó el INSERT -- alguien más
                // (doble click, dos pestañas) ya generó y guardó el recibo de esta
                // misma cuota mientras tanto. El otro ya ganó -- se sirve lo que
                // quedó registrado en vez de dejar en storage un archivo huérfano sin
                // fila en ReceiptFile.
                _logger.LogInformation(ex,
                    "Conflicto al registrar ReceiptFile para Installment {IdInstallment} -- probablemente una generación concurrente, se busca la que ganó.",
                    installment.IdInstallment);

                var ganador = await _ec.GetReceiptFileByInstallmentAsync(installment.IdInstallment);
                var bytesGanador = ganador is null ? null : await _fileStorage.ReadAsync(ganador.FilePath);
                if (bytesGanador is not null)
                    return bytesGanador;

                throw;
            }

            return pdfBytes;
        }

        public async Task<byte[]> GetOrGenerateAllReceiptsZipAsync(
            InstallmentExportService exportService, List<Installment> installments, Guid idBuilding, string performedBy)
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var installment in installments)
                {
                    var pdfBytes = await GetOrGenerateReceiptAsync(exportService, installment, idBuilding, performedBy);
                    var entry = archive.CreateEntry($"Recibo_{installment.UnitName}_{installment.Period:yyyyMM}.pdf");
                    using var entryStream = entry.Open();
                    await entryStream.WriteAsync(pdfBytes);
                }
            }

            return memoryStream.ToArray();
        }
    }
}
