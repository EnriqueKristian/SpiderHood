using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpiderHood.Models;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace SpiderHood.Services
{
    // Genera el PDF descargable de un Acta de Reunión (Docs/Pendientes-Negocio-
    // Consolidado.md #21, Fase 3). Mismo patrón stateless que
    // BoletaPagoExportService -- se instancia, se llama GeneratePdf() una vez y
    // se descarta. El cuerpo del acta es el texto plano ya compuesto por
    // IReunionService.ComponerContenidoActaAsync (una línea = un párrafo).
    public class ActaExportService
    {
        private readonly Acta _acta;
        private readonly Reunion _reunion;

        public ActaExportService(Acta acta, Reunion reunion)
        {
            _acta = acta;
            _reunion = reunion;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GeneratePdf()
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));
                    page.Content().Element(Compose);
                });
            });

            return document.GeneratePdf();
        }

        private void Compose(IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Element(ComposeHeader);
                column.Item().PaddingTop(10).Element(ComposeCuerpo);
                column.Item().PaddingTop(20).Element(ComposeFirmas);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container.BorderBottom(2).BorderColor(Colors.Blue.Darken2).PaddingBottom(8).Column(col =>
            {
                col.Item().Text(_reunion.Tipo == TipoReunion.Ordinaria ? "ACTA DE REUNIÓN ORDINARIA" : "ACTA DE REUNIÓN EXTRAORDINARIA")
                    .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
                col.Item().Text(_reunion.Titulo).FontSize(11);
                col.Item().Text(_acta.Estado == EstadoActa.Firmada ? "FIRMADA" : "BORRADOR")
                    .FontSize(8).Bold().FontColor(_acta.Estado == EstadoActa.Firmada ? Colors.Green.Darken2 : Colors.Orange.Darken2);
            });
        }

        private void ComposeCuerpo(IContainer container)
        {
            container.Column(col =>
            {
                foreach (var linea in _acta.ContenidoGenerado.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(linea))
                    {
                        col.Item().Height(6);
                        continue;
                    }

                    var esTitulo = linea.ToUpperInvariant() == linea && linea.Trim().Length > 3 && !linea.TrimStart().StartsWith("-");
                    var texto = col.Item().Text(linea);
                    if (esTitulo)
                        texto.Bold().FontSize(10.5f);
                }
            });
        }

        private void ComposeFirmas(IContainer container)
        {
            if (_acta.Estado != EstadoActa.Firmada)
            {
                container.Text("Documento sin firmar -- borrador generado automáticamente por el sistema.")
                    .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).PaddingTop(15).Row(row =>
            {
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().BorderTop(1).BorderColor(Colors.Black).PaddingTop(3).AlignCenter().Text(_acta.NombrePresidente ?? "");
                    col.Item().AlignCenter().Text("Presidente").FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text(_acta.FirmaPresidenteEn?.ToString("dd/MM/yyyy") ?? "").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                row.ConstantItem(30);
                row.RelativeItem().AlignCenter().Column(col =>
                {
                    col.Item().BorderTop(1).BorderColor(Colors.Black).PaddingTop(3).AlignCenter().Text(_acta.NombreSecretario ?? "");
                    col.Item().AlignCenter().Text("Secretario").FontSize(8).FontColor(Colors.Grey.Darken1);
                    col.Item().AlignCenter().Text(_acta.FirmaSecretarioEn?.ToString("dd/MM/yyyy") ?? "").FontSize(7).FontColor(Colors.Grey.Darken1);
                });
            });
        }
    }
}
