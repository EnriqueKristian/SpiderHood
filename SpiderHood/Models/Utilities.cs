using ClosedXML.Excel;
using OfficeOpenXml;
using SpiderHood.Components.Pages.Components;
using System.ComponentModel;
using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using IContainer = QuestPDF.Infrastructure.IContainer;

namespace SpiderHood.Models
{
    public class Utilities
    {
      
    }


    public class ConfirmationUtil
    {
        //private ConfirmationModal? _confirmationModal;
        private Action? _pendingAction;
        private string _pendingActionName = "";
        public bool _isLoading = false;
        public string _currentOperation = "";

        public async Task ExecuteWithConfirmation(Func<Task> action, string actionName, ConfirmationModal? _confirmationModal, string message = "", string type = "warning", bool isCancelOnly = false)
        {
            _pendingAction = () =>
            {
                Task task = ExecuteWithLoading(async () => { await action.Invoke(); }, actionName);
            };

            _pendingActionName = actionName;

            var defaultMessage = type == "danger"
                ? $"¿Está seguro de {actionName.ToLower()}? Esta acción no se puede deshacer."
                : $"¿Desea {actionName.ToLower()}?";

            _confirmationModal?.Show(type);
            _confirmationModal?.Message = string.IsNullOrEmpty(message) ? defaultMessage : message;
            _confirmationModal?.IsCancelOnly = isCancelOnly;
        }

        public async Task OnConfirmationResult(bool confirmed)
        {
            if (confirmed && _pendingAction != null)
            {
                _pendingAction.Invoke();
            }

            _pendingAction = null;
            _pendingActionName = "";
        }

        public async Task StartLoading(string operation)
        {
            _isLoading = true;
            _currentOperation = operation;
        }

        public void StopLoading()
        {
            _isLoading = false;
            _currentOperation = "";
        }

        public async Task ExecuteWithLoading(Func<Task> operation, string operationName)
        {
            try
            {
                await StartLoading(operationName);
                await operation.Invoke();
            }
            finally
            {
                StopLoading();
            }
        }
    }

    public class ExcelExportService
    {
        public async Task ExportWaterReadingsAsync(
            List<ServiceReadingDetail> lecturas,
            DateTime periodo,
            decimal total)
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Lecturas");

            // Configurar cabeceras
            worksheet.Cells[1, 1].Value = "Unidad";
            worksheet.Cells[1, 2].Value = "Lectura Actual";
            worksheet.Cells[1, 3].Value = "Consumo";
            worksheet.Cells[1, 4].Value = "Monto";

            // Llenar datos
            for (int i = 0; i < lecturas.Count; i++)
            {
                worksheet.Cells[i + 2, 1].Value = lecturas[i].Code;
                worksheet.Cells[i + 2, 2].Value = lecturas[i].CurrentReading;
                worksheet.Cells[i + 2, 3].Value = lecturas[i].Consumption;
                worksheet.Cells[i + 2, 4].Value = lecturas[i].CalculatedAmount;
            }

            // Agregar total
            worksheet.Cells[lecturas.Count + 3, 3].Value = "TOTAL:";
            worksheet.Cells[lecturas.Count + 3, 4].Value = total;

            // Guardar archivo
            var bytes = package.GetAsByteArray();
            // TODO: Implementar descarga
        }

        public async Task<(string Filename, MemoryStream Stream)> ExportarPlantillaVacia(ServiceReadingState state, List<UnitView> unidades)
        {
            try
            {
                using var memoryStream = new MemoryStream();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Plantilla");

                    worksheet.Cell(1, 1).Value = "Periodo";
                    worksheet.Cell(1, 1).Style.Font.Bold = true;
                    worksheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.LightGray;

                    worksheet.Cell(1, 2).Value = state.CurrentReading.Period;

                    // Encabezados
                    var headers = new[] { "Dpto.", "Lect. Actual", "Fecha. Lectura" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(3, i + 1).Value = headers[i];
                        worksheet.Cell(3, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    // Formato para ID de unidad
                    worksheet.Column(1).Style.NumberFormat.Format = "@"; // Texto

                    // Agregar algunas unidades como ejemplo
                    //var unidades = await ObtenerUnidadesDelEdificio();
                    int row = 4;

                    foreach (var unidad in unidades)
                    {
                        worksheet.Cell(row, 1).Value = unidad.Number;
                        worksheet.Cell(row, 2).Value = ""; // Lectura actual vacía
                        worksheet.Cell(row, 3).Value = ""; // Observaciones vacías
                        row++;
                    }

                    // Instrucciones
                    worksheet.Cell(row + 2, 1).Value = "INSTRUCCIONES:";
                    worksheet.Cell(row + 2, 1).Style.Font.Bold = true;
                    worksheet.Cell(row + 3, 1).Value = "1. Complete la columna 'CurrentReading' con la lectura actual";
                    worksheet.Cell(row + 4, 1).Value = "2. No modifique la columna 'Dpto.'";
                    worksheet.Cell(row + 5, 1).Value = "3. Elimine seccion de INSTRUCCIONES";
                    worksheet.Cell(row + 6, 1).Value = "4. Guarde el archivo y cárguelo en el sistema";

                    workbook.SaveAs(memoryStream);
                }

                var fileName = $"Plantilla_Lecturas_{DateTime.Now:yyyyMMdd}.xlsx";

                return (fileName, memoryStream);
                //await DescargarArchivo(fileName, memoryStream.ToArray());

                //await MostrarMensajeExito("Plantilla descargada exitosamente");
            }
            catch (Exception ex)
            {
                //await MostrarMensajeError($"Error al generar plantilla: {ex.Message}");
                throw new Exception($"Error al generar plantilla: {ex.Message}", ex);
            }
        }
        public async Task<(string Filename, MemoryStream Stream)> ExportarComparativoConsumo(ServiceReadingState state)
        {
            try
            {
                if (state.PreviousReadingDetail == null || !state.PreviousReadingDetail.Any())
                {
                    //await MostrarMensajeError("No hay datos del período anterior para comparar");
                    return (string.Empty, null!);
                }

                using var memoryStream = new MemoryStream();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Comparativo");

                    // Título
                    worksheet.Cell("A1").Value = "COMPARATIVO DE CONSUMO";
                    worksheet.Cell("A1").Style.Font.Bold = true;
                    worksheet.Cell("A1").Style.Font.FontSize = 14;
                    worksheet.Range("A1:G1").Merge();

                    // Períodos
                    //worksheet.Cell("A2").Value = $"Período Anterior: {_ultimoPeriodo.AddMonths(-1):MMMM yyyy}";
                    worksheet.Cell("A3").Value = $"Período Actual: {state.Period:MMMM yyyy}";

                    // Encabezados
                    int row = 5;
                    var headers = new[]
                    {
                "Unidad", "Consumo Anterior", "Consumo Actual", "Diferencia",
                "Variación %", "Tendencia", "Ahorro/Incremento"
            };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(row, i + 1).Value = headers[i];
                        worksheet.Cell(row, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    // Datos comparativos
                    row++;
                    foreach (var lecturaActual in state.CurrentReadingDetail)
                    {
                        var lecturaAnterior = state.PreviousReadingDetail
                            .FirstOrDefault(l => l.GroupNumber == lecturaActual.GroupNumber);

                        double consumoAnterior = lecturaAnterior?.Consumption ?? 0;
                        double consumoActual = lecturaActual.Consumption;
                        double diferencia = consumoActual - consumoAnterior;
                        double variacionPorcentual = consumoAnterior > 0 ?
                            (diferencia / consumoAnterior) * 100 : 0;

                        worksheet.Cell(row, 1).Value = lecturaActual.GroupNumber;
                        worksheet.Cell(row, 2).Value = consumoAnterior;
                        worksheet.Cell(row, 3).Value = consumoActual;
                        worksheet.Cell(row, 4).Value = diferencia;
                        worksheet.Cell(row, 5).Value = variacionPorcentual / 100; // Para formato %
                        worksheet.Cell(row, 5).Style.NumberFormat.Format = "0.00%";

                        // Tendencia con iconos
                        worksheet.Cell(row, 6).Value = diferencia > 0 ? "↑" : diferencia < 0 ? "↓" : "→";
                        worksheet.Cell(row, 6).Style.Font.FontColor =
                            diferencia > 0 ? XLColor.Red : diferencia < 0 ? XLColor.Green : XLColor.Black;

                        // Cálculo de ahorro/incremento en dinero
                        decimal montoDiferencia = 0;
                        if (lecturaActual.CalculationDetail != null && lecturaAnterior?.CalculationDetail != null)
                        {
                            montoDiferencia = lecturaActual.CalculationDetail.TotalConIGV -
                                             lecturaAnterior.CalculationDetail.TotalConIGV;
                        }

                        worksheet.Cell(row, 7).Value = montoDiferencia;
                        worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                        worksheet.Cell(row, 7).Style.Font.FontColor =
                            montoDiferencia < 0 ? XLColor.Green : XLColor.Red;

                        // Resaltar variaciones significativas
                        if (Math.Abs(variacionPorcentual) > 50)
                        {
                            worksheet.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.LightYellow;
                        }

                        row++;
                    }

                    // Totales
                    row++;
                    worksheet.Cell(row, 2).Value = state.PreviousReadingDetail.Sum(l => l.Consumption);
                    worksheet.Cell(row, 3).Value = state.CurrentReadingDetail.Sum(l => l.Consumption);
                    worksheet.Cell(row, 4).Value = worksheet.Cell(row, 3).GetValue<decimal>() -
                                                  worksheet.Cell(row, 2).GetValue<decimal>();

                    // Formato
                    worksheet.Columns("B:D").Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(memoryStream);
                }

                var fileName = $"Comparativo_{state.Period:yyyyMM}.xlsx";

                return (fileName, memoryStream);
                //await DescargarArchivo(fileName, memoryStream.ToArray());

                //await MostrarMensajeExito("Reporte comparativo generado exitosamente");
            }
            catch (Exception ex)
            {
                //await MostrarMensajeError($"Error al generar comparativo: {ex.Message}");
                throw new Exception($"Error al generar comparativo: {ex.Message}", ex);
            }
        }
        public async Task<byte[]> GenerarExcelAsync(ServiceReadingState state)
        {
            using var memoryStream = new MemoryStream();

            using (var workbook = new XLWorkbook())
            {
                // Hoja 1: Datos detallados
                var worksheet = workbook.Worksheets.Add("Lecturas Detalladas");
                GenerarHojaDetallada(worksheet, state);

                // Hoja 2: Resumen
                var resumenWorksheet = workbook.Worksheets.Add("Resumen");
                GenerarHojaResumen(resumenWorksheet, state);

                // Hoja 3: Estadísticas
                var estadisticasWorksheet = workbook.Worksheets.Add("Estadísticas");
                GenerarHojaEstadisticas(estadisticasWorksheet, state);

                // Ajustar columnas automáticamente
                worksheet.Columns().AdjustToContents();
                resumenWorksheet.Columns().AdjustToContents();
                estadisticasWorksheet.Columns().AdjustToContents();

                // Guardar en el stream
                workbook.SaveAs(memoryStream);
            }

            return memoryStream.ToArray();
        }
        private void GenerarHojaDetallada(IXLWorksheet worksheet, ServiceReadingState state)
        {
            // Título
            worksheet.Cell("A1").Value = "REPORTE DE LECTURAS DE AGUA";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;
            worksheet.Range("A1:G1").Merge();
            worksheet.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Subtítulo
            worksheet.Cell("A2").Value = $"Período: {state.Period:MMMM yyyy}";
            worksheet.Cell("A2").Style.Font.Bold = true;
            worksheet.Range("A2:G2").Merge();

            // Fecha de generación
            worksheet.Cell("A3").Value = $"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}";
            worksheet.Range("A3:G3").Merge();
            worksheet.Cell("A3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Encabezados de columna
            var headers = new[]
            {
            "Unidad", "Lectura Anterior", "Lectura Actual", "Consumo (m³)",
            "Cargo Fijo", "Monto Consumo", "Subtotal", "IGV (18%)", "TOTAL"
        };

            int row = 5; // Fila donde empiezan los encabezados

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Datos de las lecturas
            row++;
            foreach (var lectura in state.CurrentReadingDetail.OrderBy(l => l.GroupNumber))
            {
                worksheet.Cell(row, 1).Value = lectura.GroupNumber;
                worksheet.Cell(row, 2).Value = lectura.PreviousReading;
                worksheet.Cell(row, 3).Value = lectura.CurrentReading;
                worksheet.Cell(row, 4).Value = lectura.Consumption;

                if (lectura.CalculationDetail != null)
                {
                    worksheet.Cell(row, 5).Value = lectura.CalculationDetail.CargoFijo;
                    worksheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(row, 6).Value = lectura.Consumption;
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(row, 7).Value = lectura.CalculationDetail.Subtotal;
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(row, 8).Value = lectura.CalculationDetail.IGV;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";

                    worksheet.Cell(row, 9).Value = lectura.CalculationDetail.TotalConIGV;
                    worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";

                    // Resaltar consumo mínimo
                    if (lectura.Minimum)
                    {
                        worksheet.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.LightYellow;
                    }
                }

                // Aplicar bordes a toda la fila
                worksheet.Range(row, 1, row, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                row++;
            }

            // Total general
            row++;
            worksheet.Cell(row, 8).Value = "TOTAL GENERAL:";
            worksheet.Cell(row, 8).Style.Font.Bold = true;
            worksheet.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            decimal TotalGeneral = state.CurrentReadingDetail.Sum(l => l.CalculatedAmount);

            worksheet.Cell(row, 9).Value = TotalGeneral;
            worksheet.Cell(row, 9).Style.Font.Bold = true;
            worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
            worksheet.Cell(row, 9).Style.Fill.BackgroundColor = XLColor.LightGreen;
            worksheet.Cell(row, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // Formato de números
            worksheet.Column(4).Style.NumberFormat.Format = "#,##0"; // Consumo
            worksheet.Columns(5, 9).Style.NumberFormat.Format = "#,##0.00"; // Montos
        }
        private void GenerarHojaResumen(IXLWorksheet worksheet, ServiceReadingState state)
        {
            // Título
            worksheet.Cell("A1").Value = "RESUMEN DEL PERÍODO";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;
            worksheet.Range("A1:B1").Merge();

            // Datos del resumen
            int row = 3;

            var resumenData = new Dictionary<string, object>
            {
                ["Período"] = state.Period.ToString("MMMM yyyy"),
                ["Total Unidades"] = state.CurrentReadingDetail.Count,
                ["Total Consumo (m³)"] = state.CurrentReadingDetail.Sum(l => l.Consumption),
                ["Consumo Promedio (m³)"] = state.CurrentReadingDetail.Average(l => l.Consumption),
                ["Unidades con Consumo Mínimo"] = state.CurrentReadingDetail.Count(l => l.Minimum),
                ["Cargo Fijo Aplicado"] = state.CargoFijo,
                ["Subtotal"] = state.CurrentReadingDetail.Sum(l => l.CalculationDetail!.Subtotal),
                ["IGV Total"] = state.CurrentReadingDetail.Sum(l => l.CalculationDetail?.IGV ?? 0),
                ["TOTAL GENERAL"] = state.CurrentReadingDetail.Sum(l => l.CalculatedAmount)
            };

            foreach (var item in resumenData)
            {
                worksheet.Cell(row, 1).Value = item.Key;
                worksheet.Cell(row, 1).Style.Font.Bold = true;

                switch (item.Key)
                {
                    case "Período":
                        worksheet.Cell(row, 2).Value = item.Value.ToString();
                        break;
                    case "Total Unidades":
                    case "Unidades con Consumo Mínimo":
                        worksheet.Cell(row, 2).Value = (int)item.Value;
                        break;
                    case "Cargo Fijo Aplicado":
                    case "Subtotal":
                    case "IGV Total":
                    case "TOTAL GENERAL":
                        worksheet.Cell(row, 2).Value = (decimal)item.Value;
                        break;
                    default:
                        worksheet.Cell(row, 2).Value = (double)item.Value;
                        break;
                }

                // Formato para montos y números
                if (item.Key.Contains("TOTAL") || item.Key.Contains("Subtotal") ||
                    item.Key.Contains("IGV") || item.Key.Contains("Cargo Fijo"))
                {
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                }
                else if (item.Key.Contains("Consumption"))
                {
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                }

                row++;
            }

            // Resaltar total general
            worksheet.Cell(row - 1, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
            worksheet.Cell(row - 1, 2).Style.Fill.BackgroundColor = XLColor.LightBlue;
            worksheet.Cell(row - 1, 2).Style.Font.Bold = true;

            // Ajustar columnas
            worksheet.Columns().AdjustToContents();
        }
        private void GenerarHojaEstadisticas(IXLWorksheet worksheet, ServiceReadingState state)
        {
            // Título
            worksheet.Cell("A1").Value = "ESTADÍSTICAS DE CONSUMO";
            worksheet.Cell("A1").Style.Font.Bold = true;
            worksheet.Cell("A1").Style.Font.FontSize = 14;
            worksheet.Range("A1:C1").Merge();

            // Calcular estadísticas
            var consumos = state.CurrentReadingDetail.Where(l => l.Consumption > 0).Select(l => l.Consumption).ToList();

            if (consumos.Any())
            {
                var estadisticas = new Dictionary<string, decimal>
                {
                    ["Consumo Máximo"] = (decimal)consumos.Max(),
                    ["Consumo Mínimo"] = (decimal)consumos.Min(),
                    ["Consumo Promedio"] = (decimal)consumos.Average(),
                    ["Consumo Mediano"] = CalcularMediana(consumos.Select(x => (decimal)x).ToList()),
                    ["Desviación Estándar"] = CalcularDesviacionEstandar(consumos.Select(x => (decimal)x).ToList())
                };

                int row = 3;
                foreach (var item in estadisticas)
                {
                    worksheet.Cell(row, 1).Value = item.Key;
                    worksheet.Cell(row, 1).Style.Font.Bold = true;

                    worksheet.Cell(row, 2).Value = item.Value;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

                    row++;
                }

                // Histograma de consumo
                row += 2;
                worksheet.Cell(row, 1).Value = "DISTRIBUCIÓN DE CONSUMO";
                worksheet.Cell(row, 1).Style.Font.Bold = true;
                worksheet.Range(row, 1, row, 3).Merge();

                row++;
                worksheet.Cell(row, 1).Value = "Rango (m³)";
                worksheet.Cell(row, 2).Value = "Cantidad";
                worksheet.Cell(row, 3).Value = "Porcentaje";

                var rangos = new[]
                {
                (0m, 10m, "0-10"),
                (10.01m, 20m, "11-20"),
                (20.01m, 30m, "20-30"),
                (30.01m, 50m, "30-50"),
                (50.01m, 100m, "50-100"),
                (100.01m, decimal.MaxValue, ">100")
            };

                row++;
                foreach (var rango in rangos)
                {
                    var cantidad = consumos.Count(c => (decimal)c >= rango.Item1 && (decimal)c <= rango.Item2);
                    var porcentaje = consumos.Count > 0 ? (cantidad * 100m / consumos.Count) : 0;

                    worksheet.Cell(row, 1).Value = rango.Item3;
                    worksheet.Cell(row, 2).Value = cantidad;
                    worksheet.Cell(row, 3).Value = porcentaje / 100; // Para formato porcentaje
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "0.00%";

                    row++;
                }
            }
            else
            {
                worksheet.Cell("A3").Value = "No hay datos de consumo disponibles";
            }

            worksheet.Columns().AdjustToContents();
        }
        private decimal CalcularMediana(List<decimal> valores)
        {
            if (!valores.Any()) return 0;

            var sorted = valores.OrderBy(v => v).ToList();
            int count = sorted.Count;

            if (count % 2 == 0)
            {
                return (sorted[count / 2 - 1] + sorted[count / 2]) / 2;
            }
            else
            {
                return sorted[count / 2];
            }
        }
        private decimal CalcularDesviacionEstandar(List<decimal> valores)
        {
            if (valores.Count < 2) return 0;

            var promedio = valores.Average();
            var sumaCuadrados = valores.Sum(v => (v - promedio) * (v - promedio));

            return (decimal)Math.Sqrt((double)(sumaCuadrados / (valores.Count - 1)));
        }
    }

    // InstallmentExportService.cs



public class InstallmentExportService
    {
        private readonly List<Installment> _installments;
        private readonly BudgetHeader _budget;
        private readonly List<ServiceReadingDetail> _waterReadings;
        private readonly List<Exoneration> _exonerations;
        private readonly Building _building;
        private readonly List<Category> _categories;

        public InstallmentExportService(
            List<Installment> installments,
            BudgetHeader budget,
            List<ServiceReadingDetail> waterReadings,
            List<Exoneration> exonerations,
            Building building,
            List<Category> categories)
        {
            _installments = installments;
            _budget = budget;
            _waterReadings = waterReadings;
            _exonerations = exonerations;
            _building = building;
            _categories = categories;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // Exportar a Excel
        public byte[] ExportToExcel()
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Cuotas");

            // Encabezados
            worksheet.Cell(1, 1).Value = "REPORTE DE CUOTAS";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Range(1, 1, 1, 10).Merge();

            worksheet.Cell(2, 1).Value = "DPTO";
            worksheet.Cell(2, 2).Value = "PROPIETARIO";
            worksheet.Cell(2, 3).Value = "ÁREA (m²)";
            worksheet.Cell(2, 4).Value = "% DISTRIBUCIÓN";
            worksheet.Cell(2, 5).Value = "CONSUMO AGUA";
            worksheet.Cell(2, 6).Value = "CUOTA ORDINARIA";
            worksheet.Cell(2, 7).Value = "CUOTA AGUA";
            worksheet.Cell(2, 8).Value = "TOTAL";
            worksheet.Cell(2, 9).Value = "FECHA VENCIMIENTO";
            worksheet.Cell(2, 10).Value = "ESTADO";

            var headerRange = worksheet.Range(2, 1, 2, 10);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 3;
            foreach (var installment in _installments)
            {
                var waterReading = _waterReadings?
                    .FirstOrDefault(w => w.IdGroupUnit == installment.IdGroupUnit);

                decimal waterAmount = waterReading?.CalculatedAmount ?? 0;
                decimal installmentAmount = CalculateInstallmentAmount(installment);
                decimal total = installmentAmount + waterAmount;

                worksheet.Cell(row, 1).Value = installment.UnitName;
                worksheet.Cell(row, 2).Value = installment.OwnerName;
                worksheet.Cell(row, 3).Value = installment.TotalArea;
                worksheet.Cell(row, 4).Value = installment.Percent;
                worksheet.Cell(row, 5).Value = waterReading?.Consumption ?? 0;
                worksheet.Cell(row, 6).Value = installmentAmount;
                worksheet.Cell(row, 7).Value = waterAmount;
                worksheet.Cell(row, 8).Value = total;
                worksheet.Cell(row, 9).Value = installment.DueDate.ToString("dd/MM/yyyy");
                worksheet.Cell(row, 10).Value = GetStatusText((int)installment.Status);

                // Formato numérico
                worksheet.Cell(row, 6).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "\"S/\" #,##0.00";
                worksheet.Cell(row, 8).Style.NumberFormat.Format = "\"S/\" #,##0.00";

                row++;
            }

            // Totales
            worksheet.Cell(row, 5).Value = "TOTALES:";
            worksheet.Cell(row, 5).Style.Font.Bold = true;
            worksheet.Cell(row, 6).FormulaA1 = $"SUM(F3:F{row - 1})";
            worksheet.Cell(row, 7).FormulaA1 = $"SUM(G3:G{row - 1})";
            worksheet.Cell(row, 8).FormulaA1 = $"SUM(H3:H{row - 1})";

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // Generar PDF individual (como el ejemplo)
        
        public byte[] GenerateSinglePdf(Installment installment)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);

                    page.Header().Element(Header);
                    page.Content().Element(content => Content(content, installment));
                    page.Footer().Element(Footer);
                });
            });

            return document.GeneratePdf();
        }

        private void Header(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("RESIDENCIAL NOVA ALZAMORA - SURQUILLO - LIMA")
                        .FontSize(14).Bold();

                    column.Item().Text("REPORTE DE DEUDAS")
                        .FontSize(12);
                });

                //row.ConstantItem(100).Image("logo.png");
            });
        }

        private void Content(IContainer container, Installment installment)
        {
            container.Column(column =>
            {
                // Información del departamento
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text($"DPTO: {installment.UnitName}")
                            .FontSize(16).Bold();
                        col.Item().Text($"Propietario: {installment.OwnerName}")
                            .FontSize(12);
                        col.Item().Text($"Período: {DateTime.Now:MMMM yyyy}")
                            .FontSize(12);
                    });

                    row.ConstantItem(200).Column(col =>
                    {
                        col.Item().Text("Área Total:").FontSize(10);
                        col.Item().Text($"{installment.TotalArea:N2} m²").Bold();

                        col.Item().Text("% Distribución:").FontSize(10);
                        col.Item().Text($"{installment.Percent:N2}%").Bold();
                    });
                });

                // Histórico de deudas (como en el PDF ejemplo)
                column.Item().PaddingBottom(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Año").Bold();
                        header.Cell().Text("Ene").Bold();
                        header.Cell().Text("Feb").Bold();
                        header.Cell().Text("Mar").Bold();
                        header.Cell().Text("Abr").Bold();
                    });

                    // Aquí irían los datos históricos
                    for (int i = 2023; i >= 2017; i--)
                    {
                        table.Cell().Text(i.ToString());
                        table.Cell().Text("-");
                        table.Cell().Text("-");
                        table.Cell().Text("-");
                        table.Cell().Text("-");
                    }
                });

                // Detalle de la cuota actual
                column.Item().PaddingBottom(20).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.ConstantColumn(100);
                        columns.ConstantColumn(100);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("DESCRIPCIÓN").Bold();
                        header.Cell().AlignRight().Text("PRESUPUESTO").Bold();
                        header.Cell().AlignRight().Text("CUOTA").Bold();
                    });

                    decimal total = 0;
                    foreach (var item in _budget.Details.Where(d => !d.IsHeader))
                    {
                        var amount = CalculateItemAmount(item, installment);
                        total += amount;

                        table.Cell().Text(item.Description);
                        table.Cell().AlignRight().Text($"S/ {item.MonthlyAmount:N2}");
                        table.Cell().AlignRight().Text($"S/ {amount:N2}");
                    }

                    // Agua
                    var waterReading = _waterReadings?
                        .FirstOrDefault(w => w.IdGroupUnit == installment.IdGroupUnit);

                    if (waterReading != null)
                    {
                        table.Cell().Text("CONSUMO DE AGUA").Bold();
                        table.Cell().AlignRight().Text("-");
                        table.Cell().AlignRight().Text($"S/ {waterReading.CalculatedAmount:N2}");
                        total += waterReading.CalculatedAmount;
                    }

                    // Total
                    table.Cell().ColumnSpan(2).Text("TOTAL").Bold();
                    table.Cell().AlignRight().Text($"S/ {total:N2}").Bold();
                });

                // Tabla de cálculo de agua (como en el ejemplo)
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(100);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Rango m³").Bold();
                        header.Cell().Text("Mín").Bold();
                        header.Cell().Text("Potable").Bold();
                        header.Cell().Text("Alcant.").Bold();
                        header.Cell().Text("Total").Bold();
                        header.Cell().Text("Dif.").Bold();
                        header.Cell().Text("Consumo").Bold();
                        header.Cell().Text("Monto").Bold();
                    });

                    // Aquí irían las tarifas de agua por rangos
                });
            });
        }

        private void Footer(IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.Span("Generado el: ").FontSize(8);
                text.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(8).Bold();
            });
        }
        

        // Generar todos los PDFs (uno por departamento)
        public Dictionary<string, byte[]> GenerateAllPdfs()
        {
            var pdfs = new Dictionary<string, byte[]>();

            foreach (var installment in _installments)
            {
                var pdfBytes = GenerateSinglePdf(installment);
                pdfs.Add($"Cuota_{installment.UnitName}_{DateTime.Now:yyyyMM}.pdf", pdfBytes);
            }

            return pdfs;
        }
        
        // Generar ZIP con todos los PDFs
        public byte[] GenerateZipWithAllPdfs()
        {
            var pdfs = GenerateAllPdfs();

            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream,
                System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var pdf in pdfs)
                {
                    var entry = archive.CreateEntry(pdf.Key);
                    using var entryStream = entry.Open();
                    entryStream.Write(pdf.Value, 0, pdf.Value.Length);
                }
            }

            return memoryStream.ToArray();
        }
        
        // Métodos auxiliares
        private decimal CalculateInstallmentAmount(Installment installment)
        {
            // Lógica para calcular el monto de la cuota
            return installment.Amount;
        }

        private decimal CalculateItemAmount(BudgetDetail item, Installment installment)
        {
            // Lógica para calcular el monto por item
            // Similar a lo que ya tienes en tu código
            return item.MonthlyAmount * (installment.Percent / 100);
        }

        private string GetStatusText(int status)
        {
            return status switch
            {
                1 => "Pendiente",
                2 => "Pagado",
                3 => "Vencido",
                _ => "Desconocido"
            };
        }




        private Installment _installment = new();

        public byte[] GenerateReceipt(Installment installment)
        {
            _installment = installment;
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Content().Element(ComposeReceipt);
                });
            });

            return document.GeneratePdf();
        }

        // Todo el recibo (cabecera, datos del propietario, tabla y pie) se arma en un
        // solo bloque con un borde exterior continuo, igual que el recibo de referencia
        // (una hoja enmarcada de punta a punta) — antes se usaba page.Header()/Footer()
        // por separado, lo que dejaba cajas independientes que no calzaban visualmente.
        private void ComposeReceipt(IContainer container)
        {
            container.Border(1).BorderColor(Colors.Black).Column(column =>
            {
                column.Item().Element(ComposeHeader);
                column.Item().Element(ComposeOwnerRow);
                column.Item().Padding(10).Element(ComposeTable);
                column.Item().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            var titulo = string.IsNullOrWhiteSpace(_building.Location)
                ? _building.Name.ToUpper()
                : $"{_building.Name} - {_building.Location}".ToUpper();

            var periodo = _installment.Period.ToString("MMM-yy", CultureInfo.InvariantCulture).ToUpper();

            container.BorderBottom(2).BorderColor(Colors.Blue.Darken2).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().AlignCenter().Text(titulo).FontSize(14).Bold();
                    col.Item().AlignCenter().Text("Recibo de Mantenimiento")
                        .FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                });

                row.ConstantItem(100).Background(Colors.Blue.Lighten5).Padding(6).Column(col =>
                {
                    col.Item().AlignCenter().Text("FECHA").FontSize(7).Bold().FontColor(Colors.Grey.Darken2);
                    col.Item().AlignCenter().Text(periodo).FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
                });
            });
        }

        private void ComposeOwnerRow(IContainer container)
        {
            container.BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
            {
                row.RelativeItem().Text($"NOMBRE: {_installment.OwnerName.ToUpper()}")
                    .FontSize(10).Bold();

                row.ConstantItem(90).Background(Colors.Blue.Lighten5).Padding(4).AlignCenter()
                    .Text($"DPTO {_installment.UnitName}").FontSize(10).Bold().FontColor(Colors.Blue.Darken2);

                row.ConstantItem(90).AlignRight().Text($"Part.: {_installment.Percent:N2}%")
                    .FontSize(10).Bold();
            });
        }

        private void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                // Definir columnas
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3); // DESCRIPCION
                    columns.ConstantColumn(80); // PRESUP
                    columns.ConstantColumn(80); // CUOTA
                    columns.ConstantColumn(80); // DISTRIB.
                });

                // Encabezado de la tabla
                table.Header(header =>
                {
                    header.Cell().ColumnSpan(4).PaddingBottom(5);

                    header.Cell().Text("DESCRIPCION").Bold();
                    header.Cell().AlignRight().Text("PRESUP").Bold();
                    header.Cell().AlignRight().Text("CUOTA").Bold();
                    header.Cell().AlignRight().Text("DISTRIB.").Bold();
                });

                decimal totalCuota = 0;
                var rowIndex = 0;

                // Secciones reales del presupuesto de este edificio (headers de
                // BudgetDetail), en vez de las 6 categorías con GUIDs hardcodeados de
                // un único edificio que traía este generador antes — mismo criterio de
                // agrupación que ya usa InstallmentDetailModal.GetSections().
                foreach (var section in GetSections())
                {
                    var sectionItems = _budget.Details
                        .Where(x => x.IdSection == section.Id && !x.IsHeader)
                        .ToList();

                    if (!sectionItems.Any())
                    {
                        continue;
                    }

                    AddSectionHeader(table, section.Name);

                    // Categoría raíz de esta sección: si tiene ShowDetailInReceipt=false
                    // (configurable en /category, solo para categorías raíz), la sección
                    // se imprime colapsada — solo nombre + subtotal, sin desglosar cada
                    // ítem. El "Ver Detalle" en pantalla (InstallmentDetailModal) no lee
                    // este flag, siempre muestra el desglose completo.
                    var showDetail = _categories
                        .FirstOrDefault(c => c.IdCategory == section.IdCategory)?.ShowDetailInReceipt ?? true;

                    // Subtotal por sección (PRESUP y CUOTA), igual que el recibo de
                    // referencia — cada bloque cierra con su propia línea de totales.
                    decimal sectionPresup = 0;
                    decimal sectionCuota = 0;

                    foreach (var item in sectionItems)
                    {
                        var amount = CalculateItemAmount(item);
                        totalCuota += amount;
                        sectionPresup += item.MonthlyAmount;
                        sectionCuota += amount;

                        if (showDetail)
                        {
                            AddTableRow(table, item.Description,
                                item.MonthlyAmount, amount, item.Type, rowIndex++ % 2 == 1);
                        }

                        // Recuadro de consumo (Lectura Anterior/Actual/m³) del ítem de agua
                        // por departamento, igual que en el recibo de referencia.
                        if (item.IdCategory == _building.Configuration.WaterReadingDefault)
                        {
                            var waterReading = _waterReadings?
                                .FirstOrDefault(w => w.IdGroupUnit == _installment.IdGroupUnit);

                            if (waterReading != null)
                            {
                                totalCuota += waterReading.CalculatedAmount;
                                sectionCuota += waterReading.CalculatedAmount;

                                if (showDetail)
                                {
                                    AddWaterReadingBox(table, waterReading);
                                }
                            }
                        }
                    }

                    AddSectionSubtotal(table, sectionPresup, sectionCuota);
                }

                // TOTAL CUOTA, en una barra de color como en la referencia.
                var periodo = _installment.Period.ToString("MMM-yy", CultureInfo.InvariantCulture).ToUpper();
                table.Cell().ColumnSpan(4).PaddingTop(10);
                table.Cell().ColumnSpan(3).Background(Colors.Blue.Darken2).Padding(6)
                    .Text($"TOTAL CUOTA {periodo}").Bold().FontColor(Colors.White);
                table.Cell().Background(Colors.Blue.Darken2).Padding(6).AlignRight()
                    .Text($"S/ {totalCuota:N2}").Bold().FontSize(12).FontColor(Colors.White);

                // "DEUDAS ANTERIORES" (cuotas ordinarias/extraordinarias de periodos
                // previos, histórico de agua) queda pendiente: hoy no existe ninguna
                // consulta que traiga cuotas de un mismo IdGroupUnit a través de varios
                // periodos — se retoma en una fase aparte en vez de mostrar "S/ 0.00" fijo.
            });
        }

        private void ComposeFooter(IContainer container)
        {
            var footerText = ResolveFooterText();

            container.Padding(10).Column(column =>
            {
                if (!string.IsNullOrWhiteSpace(footerText))
                {
                    column.Item().Background(Colors.Grey.Lighten5)
                        .Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8)
                        .Text(footerText).FontSize(8).Italic();
                    column.Item().Height(6);
                }

                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("Generado el: ").FontSize(8);
                    text.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}").Bold().FontSize(8);
                });
            });
        }

        // Resuelve el texto configurable de Configuración del Edificio (Fase 2), reemplazando
        // los placeholders por los datos de esta cuota/edificio.
        private string ResolveFooterText()
        {
            var template = _building.Configuration.ReceiptFooterText;
            if (string.IsNullOrWhiteSpace(template))
            {
                return "";
            }

            var cuenta = _building.Configuration.BankAccounts.FirstOrDefault();

            return template
                .Replace("{DPTO}", _installment.UnitName)
                .Replace("{Propietario}", _installment.OwnerName)
                .Replace("{NroCta}", cuenta?.AccountNumber ?? "")
                .Replace("{Banco}", cuenta?.BankName ?? "")
                .Replace("{Titular}", cuenta?.AccountName ?? "")
                .Replace("{CCI}", cuenta?.CCI ?? "")
                .Replace("{Administrador}", _building.Configuration.AdminContact.Name)
                .Replace("{CorreoADM}", _building.Configuration.AdminContact.Email);
        }

        private void AddSectionHeader(TableDescriptor table, string title)
        {
            table.Cell().ColumnSpan(4).PaddingTop(8).Background(Colors.Blue.Lighten5)
                .BorderLeft(3).BorderColor(Colors.Blue.Darken2).Padding(4)
                .Text(title).Bold().FontSize(10).FontColor(Colors.Blue.Darken2);
        }

        // Línea de cierre de cada sección con sus totales de PRESUP y CUOTA, como en el
        // recibo de referencia (cada bloque termina con una raya y su subtotal).
        private void AddSectionSubtotal(TableDescriptor table, decimal presupTotal, decimal cuotaTotal)
        {
            table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).Text("");
            table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).AlignRight()
                .Text($"S/ {presupTotal:N2}").Bold();
            table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).AlignRight()
                .Text($"S/ {cuotaTotal:N2}").Bold();
            table.Cell().BorderTop(1).BorderColor(Colors.Grey.Darken1).Text("");
        }

        // Recuadro de consumo de agua por departamento (Anterior/Actual/Consumo/Monto),
        // como una mini-tabla resaltada dentro de la fila — igual que el recibo de
        // referencia, en vez del texto plano que traía antes.
        private void AddWaterReadingBox(TableDescriptor table, ServiceReadingDetail waterReading)
        {
            table.Cell().ColumnSpan(4).PaddingVertical(3)
                .Background(Colors.Cyan.Lighten5).BorderLeft(3).BorderColor(Colors.Cyan.Darken1)
                .Padding(6)
                .Table(waterTable =>
                {
                    waterTable.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(80);
                    });

                    waterTable.Cell().Text("Lectura de Agua por Dpto")
                        .Bold().FontSize(9).FontColor(Colors.Cyan.Darken2);
                    waterTable.Cell().AlignCenter().Text("Anterior").Bold().FontSize(7).FontColor(Colors.Grey.Darken2);
                    waterTable.Cell().AlignCenter().Text("Actual").Bold().FontSize(7).FontColor(Colors.Grey.Darken2);
                    waterTable.Cell().AlignCenter().Text("Consumo").Bold().FontSize(7).FontColor(Colors.Grey.Darken2);
                    waterTable.Cell().AlignRight().Text("Monto").Bold().FontSize(7).FontColor(Colors.Grey.Darken2);

                    waterTable.Cell().Text("");
                    waterTable.Cell().AlignCenter().Text(waterReading.PreviousReading.ToString("N2"));
                    waterTable.Cell().AlignCenter().Text(waterReading.CurrentReading.ToString("N2"));
                    waterTable.Cell().AlignCenter().Text($"{waterReading.Consumption:N2} m³");
                    waterTable.Cell().AlignRight().Text($"S/ {waterReading.CalculatedAmount:N2}")
                        .Bold().FontColor(Colors.Cyan.Darken2);
                });
        }

        private void AddTableRow(TableDescriptor table, string description,
                                decimal presupuesto, decimal cuota, int tipo, bool shaded = false)
        {
            var background = shaded ? Colors.Grey.Lighten5 : Colors.White;

            table.Cell().Background(background).PaddingVertical(3).Text(description);
            table.Cell().Background(background).PaddingVertical(3).AlignRight().Text(presupuesto > 0 ? $"S/ {presupuesto:N2}" : "-");
            table.Cell().Background(background).PaddingVertical(3).AlignRight().Text(cuota > 0 ? $"S/ {cuota:N2}" : "-");
            table.Cell().Background(background).PaddingVertical(3).AlignRight().Text(GetDistributionType(tipo));
        }

        // Misma fórmula que InstallmentDetailModal.CalculateQuote, para que el PDF cuadre
        // exactamente con lo que ya se ve en pantalla en el detalle de cuota.
        private decimal CalculateItemAmount(BudgetDetail item)
        {
            if (item.IdCategory == _building.Configuration.WaterReadingDefault && _waterReadings.Any())
            {
                var totalWaterConsumption = _waterReadings.Sum(w => w.CalculatedAmount);
                var inverseNroGroupUnit = GetTotalUnits() > 0 ? 1m / GetTotalUnits() : 0;
                return Math.Round(Math.Abs(item.MonthlyAmount - totalWaterConsumption) * inverseNroGroupUnit, 2);
            }

            var idGroupUnitExonerado = _exonerations
                .Where(c => c.IdCategory == item.IdCategory)
                .Select(c => c.IdGroupUnit)
                .FirstOrDefault();

            if (_installment.IdGroupUnit == idGroupUnitExonerado)
            {
                return 0;
            }

            var nroExcepciones = _exonerations.Count(c => c.IdCategory == item.IdCategory);
            var total = item.Type == 1
                ? item.MonthlyAmount / (GetTotalUnits() - nroExcepciones)
                : item.MonthlyAmount * (_installment.Percent / 100);

            return Math.Round(total, 2);
        }

        private int GetTotalUnits() => _building.Apartments;

        private string GetDistributionType(int tipo)
        {
            return tipo switch
            {
                1 => "Por Unidad",
                2 => "Por Área",
                3 => "Por Consumo",
                _ => "Fijo"
            };
        }

        // Secciones reales del presupuesto (headers de BudgetDetail), mismo criterio que
        // InstallmentDetailModal.GetSections() — reemplaza el mapeo de 6 categorías con
        // GUIDs hardcodeados de un único edificio que traía este generador antes.
        private List<SectionInfo> GetSections()
        {
            if (_budget?.Details == null)
            {
                return new();
            }

            return _budget.Details
                .Where(x => x.IsHeader)
                .Select(x => new SectionInfo { Id = x.IdSection, Name = x.Description, IdCategory = x.IdCategory })
                .OrderBy(x => x.Id)
                .ToList();
        }






    }
}
