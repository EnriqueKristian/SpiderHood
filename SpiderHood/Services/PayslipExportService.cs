using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SpiderHood.Models;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace SpiderHood.Services
{
    // Genera el PDF descargable de una Payslip (Employee y Payroll -- Fase
    // 2, sección 9 de la especificación: "lo mínimo indispensable" -- Empleador,
    // Trabajador, Periodo, Ingresos, Descuentos, Neto a pagar). Mismo patrón
    // stateless que InstallmentExportService (Classes/Utilities.cs) -- se
    // instancia, se llama GeneratePdf() una vez y se descarta.
    public class PayslipExportService
    {
        private readonly Payslip _boleta;
        private readonly string _razonSocial;
        private readonly string _ruc;

        public PayslipExportService(Payslip boleta, string? razonSocial, string? ruc)
        {
            _boleta = boleta;
            _razonSocial = string.IsNullOrWhiteSpace(razonSocial) ? "(razón social no configurada)" : razonSocial;
            _ruc = string.IsNullOrWhiteSpace(ruc) ? "-" : ruc;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GeneratePdf()
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Content().Element(Compose);
                });
            });

            return document.GeneratePdf();
        }

        private void Compose(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Black).Column(column =>
            {
                column.Item().Element(ComposeHeader);
                column.Item().Element(ComposeEmpleadorTrabajador);
                column.Item().Padding(10).Element(ComposeConceptos);
                column.Item().Element(ComposeNeto);
                column.Item().Element(ComposeAportesEmpleador);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            var mesAno = char.ToUpper(_boleta.NombrePeriodo[0]) + _boleta.NombrePeriodo.Substring(1);

            container.BorderBottom(2).BorderColor(Colors.Blue.Darken2).Padding(10).Column(col =>
            {
                col.Item().AlignCenter().Text(_razonSocial.ToUpper()).FontSize(13).Bold();
                col.Item().AlignCenter().Text($"RUC: {_ruc}").FontSize(8).FontColor(Colors.Grey.Darken1);
                col.Item().AlignCenter().Text($"Boleta de Pago - {mesAno}").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
            });
        }

        private void ComposeEmpleadorTrabajador(IContainer container)
        {
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
            {
                col.Item().Text($"TRABAJADOR: {_boleta.NombreEmployee.ToUpper()}").FontSize(10).Bold();
                col.Item().Text($"DNI: {_boleta.DNI}   Cargo: {_boleta.Cargo}");
                col.Item().Text($"Fecha ingreso: {_boleta.FechaIngreso:dd/MM/yyyy}" +
                    (_boleta.FechaCese != null ? $"   Fecha cese: {_boleta.FechaCese:dd/MM/yyyy}" : ""));
                col.Item().Text($"Sistema pensionario: {_boleta.SistemaPensionario}   Régimen: {TraducirRegimen(_boleta.TipoRegimen)}");
            });
        }

        private void ComposeConceptos(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(80);
                });

                table.Header(header =>
                {
                    header.Cell().Text("CONCEPTO").Bold();
                    header.Cell().AlignRight().Text("MONTO").Bold();
                });

                var ingresos = _boleta.Detalle.Where(d => d.TipoConcepto == "Ingreso").ToList();
                if (ingresos.Count > 0)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(6).Background(Colors.Blue.Lighten4).Padding(3).Text("INGRESOS").Bold();
                    foreach (var d in ingresos)
                    {
                        table.Cell().PaddingTop(2).Text(d.Descripcion);
                        table.Cell().PaddingTop(2).AlignRight().Text($"S/ {d.Monto:N2}");
                    }
                }

                var descuentos = _boleta.Detalle.Where(d => d.TipoConcepto == "DescuentoTrabajador").ToList();
                if (descuentos.Count > 0)
                {
                    table.Cell().ColumnSpan(2).PaddingTop(6).Background(Colors.Orange.Lighten4).Padding(3).Text("DESCUENTOS").Bold();
                    foreach (var d in descuentos)
                    {
                        table.Cell().PaddingTop(2).Text(d.Descripcion);
                        table.Cell().PaddingTop(2).AlignRight().Text($"S/ {d.Monto:N2}");
                    }
                }
            });
        }

        private void ComposeNeto(IContainer container)
        {
            container.Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text($"Total ingresos: S/ {_boleta.TotalIngresos:N2}").FontSize(9);
                    col.Item().Text($"Total descuentos: S/ {_boleta.TotalDescuentos:N2}").FontSize(9);
                });

                row.ConstantItem(130).Background(Colors.Blue.Darken2).Padding(8).Column(col =>
                {
                    col.Item().AlignCenter().Text("NETO A PAGAR").FontSize(8).FontColor(Colors.White);
                    col.Item().AlignCenter().Text($"S/ {_boleta.NetoAPagar:N2}").FontSize(14).Bold().FontColor(Colors.White);
                });
            });
        }

        private void ComposeAportesEmpleador(IContainer container)
        {
            var aportes = _boleta.Detalle.Where(d => d.TipoConcepto == "AporteEmpleador").ToList();
            if (aportes.Count == 0)
                return;

            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
            {
                col.Item().Text("Aportes a cargo del empleador (informativo, no afectan el neto a pagar):").FontSize(7).FontColor(Colors.Grey.Darken1);
                foreach (var a in aportes)
                {
                    col.Item().Text($"{a.Descripcion}: S/ {a.Monto:N2}").FontSize(7).FontColor(Colors.Grey.Darken1);
                }
            });
        }

        private static string TraducirRegimen(string tipoRegimen) => tipoRegimen switch
        {
            nameof(LaborRegimeType.Microempresa) => "Microempresa",
            nameof(LaborRegimeType.PequenaEmpresa) => "Pequeña Empresa",
            nameof(LaborRegimeType.RegimenGeneral) => "Régimen General",
            _ => tipoRegimen
        };
    }
}
