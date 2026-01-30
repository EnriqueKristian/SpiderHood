using ClosedXML.Excel;
using OfficeOpenXml;
using SpiderHood.Components.Pages.Components;

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
}
