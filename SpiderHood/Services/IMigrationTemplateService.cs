using ClosedXML.Excel;

namespace SpiderHood.Services
{
    // Genera las plantillas de carga histórica (migración inicial desde un Excel de
    // control externo), personalizadas por edificio: cada plantilla trae, como listas
    // desplegables, las Unidades/Cuentas Bancarias/Categorías que YA existen en
    // SpiderHood para ese edificio -- así el administrador no reescribe nombres a mano
    // y no termina creando categorías/cuentas duplicadas al importar.
    //
    // Distinto del importador del día a día (BankAccountService.ProcesarArchivoEstadoCuentaAsync,
    // ExcelExportService.ExportarPlantillaVacia para lecturas de agua): esas plantillas
    // cargan UN periodo/cuenta a la vez con el formato que entrega el banco. Estas
    // cargan AÑOS de historial de una sola vez, ya consolidado por el administrador
    // (ver análisis de migración) -- no se llenan copiando el reporte del banco tal cual.
    public interface IMigrationTemplateService
    {
        Task<(string FileName, byte[] Bytes)> GenerarPlantillaUnidadesYPropietariosAsync(Guid idBuilding);
        Task<(string FileName, byte[] Bytes)> GenerarPlantillaCuotasYPagosAsync(Guid idBuilding);
        Task<(string FileName, byte[] Bytes)> GenerarPlantillaEstadoDeCuentaAsync(Guid idBuilding);
        Task<(string FileName, byte[] Bytes)> GenerarPlantillaLecturasAguaAsync(Guid idBuilding);
        Task<(string FileName, byte[] Bytes)> GenerarPlantillaPresupuestoHistoricoAsync(Guid idBuilding);
    }

    public class MigrationTemplateService : IMigrationTemplateService
    {
        private readonly IBuildingService _buildingService;
        private readonly IBankAccountService _bankAccountService;
        private readonly ICategoryService _categoryService;

        public MigrationTemplateService(
            IBuildingService buildingService,
            IBankAccountService bankAccountService,
            ICategoryService categoryService)
        {
            _buildingService = buildingService;
            _bankAccountService = bankAccountService;
            _categoryService = categoryService;
        }

        // Modelo real de agrupamiento (rastreado en Owners.razor/AssignUnits.razor, no
        // hay documentación aparte): un Depto/Oficina (TypeUnit 1/4) es la "cabeza" --
        // al asignarle un propietario nace un IdGroupOwner nuevo. Cada Estacionamiento/
        // Depósito (TypeUnit 2/3) que se agrega a ese propietario es OTRA fila de
        // GroupUnit apuntando al MISMO IdGroupOwner (TypeGroupUnit=Shared). Una unidad
        // sin GroupUnit queda "libre" (AssignUnits.razor.FreeUnits) -- hoy eso significa
        // que no genera cuota a nadie (ver conversación sobre RealEstateCompany como
        // pagador por defecto, pendiente en IBudgetService.LoadDataDefaultAsync).
        //
        // Esta plantilla sólo genera el archivo de carga -- igual que las otras 4, el
        // importador (que arma Owner + OwnerUnit + GroupUnit a partir de estas filas)
        // es la fase siguiente.
        public async Task<(string FileName, byte[] Bytes)> GenerarPlantillaUnidadesYPropietariosAsync(Guid idBuilding)
        {
            var cabezasExistentes = await ObtenerCabezasDeGrupoAsync(idBuilding);

            using var workbook = new XLWorkbook();

            var wsUnidades = workbook.Worksheets.Add("Unidades");
            var headersUnidades = new[]
            {
                "Código de Unidad", "Tipo", "Área (m²)", "Unidad Cabeza de Grupo (vacío si ESTA fila es la cabeza)"
            };
            EscribirEncabezado(wsUnidades, headersUnidades);

            EscribirFilaEjemplo(wsUnidades, 2, new object[] { "301", "Departamento", 85.4, "" });
            EscribirFilaEjemplo(wsUnidades, 3, new object[] { "E-12", "Estacionamiento", 12.0, "301" });
            EscribirFilaEjemplo(wsUnidades, 4, new object[] { "D-05", "Depósito", 4.5, "301" });

            AplicarListaInline(wsUnidades, "B5:B2000", "Departamento,Oficina,Estacionamiento,Depósito");
            var referenciaCabezas = cabezasExistentes.Count > 0 ? cabezasExistentes : new List<string> { "301" };
            AplicarListaValidacion(wsUnidades, "D5:D2000", referenciaCabezas, "UnidadCabeza");

            AjustarColumnas(wsUnidades, 20, 16, 12, 44);

            var wsPropietarios = workbook.Worksheets.Add("Propietarios");
            var headersPropietarios = new[]
            {
                "Unidad Cabeza de Grupo", "Tipo de Propietario", "Nombres / Razón Social", "Apellidos",
                "Tipo de Documento", "Número de Documento", "Dirección", "Teléfono", "Email"
            };
            EscribirEncabezado(wsPropietarios, headersPropietarios);

            EscribirFilaEjemplo(wsPropietarios, 2, new object[]
            {
                "301", "Persona Natural", "Jocelyn", "Coronel Villegas", "DNI", "45678912",
                "Av. Siempre Viva 123", "987654321", "jocelyn@correo.com"
            });
            EscribirFilaEjemplo(wsPropietarios, 3, new object[]
            {
                "302", "Persona Jurídica", "Inmobiliaria XYZ S.A.C.", "", "RUC", "20123456789",
                "Av. Los Olivos 456", "014567890", "contacto@xyz.com"
            });

            AplicarListaInline(wsPropietarios, "B4:B2000", "Persona Natural,Persona Jurídica");
            AplicarListaValidacion(wsPropietarios, "A4:A2000", referenciaCabezas, "UnidadCabezaProp");

            AjustarColumnas(wsPropietarios, 20, 16, 26, 20, 14, 18, 26, 14, 24);

            AgregarInstrucciones(workbook, "Plantilla: Unidades y Propietarios", new[]
            {
                "Hoja 'Unidades': una fila por CADA unidad física del edificio (Departamentos, Oficinas, Estacionamientos, Depósitos), incluyendo las que aún no tienen dueño.",
                "Solo Departamento u Oficina pueden ser 'cabeza de grupo' -- son las unidades que se venden con nombre propio. Un Estacionamiento o Depósito SIEMPRE pertenece a una cabeza: complete su columna 'Unidad Cabeza de Grupo' con el código del Departamento/Oficina correspondiente.",
                "Hoja 'Propietarios': una fila por cada Departamento/Oficina QUE YA TIENE dueño (no hace falta fila para Estacionamientos/Depósitos -- se heredan de su cabeza; ni para unidades todavía sin vender).",
                "Las unidades que no aparezcan en 'Propietarios' quedan registradas como disponibles/sin vender -- igual que si se crearan desde la pantalla de Unidades sin asignarles propietario todavía.",
                "El % de cuota de cada unidad NO se ingresa a mano -- el sistema lo calcula del 'Área (m²)' al generar el presupuesto.",
                "Copropietarios o residentes adicionales sobre una misma unidad se agregan después, desde Propietarios en la pantalla, no en esta carga inicial.",
                cabezasExistentes.Count == 0
                    ? "Este edificio todavía no tiene Departamentos/Oficinas registrados -- la lista de 'Unidad Cabeza de Grupo' viene vacía, escriba el código a mano."
                    : $"Departamentos/Oficinas ya registrados en este edificio (disponibles como referencia): {string.Join(", ", cabezasExistentes)}."
            });

            workbook.Worksheet("Instrucciones").Position = 1;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ($"Plantilla_UnidadesYPropietarios_{DateTime.Now:yyyyMMdd}.xlsx", ms.ToArray());
        }

        public async Task<(string FileName, byte[] Bytes)> GenerarPlantillaCuotasYPagosAsync(Guid idBuilding)
        {
            var (unidades, errorUnidades) = await ObtenerCodigosUnidadAsync(idBuilding);
            var cuentas = await ObtenerCuentasAsync(idBuilding);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Cuotas");

            var headers = new[]
            {
                "Periodo (AAAA-MM)", "Unidad", "Tipo de Cuota", "Concepto",
                "Monto Cuota (S/.)", "Fecha de Vencimiento", "Monto Pagado (S/.)",
                "Fecha de Pago", "Cuenta Bancaria del Pago", "Referencia de Pago"
            };
            EscribirEncabezado(ws, headers);

            EscribirFilaEjemplo(ws, 2, new object[]
            {
                DateTime.Today.ToString("yyyy-MM"), unidades.FirstOrDefault() ?? "EJEMPLO", "Ordinaria", "",
                450.00, DateTime.Today, 450.00, DateTime.Today, cuentas.FirstOrDefault().Numero ?? "", "Transferencia"
            });
            EscribirFilaEjemplo(ws, 3, new object[]
            {
                DateTime.Today.ToString("yyyy-MM"), unidades.FirstOrDefault() ?? "EJEMPLO", "Extraordinaria",
                "Regularización de Agua", 45.00, DateTime.Today, 0, "", "", ""
            });

            AplicarListaValidacion(ws, "B4:B2000", unidades, "Unidad");
            AplicarListaInline(ws, "C4:C2000", "Ordinaria,Extraordinaria");
            AplicarListaValidacion(ws, "I4:I2000", cuentas.Select(c => c.Numero).ToList(), "Cuenta");

            AjustarColumnas(ws, 16, 10, 16, 30, 14, 16, 14, 14, 22, 20);

            AgregarInstrucciones(workbook, "Plantilla: Cuotas y Pagos Históricos", new[]
            {
                "Una fila por cuota generada a un propietario/unidad en un periodo. Si la cuota se pagó (total o parcialmente), complete también las columnas de pago en la misma fila.",
                "'Unidad' se elige de la lista desplegable -- son las unidades ya registradas en SpiderHood para este edificio.",
                "'Tipo de Cuota' = Ordinaria para la cuota mensual normal. Use Extraordinaria para fondos, regularizaciones de agua (\"Reg. Agua\"), multas o cualquier cargo puntual -- y describa el motivo en 'Concepto'.",
                "Si una cuota se pagó en más de un abono, agregue una fila adicional igual a la original pero solo con las columnas de pago llenas -- el importador las suma contra la misma cuota.",
                "'Cuenta Bancaria del Pago' y 'Referencia de Pago' son opcionales: si además carga la plantilla de Estado de Cuenta, el sistema intenta conciliar automáticamente por fecha + monto + cuenta.",
                "Deje 'Monto Pagado' en 0 y las columnas de pago vacías si la cuota sigue impaga.",
                MensajeUnidades(unidades, errorUnidades)
            });

            workbook.Worksheet("Instrucciones").Position = 1;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ($"Plantilla_CuotasYPagos_{DateTime.Now:yyyyMMdd}.xlsx", ms.ToArray());
        }

        public async Task<(string FileName, byte[] Bytes)> GenerarPlantillaEstadoDeCuentaAsync(Guid idBuilding)
        {
            var cuentas = await ObtenerCuentasAsync(idBuilding);
            var categorias = await ObtenerCategoriasRaizAsync(idBuilding);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Movimientos");

            var headers = new[]
            {
                "Cuenta Bancaria", "Fecha", "Tipo", "Categoría", "Descripción",
                "Moneda", "ITF (S/.)", "Monto (S/.)", "Es Saldo Inicial"
            };
            EscribirEncabezado(ws, headers);

            EscribirFilaEjemplo(ws, 2, new object[]
            {
                cuentas.FirstOrDefault().Numero ?? "EJEMPLO", DateTime.Today.AddYears(-1), "Egreso",
                categorias.FirstOrDefault() ?? "", "Apertura de cuenta", "S/", 0, 0, "Sí"
            });
            EscribirFilaEjemplo(ws, 3, new object[]
            {
                cuentas.FirstOrDefault().Numero ?? "EJEMPLO", DateTime.Today, "Ingreso",
                categorias.FirstOrDefault(c => c.Equals("Cuota", StringComparison.OrdinalIgnoreCase)) ?? categorias.FirstOrDefault() ?? "",
                "Abono de mantenimiento", "S/", 0, 450.00, "No"
            });

            AplicarListaValidacion(ws, "A4:A2000", cuentas.Select(c => c.Numero).ToList(), "Cuenta");
            AplicarListaInline(ws, "C4:C2000", "Ingreso,Egreso");
            AplicarListaValidacion(ws, "D4:D2000", categorias, "Categoría");
            AplicarListaInline(ws, "F4:F2000", "S/,US$");
            AplicarListaInline(ws, "I4:I2000", "Sí,No");

            AjustarColumnas(ws, 22, 14, 10, 20, 34, 8, 12, 14, 14);

            AgregarInstrucciones(workbook, "Plantilla: Estado de Cuenta Histórico", new[]
            {
                "Una fila por movimiento bancario (ingreso o egreso) de cualquiera de las cuentas del edificio, para todo el periodo que se quiera migrar.",
                "No se llena copiando el reporte del banco tal cual -- se arma a partir de su propio registro consolidado (fecha + descripción + monto, con o sin ITF aparte).",
                "'Cuenta Bancaria' y 'Categoría' se eligen de listas desplegables con lo que ya existe en este edificio. Si necesita una categoría nueva, créela primero en Categorías y vuelva a descargar la plantilla.",
                "El monto se ingresa siempre en positivo; el signo lo determina la columna 'Tipo'.",
                "'Es Saldo Inicial' = Sí SOLO en la fila que representa el saldo de apertura de cada cuenta (normalmente su primera fila). Se usa una única vez por cuenta y queda fijo en la ficha de la cuenta bancaria -- no editable después.",
                cuentas.Count == 0
                    ? "Este edificio todavía no tiene cuentas bancarias registradas -- la lista de 'Cuenta Bancaria' está vacía. Regístrelas primero en Edificios > Cuentas Bancarias."
                    : $"Cuentas bancarias disponibles: {string.Join(", ", cuentas.Select(c => c.Numero))}.",
                categorias.Count == 0
                    ? "Este edificio todavía no tiene categorías registradas -- la lista de 'Categoría' está vacía. Créelas primero en Categorías."
                    : $"Categorías disponibles: {string.Join(", ", categorias)}."
            });

            workbook.Worksheet("Instrucciones").Position = 1;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ($"Plantilla_EstadoDeCuenta_{DateTime.Now:yyyyMMdd}.xlsx", ms.ToArray());
        }

        public async Task<(string FileName, byte[] Bytes)> GenerarPlantillaLecturasAguaAsync(Guid idBuilding)
        {
            var (unidades, errorUnidades) = await ObtenerCodigosUnidadAsync(idBuilding);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Lecturas");

            var headers = new[]
            {
                "Unidad", "Periodo (AAAA-MM)", "Lectura (m³)",
                "Lectura Inicial (solo 1er periodo de la unidad)", "Fecha de Lectura"
            };
            EscribirEncabezado(ws, headers);

            EscribirFilaEjemplo(ws, 2, new object[]
            {
                unidades.FirstOrDefault() ?? "EJEMPLO", DateTime.Today.AddMonths(-1).ToString("yyyy-MM"), 393.98, 378.263, DateTime.Today.AddMonths(-1)
            });
            EscribirFilaEjemplo(ws, 3, new object[]
            {
                unidades.FirstOrDefault() ?? "EJEMPLO", DateTime.Today.ToString("yyyy-MM"), 412.11, "", DateTime.Today
            });

            AplicarListaValidacion(ws, "A4:A2000", unidades, "Unidad");

            AjustarColumnas(ws, 10, 18, 14, 34, 16);

            AgregarInstrucciones(workbook, "Plantilla: Lecturas de Agua Históricas", new[]
            {
                "Una fila por unidad y periodo -- todo el historial que quiera cargar, en el orden que prefiera (el sistema las ordena por fecha al importar).",
                "'Lectura' es la lectura acumulada del medidor a esa fecha, no el consumo del mes -- el consumo se calcula contra la lectura del periodo anterior de la misma unidad.",
                "'Lectura Inicial' solo se llena en la fila del primer periodo histórico de cada unidad. Déjela vacía en las demás filas.",
                "'Fecha de Lectura' es opcional; si se deja vacía, el sistema usa el último día del Periodo indicado.",
                MensajeUnidades(unidades, errorUnidades)
            });

            workbook.Worksheet("Instrucciones").Position = 1;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ($"Plantilla_LecturasAgua_{DateTime.Now:yyyyMMdd}.xlsx", ms.ToArray());
        }

        public async Task<(string FileName, byte[] Bytes)> GenerarPlantillaPresupuestoHistoricoAsync(Guid idBuilding)
        {
            var categorias = await ObtenerCategoriasRaizAsync(idBuilding);

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Presupuesto");

            var headers = new[]
            {
                "Periodo (AAAA-MM)", "Categoría", "Descripción del Ítem", "Monto Mensual (S/.)", "Frecuencia"
            };
            EscribirEncabezado(ws, headers);

            EscribirFilaEjemplo(ws, 2, new object[]
            {
                DateTime.Today.ToString("yyyy-MM"), categorias.FirstOrDefault() ?? "", "Limpieza de áreas comunes", 380.00, "Mensual"
            });
            EscribirFilaEjemplo(ws, 3, new object[]
            {
                DateTime.Today.ToString("yyyy-MM"), categorias.Skip(1).FirstOrDefault() ?? categorias.FirstOrDefault() ?? "", "Sueldo personal de portería", 1450.00, "Mensual"
            });

            AplicarListaValidacion(ws, "B4:B2000", categorias, "Categoría");
            AplicarListaInline(ws, "E4:E2000", "Mensual,Bimestral,Trimestral,Semestral,Anual");

            AjustarColumnas(ws, 16, 20, 34, 16, 12);

            AgregarInstrucciones(workbook, "Plantilla: Presupuesto Histórico por Periodo", new[]
            {
                "Una fila por ítem de gasto presupuestado en cada periodo (mes) -- reconstruye, mes a mes, el presupuesto real que generó cada cuota.",
                "Todas las filas con el mismo 'Periodo' forman un solo presupuesto de ese mes, con un ítem por fila.",
                "'Categoría' se elige de la lista desplegable -- son las categorías raíz ya creadas para este edificio. Si necesita una nueva, créela primero en Categorías y vuelva a descargar la plantilla.",
                "Si en un periodo no tiene el desglose real por categoría, puede cargar una sola fila con el monto total en la categoría más cercana -- pierde el detalle en el recibo de ese periodo, pero mantiene la trazabilidad del monto.",
                categorias.Count == 0
                    ? "Este edificio todavía no tiene categorías registradas -- la lista de 'Categoría' está vacía. Créelas primero en Categorías."
                    : $"Categorías disponibles: {string.Join(", ", categorias)}."
            });

            workbook.Worksheet("Instrucciones").Position = 1;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ($"Plantilla_PresupuestoHistorico_{DateTime.Now:yyyyMMdd}.xlsx", ms.ToArray());
        }

        // ---------------------------------------------------------------
        // Datos maestros del edificio (para las listas desplegables)
        // ---------------------------------------------------------------

        // Departamentos/Oficinas ya existentes (TypeUnit 1/4) -- referencia para que,
        // al agregar unidades nuevas a un edificio que ya tiene algunas cargadas, el
        // admin pueda apuntar un Estacionamiento/Depósito nuevo a una cabeza que ya
        // existe sin volver a escribirla a mano. Usa GetUnitsByBuildingAsync (no
        // GetGroupUnitsByTypeAsync) porque es la misma consulta que ya usa
        // AssignUnits.razor para listar "unidades libres" en producción -- pero se
        // atrapa igual por las dudas, para que un problema de datos acá tampoco tumbe
        // esta plantilla.
        private async Task<List<string>> ObtenerCabezasDeGrupoAsync(Guid idBuilding)
        {
            try
            {
                var unidades = await _buildingService.GetUnitsByBuildingAsync(idBuilding);
                return unidades
                    .Where(u => u.TypeUnit == 1 || u.TypeUnit == 4)
                    .Select(u => u.UnitNumber)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        // GetGroupUnitsByTypeAsync (GET_UnitsByType) puede reventar con
        // SqlNullValueException si alguna unidad del edificio tiene una columna Guid en
        // NULL (p.ej. IdGroupUnit de una unidad todavía sin asignar a ningún
        // propietario/grupo) -- mismo llamado que ya usa el botón "Descargar plantilla"
        // de Lecturas de Agua (BlockWaterReading.razor.ObtenerUnidadesDelEdificio), así
        // que es un problema de datos preexistente del edificio, no de esta plantilla.
        // Se atrapa acá para que ESA falla no tumbe toda la descarga -- el desplegable de
        // Unidad queda vacío y AgregarInstrucciones avisa con un mensaje distinto al de
        // "no hay unidades registradas".
        private async Task<(List<string> Codigos, bool Error)> ObtenerCodigosUnidadAsync(Guid idBuilding)
        {
            try
            {
                var unidades = await _buildingService.GetGroupUnitsByTypeAsync(idBuilding, 1);
                var codigos = unidades
                    .Select(u => u.UnitNumber)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                return (codigos, false);
            }
            catch (Exception)
            {
                return (new List<string>(), true);
            }
        }

        private static string MensajeUnidades(List<string> unidades, bool error)
        {
            if (error)
                return "No se pudo cargar la lista de unidades de este edificio (problema al leer datos de unidades) -- la lista de 'Unidad' quedó vacía. Puede escribir el código de la unidad a mano en cada fila; si el problema persiste, revise si hay unidades sin propietario/grupo asignado en Edificios > Unidades.";
            if (unidades.Count == 0)
                return "Este edificio todavía no tiene unidades registradas -- la lista de 'Unidad' está vacía. Regístrelas primero en Edificios > Unidades.";
            return $"Unidades disponibles en este edificio: {unidades.Count}.";
        }

        private async Task<List<(string Numero, string Banco)>> ObtenerCuentasAsync(Guid idBuilding)
        {
            var cuentas = await _bankAccountService.ObtenerCuentasBancariasAsync(idBuilding);
            return cuentas
                .Where(c => !string.IsNullOrWhiteSpace(c.AccountNumber))
                .Select(c => (Numero: c.AccountNumber.Trim(), Banco: c.BankName))
                .ToList();
        }

        // Solo categorías raíz (Nivel == 0) -- son las secciones reales del presupuesto
        // (BudgetDetail.IdSection), igual criterio que InstallmentDetailModal.GetSections()
        // y CategoryPage. Los subniveles se dejan como texto libre en "Descripción del
        // Ítem" -- no tiene sentido migrarlos como una lista cerrada.
        private async Task<List<string>> ObtenerCategoriasRaizAsync(Guid idBuilding)
        {
            var categorias = await _categoryService.GetCategoriesAsync(idBuilding);
            return categorias
                .Where(c => c.Nivel == 0)
                .Select(c => c.Description)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        // ---------------------------------------------------------------
        // Helpers de construcción del Excel (mismo look que Utilities.cs:
        // encabezado en negrita con fondo gris, hoja "Instrucciones" aparte)
        // ---------------------------------------------------------------

        private static void EscribirEncabezado(IXLWorksheet ws, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.WrapText = true;
            }
            ws.Row(1).Height = 30;
            ws.SheetView.FreezeRows(1);
        }

        // Filas 2-3: ejemplo de referencia (fondo amarillo claro, cursiva) -- mismo
        // criterio que la plantilla de Lecturas de Agua: el importador debe ignorar
        // toda fila cuya Unidad/Cuenta = "EJEMPLO", o el usuario la borra antes de
        // cargar sus datos reales.
        private static void EscribirFilaEjemplo(IXLWorksheet ws, int row, object[] valores)
        {
            for (int i = 0; i < valores.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                switch (valores[i])
                {
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.DateFormat.Format = "yyyy-MM-dd";
                        break;
                    case double d:
                        cell.Value = d;
                        break;
                    case int n:
                        cell.Value = n;
                        break;
                    case bool b:
                        cell.Value = b;
                        break;
                    case string s:
                        cell.Value = s;
                        break;
                    default:
                        cell.Value = valores[i]?.ToString() ?? string.Empty;
                        break;
                }
                cell.Style.Font.Italic = true;
                cell.Style.Font.FontColor = XLColor.FromArgb(0x7F, 0x60, 0x00);
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xFF, 0xF6, 0xD9);
            }
        }

        // Lista desplegable a partir de datos reales del edificio (cantidad variable) --
        // se escriben en una hoja "Listas" oculta y la validación apunta a ese rango, en
        // vez de una lista inline (el literal de ClosedXML/Excel tiene tope de ~255
        // caracteres, insuficiente para edificios con muchas unidades/categorías).
        private static void AplicarListaValidacion(IXLWorksheet ws, string rango, List<string> valores, string nombreColumna)
        {
            if (valores.Count == 0) return;

            var wb = ws.Workbook;
            if (!wb.Worksheets.TryGetWorksheet("Listas", out var listas))
                listas = wb.Worksheets.Add("Listas");
            var columna = listas.Column(ProximaColumnaLibre(listas));
            var colLetter = columna.ColumnLetter();

            listas.Cell(1, columna.ColumnNumber()).Value = nombreColumna;
            for (int i = 0; i < valores.Count; i++)
                listas.Cell(i + 2, columna.ColumnNumber()).Value = valores[i];

            listas.Visibility = XLWorksheetVisibility.Hidden;

            var rangoListas = listas.Range($"{colLetter}2:{colLetter}{valores.Count + 1}");
            ws.Range(rango).SetDataValidation().List(rangoListas, true);
        }

        private static int ProximaColumnaLibre(IXLWorksheet listas)
        {
            var usado = listas.LastColumnUsed();
            return usado == null ? 1 : usado.ColumnNumber() + 1;
        }

        // Lista fija corta (Ordinaria/Extraordinaria, Ingreso/Egreso, Sí/No, etc.) --
        // sí cabe cómoda como lista inline.
        //
        // OJO: List(string) NO encierra el literal entre comillas por su cuenta --
        // hay que mandárselo ya entrecomillado ("Ordinaria,Extraordinaria" en vez de
        // Ordinaria,Extraordinaria) o escribe <formula1>Ordinaria,Extraordinaria</formula1>
        // en vez de <formula1>"Ordinaria,Extraordinaria"</formula1>, que es lo que exige
        // el formato -- Excel lo marca como contenido dañado al abrir (bug conocido de
        // ClosedXML, ver https://github.com/ClosedXML/ClosedXML/issues/2822).
        private static void AplicarListaInline(IXLWorksheet ws, string rango, string opciones)
        {
            ws.Range(rango).SetDataValidation().List($"\"{opciones}\"", true);
        }

        private static void AjustarColumnas(IXLWorksheet ws, params double[] anchos)
        {
            for (int i = 0; i < anchos.Length; i++)
                ws.Column(i + 1).Width = anchos[i];
        }

        private static void AgregarInstrucciones(XLWorkbook workbook, string titulo, string[] bullets)
        {
            var ws = workbook.Worksheets.Add("Instrucciones");
            ws.ShowGridLines = false;

            ws.Cell(1, 1).Value = titulo;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Range(1, 1, 1, 6).Merge();

            ws.Cell(3, 1).Value = "Cómo usar esta plantilla";
            ws.Cell(3, 1).Style.Font.Bold = true;

            int row = 4;
            foreach (var bullet in bullets)
            {
                ws.Cell(row, 1).Value = "•  " + bullet;
                ws.Range(row, 1, row, 6).Merge();
                ws.Cell(row, 1).Style.Alignment.WrapText = true;
                ws.Row(row).Height = 30;
                row++;
            }

            row++;
            ws.Cell(row, 1).Value = "Leyenda de celdas";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Fila 1 (gris)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = "Encabezado -- no modifique nombres ni orden de columnas.";
            ws.Range(row, 2, row, 6).Merge();
            row++;
            ws.Cell(row, 1).Value = "Filas 2-3 (amarillas, cursiva)";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = "Ejemplo de referencia -- bórrelas antes de importar.";
            ws.Range(row, 2, row, 6).Merge();
            row++;
            ws.Cell(row, 1).Value = "Filas siguientes";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = "Sus datos reales, una fila por registro.";
            ws.Range(row, 2, row, 6).Merge();

            ws.Columns(1, 6).AdjustToContents();
            ws.Column(1).Width = Math.Max(ws.Column(1).Width, 22);
        }
    }
}
