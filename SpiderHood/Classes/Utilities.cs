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
        // Antes era un `Action` invocado fire-and-forget (`Task task = ExecuteWithLoading(...)`
        // sin await) desde OnConfirmationResult -- cualquier excepción dentro de la acción
        // confirmada (ej. Publicar un presupuesto sin lectura de agua) quedaba en un Task
        // que nadie observaba: no llegaba al catch del propio `proceed` del llamador, no
        // mostraba ningún toast de error, y la UI se quedaba con el spinner de carga
        // trabado para siempre (indistinguible de un hang real). Ahora es un `Func<Task>`
        // que OnConfirmationResult awaitea, así la excepción sí propaga hasta el catch del
        // llamador (o, en su defecto, hasta el manejo de errores del propio circuito).
        private Func<Task>? _pendingAction;
        private string _pendingActionName = "";
        public bool _isLoading = false;
        public string _currentOperation = "";

        public async Task ExecuteWithConfirmation(Func<Task> action, string actionName, ConfirmationModal? _confirmationModal, string message = "", string type = "warning", bool isCancelOnly = false)
        {
            _pendingAction = () => ExecuteWithLoading(async () => { await action.Invoke(); }, actionName);

            _pendingActionName = actionName;

            var defaultMessage = type == "danger"
                ? $"¿Está seguro de {actionName.ToLower()}? Esta acción no se puede deshacer."
                : $"¿Desea {actionName.ToLower()}?";

            // Orden importa: Show() dispara su propio StateHasChanged() adentro del
            // componente -- si corre ANTES de fijar Message/IsCancelOnly, ese primer
            // render puede salir con el default ("¿Está seguro de realizar esta
            // acción?") o el texto de una invocación anterior del mismo modal
            // compartido, en vez del mensaje real (acá, el detalle de qué le falta al
            // presupuesto -- ej. la lectura de agua). Mismo bug ya corregido en
            // ReconciliationWorkspace.ConfirmarAsync (Docs/Pendientes-Negocio-
            // Conciliacion.md #4); esta es la copia compartida que usan
            // BudgetGenerator.razor, ServiceReadingModal.razor, ModalOwnerUnit.razor y
            // ManualInstallmentConciliation.razor -- se corrige acá una sola vez para
            // los 4 a la vez.
            _confirmationModal?.Message = string.IsNullOrEmpty(message) ? defaultMessage : message;
            _confirmationModal?.IsCancelOnly = isCancelOnly;
            _confirmationModal?.Show(type);
        }

        public async Task OnConfirmationResult(bool confirmed)
        {
            if (confirmed && _pendingAction != null)
            {
                await _pendingAction.Invoke();
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

        // La plantilla tiene que calzar exactamente con lo que WaterCalculationService.
        // ImportarDesdeExcelAsync espera al leerla de vuelta: hoja 1, fila 1 = encabezado
        // (se descarta con RowsUsed().Skip(1), sin importar su contenido), y desde la
        // fila 2 en adelante, EXACTAMENTE las columnas del layout correspondiente — ver
        // WaterReadingValidator.ValidateLoadExcelReading. Dos layouts posibles:
        //   - Primera Carga (esPrimeraCarga=true, sin ninguna lectura anterior guardada
        //     para el edificio): Dpto./Periodo/Lectura Inicial/Lectura Final/Fecha
        //     Lectura (5 columnas) — la Inicial es necesaria para no cobrarle a cada
        //     unidad el consumo acumulado de toda la vida del medidor en su primer recibo.
        //   - Cargas siguientes (ya con historial): Dpto./Periodo/Lectura Final/Fecha
        //     Lectura (4 columnas) — el sistema toma la Lectura Final ya guardada de la
        //     carga anterior como "anterior" de esta.
        public async Task<(string Filename, MemoryStream Stream)> ExportarPlantillaVacia(ServiceReadingState state, List<UnitView> unidades, bool esPrimeraCarga = false)
        {
            try
            {
                using var memoryStream = new MemoryStream();

                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("Plantilla");

                    // Encabezado en la fila 1 — es la única fila que el importador se salta,
                    // así que tiene que ser justo esta, sin nada de "Periodo" suelto arriba.
                    var headers = esPrimeraCarga
                        ? new[] { "Dpto.", "Periodo", "Lectura Inicial", "Lectura Final", "Fecha Lectura" }
                        : new[] { "Dpto.", "Periodo", "Lectura Final", "Fecha Lectura" };

                    for (int i = 0; i < headers.Length; i++)
                    {
                        worksheet.Cell(1, i + 1).Value = headers[i];
                        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
                        worksheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
                    }

                    // Texto plano en todas las columnas (incluidas las de fecha): así lo que
                    // el usuario escriba se guarda tal cual, sin que Excel lo reformatee a su
                    // propio formato regional al vuelo — mismo criterio que ya usaba la
                    // columna 1 para no perder ceros a la izquierda del Dpto.
                    worksheet.Columns(1, headers.Length).Style.NumberFormat.Format = "@";

                    var periodoTexto = state.CurrentReading.Period.ToString("yyyy-MM-dd");
                    var fechaLecturaSugerida = DateTime.Today.ToString("yyyy-MM-dd");

                    int row = 2;
                    foreach (var unidad in unidades)
                    {
                        worksheet.Cell(row, 1).Value = unidad.Number.ToString();
                        // Precargado — es el mismo periodo para todas las unidades y es
                        // obligatorio para que la fila pase la validación al importar.
                        worksheet.Cell(row, 2).Value = periodoTexto;

                        if (esPrimeraCarga)
                        {
                            worksheet.Cell(row, 3).Value = ""; // Lectura inicial — a completar
                            worksheet.Cell(row, 4).Value = ""; // Lectura final — a completar
                            worksheet.Cell(row, 5).Value = fechaLecturaSugerida;
                        }
                        else
                        {
                            worksheet.Cell(row, 3).Value = ""; // Lectura final — a completar
                            worksheet.Cell(row, 4).Value = fechaLecturaSugerida;
                        }
                        row++;
                    }

                    worksheet.Columns().AdjustToContents();

                    // Las instrucciones van en una hoja aparte — WaterCalculationService.
                    // ImportarDesdeExcelAsync solo lee workbook.Worksheet(1) (la primera),
                    // así que esto no interfiere para nada con la importación.
                    var hojaInstrucciones = workbook.Worksheets.Add("Instrucciones");
                    hojaInstrucciones.Cell(1, 1).Value = "INSTRUCCIONES";
                    hojaInstrucciones.Cell(1, 1).Style.Font.Bold = true;

                    var instrucciones = esPrimeraCarga
                        ? new[]
                        {
                            "1. No modifique la columna 'Dpto.' ni el orden de las columnas.",
                            "2. Esta es la Primera Carga del edificio: complete 'Lectura Inicial' con la lectura del medidor al momento de empezar a operar el sistema, y 'Lectura Final' con la lectura de este periodo. El consumo se calcula como Final - Inicial.",
                            "3. 'Periodo' y 'Fecha Lectura' ya vienen precargados — solo ajuste 'Fecha Lectura' si tomó la lectura otro día.",
                            "4. Las fechas deben tener formato AAAA-MM-DD (ej: 2026-03-15) y no pueden ser una fecha futura.",
                            "5. Guarde el archivo y cárguelo en el sistema con 'Importar Lecturas de Agua'.",
                        }
                        : new[]
                        {
                            "1. No modifique la columna 'Dpto.' ni el orden de las columnas.",
                            "2. Complete 'Lectura Final' con la lectura del medidor de cada unidad — el sistema calcula el consumo contra la última lectura guardada.",
                            "3. 'Periodo' y 'Fecha Lectura' ya vienen precargados — solo ajuste 'Fecha Lectura' si tomó la lectura otro día.",
                            "4. Las fechas deben tener formato AAAA-MM-DD (ej: 2026-03-15) y no pueden ser una fecha futura.",
                            "5. Guarde el archivo y cárguelo en el sistema con 'Importar Lecturas de Agua'.",
                        };

                    for (int i = 0; i < instrucciones.Length; i++)
                        hojaInstrucciones.Cell(i + 2, 1).Value = instrucciones[i];

                    hojaInstrucciones.Columns().AdjustToContents();

                    workbook.SaveAs(memoryStream);
                }

                var fileName = $"Plantilla_Lecturas_{DateTime.Now:yyyyMMdd}.xlsx";

                return (fileName, memoryStream);
            }
            catch (Exception ex)
            {
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

    // Cálculo de "saldo a favor no aplicado": dinero de un pago (transacción bancaria) ya
    // conciliado a esta unidad que quedó sin usar en ninguna cuota -- distinto de un
    // Installment.Debt negativo (que sí se refleja en DEUDA TOTAL, ver "Deudas Anteriores"
    // en InstallmentExportService/InstallmentDetailModal). Este vive puramente en la
    // transacción bancaria, sin ningún Installment/InstallmentPaid que lo referencie -- por
    // eso ni el detalle ni el recibo lo mostraban, aunque MyReceipts.razor ya lo calculaba
    // para el aviso de la pantalla principal (Docs/... hallazgo de 2026-09-24). Un solo
    // cálculo para admin (InstallmentList.razor) y residente (MyReceipts.razor,
    // MyPayments.razor) en vez de triplicarlo.
    public static class SaldoAFavorCalculator
    {
        public static decimal CalcularNoAplicado(
            Guid idGroupUnit,
            List<Installment> todasLasCuotas,
            List<InstallmentPaid> pagosCuotas,
            List<AccountStatementDetailView> transacciones)
        {
            var idsInstallment = todasLasCuotas.Where(c => c.IdGroupUnit == idGroupUnit).Select(c => c.IdInstallment).ToHashSet();
            if (!idsInstallment.Any()) return 0;

            var idsTransaccion = pagosCuotas
                .Where(p => idsInstallment.Contains(p.IdInstallment))
                .Select(p => p.IdTransaction)
                .Distinct()
                .ToHashSet();
            if (!idsTransaccion.Any()) return 0;

            return transacciones
                    .Where(t => idsTransaccion.Contains(t.IdStatementDetail))
                    .Sum(t =>
                    {
                        // Solo sumar los pagos de ESTA unidad (idsInstallment), no de todas
                        decimal pagadoEnEstaUnidad = pagosCuotas
                            .Where(p => p.IdTransaction == t.IdStatementDetail && idsInstallment.Contains(p.IdInstallment))
                            .Sum(p => p.Amount);

                        var diferencia = t.Amount - pagadoEnEstaUnidad;
                        // Solo contar como saldo a favor si es positivo
                        return diferencia > 0 ? diferencia : 0;
                    });
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
        private readonly List<Installment> _cargosAdicionales;
        private readonly List<Installment> _deudasAnteriores;
        private readonly int _totalApartments;
        private readonly Dictionary<Guid, int> _unitCountByGroup;
        // Logo del encabezado (Docs/Pendientes-Negocio-Consolidado.md #30, puntos
        // d/e) -- el del Edificio si tiene uno propio, si no el de la Account
        // (empresa administradora) como respaldo; la elección la hace
        // BuildingService.GetReceiptBrandingAsync, ACA sólo se dibuja lo que
        // llegue. Ambos opcionales a propósito: sin ninguno de los dos, el
        // recibo se genera igual, simplemente sin logo ni franja de
        // administradora en la cabecera.
        private readonly byte[]? _logoBytes;
        private readonly string? _administradoraName;
        // Dinero de un pago ya conciliado a esta unidad que quedó sin aplicar a ninguna
        // cuota -- vive puramente en la transacción bancaria (ver
        // SaldoAFavorCalculator), sin ningún Installment/InstallmentPaid que lo referencie.
        // Distinto de un Installment.Debt negativo en _deudasAnteriores (que sí se resta de
        // DEUDA TOTAL): este monto NO está incluido en ningún total del recibo, es solo
        // informativo -- ver el aviso aparte en ComposeTable.
        private readonly decimal _saldoAFavorNoAplicado;

        public InstallmentExportService(
            List<Installment> installments,
            BudgetHeader budget,
            List<ServiceReadingDetail> waterReadings,
            List<Exoneration> exonerations,
            Building building,
            List<Category> categories,
            List<OwnerUnitView> owners,
            List<Installment>? cargosAdicionales = null,
            List<Installment>? deudasAnteriores = null,
            byte[]? logoBytes = null,
            string? administradoraName = null,
            decimal saldoAFavorNoAplicado = 0)
        {
            _installments = installments;
            _budget = budget;
            _waterReadings = waterReadings;
            _exonerations = exonerations;
            _building = building;
            _categories = categories;
            _cargosAdicionales = cargosAdicionales ?? new();
            _deudasAnteriores = deudasAnteriores ?? new();
            _logoBytes = logoBytes;
            _administradoraName = administradoraName;
            _saldoAFavorNoAplicado = saldoAFavorNoAplicado;

            var unidadesFacturables = owners
                .Where(o => o.Role == 1 && (o.TypeUnit == 1 || o.TypeUnit == 4))
                .ToList();
            _totalApartments = unidadesFacturables.Select(o => o.IdUnit).Distinct().Count();
            _unitCountByGroup = unidadesFacturables
                .GroupBy(o => o.IdGroupUnit)
                .ToDictionary(g => g.Key, g => g.Select(o => o.IdUnit).Distinct().Count());

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

        // Métodos auxiliares
        private decimal CalculateInstallmentAmount(Installment installment)
        {
            // Lógica para calcular el monto de la cuota
            return installment.Amount;
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
                    page.Margin(15); // Reducido de 20 a 15 para más espacio útil
                    page.DefaultTextStyle(x => x.FontSize(8)); // Reducido de 9 a 8 para compactar

                    page.Content().Element(ComposeReceipt);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateAllReceiptsZip()
        {
            using var memoryStream = new MemoryStream();
            using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
            {
                foreach (var installment in _installments)
                {
                    var pdfBytes = GenerateReceipt(installment);
                    var entry = archive.CreateEntry($"Recibo_{installment.UnitName}_{installment.Period:yyyyMM}.pdf");
                    using var entryStream = entry.Open();
                    entryStream.Write(pdfBytes, 0, pdfBytes.Length);
                }
            }
            return memoryStream.ToArray();
        }

        private void ComposeReceipt(IContainer container)
        {
            // Borde exterior más sutil (0.5f en lugar de 1)
            container.Border(0.5f).BorderColor(Colors.Grey.Lighten2).Column(column =>
            {
                column.Item().Element(ComposeHeader);
                column.Item().Element(ComposeOwnerRow);
                column.Item().Padding(8).Element(ComposeTable); // Padding reducido de 10 a 8
                column.Item().Element(ComposeFooter);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            var periodo = _installment.Period.ToString("MMM-yy", CultureInfo.InvariantCulture).ToUpper();
            var mesAno = _installment.Period.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            mesAno = char.ToUpper(mesAno[0]) + mesAno.Substring(1);

            container.Column(headerColumn =>
            {
                // Fondo gris claro unificado para toda la cabecera
                headerColumn.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(row =>
                {
                    if (_logoBytes != null)
                    {
                        row.ConstantItem(50).AlignMiddle().MaxHeight(40).Image(_logoBytes).FitArea();
                    }

                    row.RelativeItem().Column(col =>
                    {
                        col.Item().AlignCenter().Text(_building.Name.ToUpper()).FontSize(13).Bold().FontColor(Colors.Black);

                        if (!string.IsNullOrWhiteSpace(_building.Location))
                        {
                            col.Item().AlignCenter().Text(_building.Location)
                                .FontSize(8).FontColor(Colors.Grey.Darken1);
                        }

                        col.Item().AlignCenter().Text($"Recibo de Mantenimiento - {mesAno}")
                            .FontSize(10).Bold().FontColor(Colors.Blue.Darken2);
                    });

                    // Caja de fecha con fondo azul oscuro y texto blanco (más profesional)
                    row.ConstantItem(90).Background(Colors.Blue.Darken2).Padding(6).Column(col =>
                    {
                        col.Item().AlignCenter().Text("FECHA").FontSize(7).Bold().FontColor(Colors.White);
                        col.Item().AlignCenter().Text(periodo).FontSize(11).Bold().FontColor(Colors.White);
                    });
                });

                // Franja "Administrado por..." (Docs/Pendientes-Negocio-Consolidado.md
                // #30, Opción A del mockup acordado con el usuario) -- independiente de
                // cuál logo ganó arriba (Edificio o Account de respaldo): mientras haya
                // una Account con RazonSocial, se identifica quién administra. Si el
                // Building no tiene Account (o la Account no tiene RazonSocial), no hay
                // nada que decir acá y la franja no se dibuja -- mismo criterio
                // fail-open que el resto del recibo.
                if (!string.IsNullOrWhiteSpace(_administradoraName))
                {
                    headerColumn.Item().Background(Colors.Grey.Lighten5)
                        .BorderTop(0.5f).BorderColor(Colors.Grey.Lighten2)
                        .Padding(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Administrado por {_administradoraName}")
                            .FontSize(7).FontColor(Colors.Grey.Darken1);

                        // Propaganda sutil, pedida por el usuario -- no invasiva (gris,
                        // chica, itálica) pero presente en el mismo lugar donde ya se
                        // identifica a la administradora.
                        row.ConstantItem(70).AlignRight().Text("SpiderHoodApp")
                            .FontSize(6).Italic().FontColor(Colors.Grey.Medium);
                    });
                }
            });
        }

        private void ComposeOwnerRow(IContainer container)
        {
            container.Background(Colors.Grey.Lighten3).Padding(6).Row(row =>
            {
                row.RelativeItem(2).Text($"NOMBRE: {_installment.OwnerName.ToUpper()}")
                    .FontSize(9).Bold();

                row.RelativeItem().AlignCenter().Text($"DPTO {_installment.UnitName}")
                    .FontSize(9).Bold();

                row.RelativeItem().AlignRight().Text($"Part.: {_installment.Percent:N2}%")
                    .FontSize(9).Bold();
            });
        }

        private void ComposeTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.ConstantColumn(75); // Reducido de 80
                    columns.ConstantColumn(75); // Reducido de 80
                    columns.ConstantColumn(70); // Reducido de 80
                });

                table.Header(header =>
                {
                    header.Cell().ColumnSpan(4).PaddingBottom(4);
                    header.Cell().Text("DESCRIPCION").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("PRESUP").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("CUOTA").Bold().FontSize(8);
                    header.Cell().AlignRight().Text("DISTRIB.").Bold().FontSize(8);
                });

                decimal totalCuota = 0;

                if (_installment.Type != InstallmentType.Ordinaria)
                {
                    AddSectionHeader(table, TipoDescripcion(_installment.Type));
                    AddTableRow(table, string.IsNullOrWhiteSpace(_installment.Concept) ? TipoDescripcion(_installment.Type) : _installment.Concept, 0, _installment.Amount, 0);
                    totalCuota = _installment.Amount;

                    var periodoExtra = _installment.Period.ToString("MMM-yy", CultureInfo.InvariantCulture).ToUpper();
                    table.Cell().ColumnSpan(4).PaddingTop(8);
                    table.Cell().ColumnSpan(3).Background(Colors.Blue.Darken2).Padding(5)
                        .Text($"TOTAL CUOTA {periodoExtra}").Bold().FontColor(Colors.White).FontSize(9);
                    table.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                        .Text($"S/ {totalCuota:N2}").Bold().FontSize(11).FontColor(Colors.White);
                    return;
                }

                foreach (var section in GetSections())
                {
                    var sectionItems = _budget.Details.Where(x => x.IdSection == section.Id && !x.IsHeader).ToList();
                    if (!sectionItems.Any()) continue;

                    AddSectionHeader(table, section.Name);

                    var showDetail = _categories.FirstOrDefault(c => c.IdCategory == section.IdCategory)?.ShowDetailInReceipt ?? true;
                    decimal sectionPresup = 0;
                    decimal sectionCuota = 0;

                    foreach (var item in sectionItems)
                    {
                        var amount = CalculateItemAmount(item);
                        sectionPresup += item.MonthlyAmount;
                        sectionCuota += amount;

                        if (showDetail)
                        {
                            AddTableRow(table, item.Description, item.MonthlyAmount, amount, item.Type);
                        }

                        if (item.IdCategory == _building.Configuration.WaterReadingDefault)
                        {
                            var waterReading = _waterReadings?.FirstOrDefault(w => w.IdGroupUnit == _installment.IdGroupUnit);
                            if (waterReading != null && showDetail)
                            {
                                sectionCuota += waterReading.CalculatedAmount;
                                // CAMBIO CLAVE: Usar versión inline en lugar de caja separada
                                AddWaterReadingInline(table, waterReading);
                            }
                        }
                    }

                    AddSectionSubtotal(table, sectionPresup, sectionCuota);
                }

                var periodo = _installment.Period.ToString("MMM-yy", CultureInfo.InvariantCulture).ToUpper();
                table.Cell().ColumnSpan(4).PaddingTop(8);
                table.Cell().ColumnSpan(3).Background(Colors.Blue.Darken2).Padding(5)
                    .Text($"TOTAL CUOTA ORDINARIA {periodo}").Bold().FontColor(Colors.White).FontSize(9);
                table.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight()
                    .Text($"S/ {_installment.Amount:N2}").Bold().FontSize(11).FontColor(Colors.White);

                // Aviso aparte del de "Deudas Anteriores" de abajo -- ESTE monto no está
                // incluido en ningún total de este recibo (a diferencia del Debt negativo de
                // deudaAnteriorTotal, que sí se resta de DEUDA TOTAL). Puramente informativo.
                if (_saldoAFavorNoAplicado > 0.005m)
                {
                    table.Cell().ColumnSpan(4).PaddingTop(8);
                    table.Cell().ColumnSpan(4).Background(Colors.Green.Lighten4).Padding(6)
                        .Text($"Además, tiene un saldo a favor de S/ {_saldoAFavorNoAplicado:N2} de un pago anterior que aún no se aplicó a ninguna cuota (no incluido en la Deuda Total de este recibo).")
                        .Bold().FontSize(8).FontColor(Colors.Green.Darken3);
                }

                var cargosUnidad = _cargosAdicionales.Where(c => c.IdGroupUnit == _installment.IdGroupUnit).ToList();
                if (cargosUnidad.Any())
                {
                    AddSectionHeader(table, "CUOTAS EXTRAORDINARIAS, MULTAS Y MORA");
                    var totalAdicionales = 0m;
                    foreach (var cargo in cargosUnidad)
                    {
                        totalAdicionales += cargo.Amount;
                        AddTableRow(table, string.IsNullOrWhiteSpace(cargo.Concept) ? TipoDescripcion(cargo.Type) : cargo.Concept, 0, cargo.Amount, 0);
                    }

                    table.Cell().ColumnSpan(4).PaddingTop(6);
                    table.Cell().ColumnSpan(3).Background(Colors.Grey.Darken2).Padding(5)
                        .Text("TOTAL GENERAL").Bold().FontColor(Colors.White).FontSize(9);
                    table.Cell().Background(Colors.Grey.Darken2).Padding(5).AlignRight()
                        .Text($"S/ {(_installment.Amount + totalAdicionales):N2}").Bold().FontSize(11).FontColor(Colors.White);
                }

                var deudasUnidad = _deudasAnteriores.Where(d => d.IdGroupUnit == _installment.IdGroupUnit && d.IdInstallment != _installment.IdInstallment && d.Period < _installment.Period).ToList();
                if (deudasUnidad.Any())
                {
                    // Pedido explícito del usuario -- una regularización de agua es una Cuota
                    // Extraordinaria más, no una categoría aparte (ver mismo cambio en
                    // InstallmentDetailModal.razor).
                    var deudaOrdinarias = deudasUnidad.Where(d => d.Type == InstallmentType.Ordinaria).Sum(d => d.Debt);
                    var deudaExtraordinarias = deudasUnidad.Where(d => d.Type != InstallmentType.Ordinaria).Sum(d => d.Debt);
                    var deudaAnteriorTotal = deudaOrdinarias + deudaExtraordinarias;

                    // Pedido explícito del usuario -- Debt negativo (saldo a favor) ya restaba
                    // correctamente de DEUDA TOTAL, pero quedaba enterrado en una fila más de la
                    // tabla, sin aviso aparte, y eso confundió al inicio. Mismo texto que el modal.
                    if (deudaAnteriorTotal < 0)
                    {
                        table.Cell().ColumnSpan(4).PaddingTop(8);
                        table.Cell().ColumnSpan(4).Background(Colors.Green.Lighten4).Padding(6)
                            .Text($"Esta unidad tiene un saldo a favor de S/ {Math.Abs(deudaAnteriorTotal):N2}, ya descontado de la Deuda Total.")
                            .Bold().FontSize(8).FontColor(Colors.Green.Darken3);
                    }

                    AddSectionHeader(table, "DEUDAS ANTERIORES");
                    AddDeudaAnteriorRow(table, "Cuotas Ordinarias", deudaOrdinarias);
                    AddDeudaAnteriorRow(table, "Cuotas Extraordinarias, Multas y Mora", deudaExtraordinarias);

                    table.Cell().ColumnSpan(4).PaddingTop(6);
                    table.Cell().ColumnSpan(3).Background(Colors.Red.Darken2).Padding(5)
                        .Text("TOTAL DEUDAS ANTERIORES").Bold().FontColor(Colors.White).FontSize(9);
                    table.Cell().Background(Colors.Red.Darken2).Padding(5).AlignRight()
                        .Text($"S/ {deudaAnteriorTotal:N2}").Bold().FontSize(11).FontColor(Colors.White);

                    var granTotal = _installment.Amount + cargosUnidad.Sum(c => c.Amount) + deudaAnteriorTotal;
                    table.Cell().ColumnSpan(4).PaddingTop(6);
                    table.Cell().ColumnSpan(3).Background(Colors.Black).Padding(5)
                        .Text("DEUDA TOTAL").Bold().FontColor(Colors.White).FontSize(9);
                    table.Cell().Background(Colors.Black).Padding(5).AlignRight()
                        .Text($"S/ {granTotal:N2}").Bold().FontSize(12).FontColor(Colors.White);
                }
            });
        }

        private void ComposeFooter(IContainer container)
        {
            var footerText = ResolveFooterText();

            container.Padding(8).Column(column =>
            {
                // Fechas de emisión y vencimiento en la misma línea
                column.Item().Row(row =>
                {
                    row.RelativeItem().Text($"Fecha de Emisión: {DateTime.Now:dd-MMM-yy}").FontSize(8).Bold();
                    row.RelativeItem().AlignRight().Text($"Fecha Vencimiento: {_installment.DueDate:dd-MMM-yy}").FontSize(8).Bold();
                });

                column.Item().Height(6);

                if (!string.IsNullOrWhiteSpace(footerText))
                {
                    column.Item().Background(Colors.Grey.Lighten4)
                        .Border(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(6)
                        .Text(footerText).FontSize(7);
                }

                column.Item().Height(4);

                column.Item().AlignCenter().Text(text =>
                {
                    text.Span("Generado por SpiderHoodApp el: ").FontSize(7);
                    text.Span($"{DateTime.Now:dd/MM/yyyy HH:mm}").Bold().FontSize(7);
                });
            });
        }

        private string ResolveFooterText()
        {
            var template = _building.Configuration.ReceiptFooterText;
            if (string.IsNullOrWhiteSpace(template)) return "";

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
            // CAMBIO CLAVE: Línea inferior en lugar de fondo de color pesado
            table.Cell().ColumnSpan(4).PaddingTop(6).PaddingBottom(2)
                .BorderBottom(1).BorderColor(Colors.Blue.Medium)
                .Text(title).Bold().FontSize(9).FontColor(Colors.Black);
        }

        private void AddSectionSubtotal(TableDescriptor table, decimal presupTotal, decimal cuotaTotal)
        {
            table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Medium).Text("");
            table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Medium).AlignRight().Text($"S/ {presupTotal:N2}").Bold().FontSize(8);
            table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Medium).AlignRight().Text($"S/ {cuotaTotal:N2}").Bold().FontSize(8);
            table.Cell().BorderTop(0.5f).BorderColor(Colors.Grey.Medium).Text("");
        }

        // CAMBIO CLAVE: Versión inline compacta en lugar de mini-tabla separada
        private void AddWaterReadingInline(TableDescriptor table, ServiceReadingDetail waterReading)
        {
            table.Cell().ColumnSpan(4).PaddingLeft(8).PaddingVertical(2)
                .Background(Colors.Cyan.Lighten5).BorderLeft(2).BorderColor(Colors.Cyan.Medium).Padding(4)
                .Row(row =>
                {
                    row.RelativeItem().Text("Lectura de Agua por Dpto").Bold().FontSize(7).FontColor(Colors.Cyan.Darken2);
                    row.ConstantItem(90).AlignCenter().Text($"Ant: {waterReading.PreviousReading:N2}").FontSize(7);
                    row.ConstantItem(90).AlignCenter().Text($"Act: {waterReading.CurrentReading:N2}").FontSize(7);
                    row.ConstantItem(110).AlignRight().Text($"Cons: {waterReading.Consumption:N2} m³ = S/ {waterReading.CalculatedAmount:N2}")
                        .Bold().FontSize(7).FontColor(Colors.Cyan.Darken2);
                });
        }

        // CAMBIO CLAVE: Eliminada la alternancia de colores (shaded). Siempre limpio.
        private void AddTableRow(TableDescriptor table, string description, decimal presupuesto, decimal cuota, int tipo)
        {
            table.Cell().PaddingVertical(2).Text(description).FontSize(8);
            table.Cell().PaddingVertical(2).AlignRight().Text(presupuesto > 0 ? $"S/ {presupuesto:N2}" : "-").FontSize(8);
            table.Cell().PaddingVertical(2).AlignRight().Text(cuota > 0 ? $"S/ {cuota:N2}" : "-").FontSize(8);
            table.Cell().PaddingVertical(2).AlignRight().Text(GetDistributionType(tipo)).FontSize(7).FontColor(Colors.Grey.Darken1);
        }

        // A diferencia de AddTableRow (que oculta cualquier valor <= 0 detrás de un "-" --
        // correcto para ítems de presupuesto normales, donde "-" significa "no aplica"),
        // acá el signo importa: un monto negativo es un saldo a favor real (Debt = Amount -
        // AmountPaid, negativo cuando se pagó de más) y tiene que verse tal cual -- un "-"
        // ahí se leería como "no debe nada", ocultando justo el saldo a favor que se le
        // quiere avisar al propietario.
        private void AddDeudaAnteriorRow(TableDescriptor table, string description, decimal monto)
        {
            table.Cell().PaddingVertical(2).Text(description).FontSize(8);
            table.Cell().PaddingVertical(2).Text("");
            table.Cell().PaddingVertical(2).AlignRight()
                .Text($"S/ {monto:N2}").FontSize(8)
                .FontColor(monto < 0 ? Colors.Green.Darken2 : Colors.Black);
            table.Cell().PaddingVertical(2).Text("");
        }

        private decimal CalculateItemAmount(BudgetDetail item)
        {
            var pesoFija = GetUnitCount(_installment.IdGroupUnit);
            bool exonerado = _exonerations.Any(c => c.IdCategory == item.IdCategory && c.IdGroupUnit == _installment.IdGroupUnit);

            if (exonerado) return 0;

            var nroExcepciones = _exonerations.Count(c => c.IdCategory == item.IdCategory);
            var unidadesQueDividen = item.NroApartments ?? (GetTotalUnits() - nroExcepciones);

            var total = item.Type == 1
                ? item.MonthlyAmount / unidadesQueDividen * pesoFija
                : item.MonthlyAmount * (_installment.Percent / 100);

            return Math.Round(total, 2);
        }

        private int GetTotalUnits() => _totalApartments;
        private int GetUnitCount(Guid idGroupUnit) => _unitCountByGroup.TryGetValue(idGroupUnit, out var count) && count > 0 ? count : 1;

        private string TipoDescripcion(InstallmentType tipo) => tipo switch
        {
            InstallmentType.Extraordinaria => "CUOTA EXTRAORDINARIA",
            InstallmentType.Multa => "MULTA",
            InstallmentType.Mora => "MORA",
            _ => "CUOTA ORDINARIA"
        };

        private string GetDistributionType(int tipo) => tipo switch
        {
            1 => "Por Unidad",
            2 => "Por Área",
            3 => "Por Consumo",
            _ => "Fijo"
        };

        private List<SectionInfo> GetSections()
        {
            if (_budget?.Details == null) return new();

            return _budget.Details
                .Where(x => x.IsHeader)
                .DistinctBy(x => x.IdSection)
                .Select(x => new SectionInfo { Id = x.IdSection, Name = x.Description, IdCategory = x.IdCategory })
                .OrderBy(x => x.Id)
                .ToList();
        }
    }
}
