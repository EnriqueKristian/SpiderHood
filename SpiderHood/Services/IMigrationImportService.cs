using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;

namespace SpiderHood.Services
{
    public class MigrationImportResult
    {
        public int UnidadesCreadas { get; set; }
        public int UnidadesOmitidas { get; set; }
        public int PropietariosCreados { get; set; }
        public int LecturasCreadas { get; set; }
        public int PresupuestosCreados { get; set; }
        public int ItemsPresupuestoCreados { get; set; }
        public int CuotasCreadas { get; set; }
        public int PagosCreados { get; set; }
        public int MovimientosCreados { get; set; }
        public List<string> Errores { get; } = new();
        public List<string> Advertencias { get; } = new();
        public bool Success => Errores.Count == 0;
    }

    // Importadores de las 5 plantillas de migración histórica (ver
    // IMigrationTemplateService para la generación de cada una). No concilian pagos
    // contra movimientos bancarios entre sí (Cuotas y Estado de Cuenta se cargan
    // independientes, con IdTransaction = Guid.Empty) ni recalculan tarifas/cuotas
    // en vivo -- ver Docs/Pendientes-Negocio-Migracion.md para lo que queda afuera
    // a propósito.
    //
    // Unidades y Propietarios reproduce a mano la misma secuencia de escritura que
    // ya usa Owners.razor al asignar un propietario (rastreada ahí, no hay
    // documentación aparte del modelo):
    //   1) RealEstateUnit por cada unidad física (AddUnitAsync).
    //   2) Owner por cada propietario (AddOwnerAsync).
    //   3) UN OwnerUnit por grupo, con un IdGroupOwner nuevo (AddOwnerUnitAsync) --
    //      "el grupo" nace acá, no existe antes.
    //   4) Un GroupUnit por cada unidad del grupo (cabeza + agregados), apuntando al
    //      mismo IdGroupOwner (AddGroupUnitAsync) -- Individual para la cabeza
    //      (Depto/Oficina), Shared para Estacionamiento/Depósito.
    //
    // Unidades sin fila en "Propietarios" quedan creadas pero sin grupo -- "libres",
    // igual que si se cargaran desde la UI sin asignarles propietario todavía.
    //
    // OJO -- por qué Unidades/Propietarios y Lecturas de Agua usan BDLayout directo
    // (campo _ec) en vez de IBuildingService/IOwnerService/IServiceReadingService
    // para sus Add: AddUnitAsync, AddOwnerUnitAsync, AddGroupUnitAsync (BuildingService)
    // y AddServiceReadingAsync/AddServiceReadingDetailAsync (ServiceReadingService)
    // atrapan la excepción con un catch mudo (Console.WriteLine, sin throw) -- un
    // INSERT que falla de verdad en SQL (encontrado en producción: INS_ServiceReading
    // fallando en cada periodo) queda invisible para quien los llama, y este
    // importador reportaba "importado con éxito" habiendo guardado prácticamente
    // nada. BDLayout.AddNewRecordAsync sí relanza (como RepositoryException, con la
    // excepción real de SQL en .InnerException) -- por eso se usa directo acá, y se
    // captura por fila/periodo para no abortar todo el lote por un solo error.
    // AddOwnerAsync/AddInstallmentAsync/AgregarPagoAsync/AddTransactionBankHeaderAsync/
    // AddTransactionFromEECCAsync/CreatePresupuestoAsync/AddDetalleToPresupuestoAsync
    // sí relanzan correctamente -- esos importadores no tenían este problema.
    //
    // Mismo antipatrón encontrado después en una LECTURA:
    // IBankAccountService.ObtenerCuentasBancariasAsync (usado por
    // ImportarEstadoDeCuentaAsync) también atrapa cualquier error y devuelve una
    // lista vacía -- ahí el efecto era más engañoso todavía, porque con la lista
    // vacía CADA fila del archivo reporta "la cuenta no existe" (una validación que
    // sí corre, pero sobre datos vacíos por la falla oculta) en vez de mostrar el
    // error real. Se reemplazó por BDLayout.GetBankAccountsByBuildingAsync directo.
    public interface IMigrationImportService
    {
        Task<MigrationImportResult> ImportarUnidadesYPropietariosAsync(Guid idBuilding, Stream archivo);
        Task<MigrationImportResult> ImportarLecturasAguaAsync(Guid idBuilding, Stream archivo);
        Task<MigrationImportResult> ImportarPresupuestoHistoricoAsync(Guid idBuilding, Stream archivo);
        Task<MigrationImportResult> ImportarCuotasYPagosAsync(Guid idBuilding, Stream archivo);
        Task<MigrationImportResult> ImportarEstadoDeCuentaAsync(Guid idBuilding, Stream archivo);
    }

    public class MigrationImportService : IMigrationImportService
    {
        private readonly IBuildingService _buildingService;
        private readonly IOwnerService _ownerService;
        private readonly ParameterService _parameterService;
        private readonly IServiceReadingService _serviceReadingService;
        private readonly IBudgetService _budgetService;
        private readonly ICategoryService _categoryService;
        private readonly IInstallmentService _installmentService;
        private readonly IBankAccountService _bankAccountService;
        private readonly AuthService _authService;
        private readonly BDLayout _ec;

        private static readonly Dictionary<string, int> MapaTipoUnidad = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Departamento"] = 1,
            ["Estacionamiento"] = 2,
            ["Depósito"] = 3,
            ["Deposito"] = 3, // tolerar sin tilde
            ["Oficina"] = 4,
        };

        private record FilaUnidad(string Codigo, int TipoUnidad, decimal Area, string? Cabeza);
        private record UnidadResuelta(Guid IdUnit, int TipoUnidad, decimal Area);

        public MigrationImportService(
            IBuildingService buildingService,
            IOwnerService ownerService,
            ParameterService parameterService,
            IServiceReadingService serviceReadingService,
            IBudgetService budgetService,
            ICategoryService categoryService,
            IInstallmentService installmentService,
            IBankAccountService bankAccountService,
            AuthService authService,
            IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            _buildingService = buildingService;
            _ownerService = ownerService;
            _parameterService = parameterService;
            _serviceReadingService = serviceReadingService;
            _budgetService = budgetService;
            _categoryService = categoryService;
            _installmentService = installmentService;
            _bankAccountService = bankAccountService;
            _authService = authService;
            _ec = new BDLayout(contextFactory);
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "migracion";
        }

        // BDLayout.ExecuteWithErrorHandlingAsync envuelve cualquier falla real de SQL
        // en un RepositoryException genérico ("Operation X failed") -- el mensaje útil
        // está en .InnerException.
        private static string MensajeErrorReal(Exception ex)
        {
            var interno = ex;
            while (interno.InnerException != null)
                interno = interno.InnerException;
            return interno.Message;
        }

        // "AAAA-MM" (lo que trae la plantilla) o una fecha real si Excel la
        // reformateó -- siempre se normaliza al día 1 del mes.
        private static bool TryParsePeriodo(IXLCell cell, out DateTime periodo)
        {
            if (!cell.IsEmpty())
            {
                try
                {
                    var fecha = cell.GetDateTime();
                    periodo = new DateTime(fecha.Year, fecha.Month, 1);
                    return true;
                }
                catch { /* no es una fecha real de Excel -- probar como texto */ }
            }

            var texto = cell.GetString().Trim();
            if (System.DateTime.TryParseExact(texto, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var exacto))
            {
                periodo = new DateTime(exacto.Year, exacto.Month, 1);
                return true;
            }
            if (DateTime.TryParse(texto, out var libre))
            {
                periodo = new DateTime(libre.Year, libre.Month, 1);
                return true;
            }

            periodo = default;
            return false;
        }

        public async Task<MigrationImportResult> ImportarUnidadesYPropietariosAsync(Guid idBuilding, Stream archivo)
        {
            var resultado = new MigrationImportResult();

            List<Models.RealEstateUnit> unidadesExistentes;
            try
            {
                unidadesExistentes = await _buildingService.GetUnitsByBuildingAsync(idBuilding);
            }
            catch (Exception)
            {
                unidadesExistentes = new List<Models.RealEstateUnit>();
            }

            // Código -> unidad ya resuelta (existente en BD). Se completa más abajo con
            // las que se crean en esta misma corrida, para que "Propietarios" pueda
            // referenciar tanto unidades nuevas como ya existentes.
            var codigoToUnidad = unidadesExistentes
                .Where(u => !string.IsNullOrWhiteSpace(u.UnitNumber))
                .GroupBy(u => u.UnitNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new UnidadResuelta(g.First().IdUnit, g.First().TypeUnit, g.First().Area), StringComparer.OrdinalIgnoreCase);

            var codigosExistentes = new HashSet<string>(codigoToUnidad.Keys, StringComparer.OrdinalIgnoreCase);
            var cabezasExistentes = new HashSet<string>(
                unidadesExistentes.Where(u => u.TypeUnit == 1 || u.TypeUnit == 4).Select(u => u.UnitNumber),
                StringComparer.OrdinalIgnoreCase);

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(archivo);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo abrir el archivo -- ¿es un .xlsx válido? ({ex.Message})");
                return resultado;
            }
            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet("Unidades", out var wsUnidades))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Unidades' -- ¿es la plantilla correcta?");
                    return resultado;
                }
                if (!workbook.Worksheets.TryGetWorksheet("Propietarios", out var wsPropietarios))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Propietarios' -- ¿es la plantilla correcta?");
                    return resultado;
                }

                // ---------------- Hoja Unidades ----------------
                var filasUnidad = new List<FilaUnidad>();
                foreach (var row in wsUnidades.RowsUsed().Skip(1))
                {
                    var codigo = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(codigo)) continue;

                    var tipoTexto = row.Cell(2).GetString().Trim();
                    if (!MapaTipoUnidad.TryGetValue(tipoTexto, out var tipoUnidad))
                    {
                        resultado.Errores.Add($"Unidades, fila {row.RowNumber()}: Tipo '{tipoTexto}' no reconocido -- use Departamento, Oficina, Estacionamiento o Depósito.");
                        continue;
                    }

                    decimal area = 0;
                    if (!row.Cell(3).IsEmpty() && !decimal.TryParse(row.Cell(3).GetString().Trim(), out area))
                        resultado.Advertencias.Add($"Unidades, fila {row.RowNumber()}: 'Área' no es un número válido, se guardó como 0.");

                    var cabeza = row.Cell(4).GetString().Trim();
                    var esCabeza = string.IsNullOrWhiteSpace(cabeza);

                    if (codigosExistentes.Contains(codigo) || filasUnidad.Any(f => f.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)))
                    {
                        resultado.Advertencias.Add($"Unidades, fila {row.RowNumber()}: la unidad '{codigo}' ya existe -- se omitió esta fila.");
                        resultado.UnidadesOmitidas++;
                        continue;
                    }

                    if (esCabeza && tipoUnidad != 1 && tipoUnidad != 4)
                    {
                        resultado.Errores.Add($"Unidades, fila {row.RowNumber()}: '{codigo}' no tiene 'Unidad Cabeza de Grupo' pero su Tipo ({tipoTexto}) no puede ser cabeza -- solo Departamento u Oficina pueden serlo.");
                        continue;
                    }
                    if (!esCabeza && (tipoUnidad == 1 || tipoUnidad == 4))
                    {
                        resultado.Errores.Add($"Unidades, fila {row.RowNumber()}: '{codigo}' es {tipoTexto} pero tiene 'Unidad Cabeza de Grupo' -- un Departamento/Oficina no puede pertenecer a otra unidad.");
                        continue;
                    }

                    filasUnidad.Add(new FilaUnidad(codigo, tipoUnidad, area, esCabeza ? null : cabeza));
                }

                foreach (var f in filasUnidad.Where(f => f.Cabeza != null))
                {
                    var existeEnArchivo = filasUnidad.Any(h => h.Cabeza == null && h.Codigo.Equals(f.Cabeza, StringComparison.OrdinalIgnoreCase));
                    var existeEnBD = cabezasExistentes.Contains(f.Cabeza!);
                    if (!existeEnArchivo && !existeEnBD)
                        resultado.Errores.Add($"Unidades: '{f.Codigo}' referencia la cabeza de grupo '{f.Cabeza}', que no existe ni en este archivo ni ya registrada en el edificio.");
                }

                if (resultado.Errores.Count > 0)
                    return resultado; // ninguna escritura si hay errores de validación

                // ---------------- Crear unidades ----------------
                foreach (var f in filasUnidad)
                {
                    var unidad = new Models.RealEstateUnit
                    {
                        UnitNumber = f.Codigo,
                        TypeUnit = f.TipoUnidad,
                        Area = f.Area,
                        Number = int.TryParse(f.Codigo, out var n) ? n : 0,
                        IsAvailable = true,
                        IdBuilding = idBuilding
                    };
                    try
                    {
                        await _ec.AddNewRecordAsync(unidad);
                    }
                    catch (Exception ex)
                    {
                        resultado.Errores.Add($"Unidades: no se pudo crear '{f.Codigo}' -- {MensajeErrorReal(ex)}");
                        continue;
                    }
                    codigoToUnidad[f.Codigo] = new UnidadResuelta(unidad.IdUnit, unidad.TypeUnit, unidad.Area);
                    resultado.UnidadesCreadas++;
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                // ---------------- Hoja Propietarios ----------------
                await _parameterService.LoadParametersAsync(idBuilding);
                var cabezasUsadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in wsPropietarios.RowsUsed().Skip(1))
                {
                    var cabeza = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(cabeza)) continue;

                    if (!codigoToUnidad.ContainsKey(cabeza))
                    {
                        resultado.Errores.Add($"Propietarios, fila {row.RowNumber()}: la unidad cabeza '{cabeza}' no existe ni en este archivo ni en el edificio.");
                        continue;
                    }
                    if (!cabezasUsadas.Add(cabeza))
                    {
                        resultado.Advertencias.Add($"Propietarios, fila {row.RowNumber()}: ya se cargó un propietario para '{cabeza}' en este archivo -- se omitió (agregue copropietarios desde la pantalla de Propietarios).");
                        continue;
                    }

                    var nombres = row.Cell(3).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(nombres))
                    {
                        resultado.Errores.Add($"Propietarios, fila {row.RowNumber()}: falta 'Nombres / Razón Social'.");
                        continue;
                    }

                    var tipoPropText = row.Cell(2).GetString().Trim();
                    var tipoPropietario = tipoPropText.Equals("Persona Jurídica", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

                    var owner = new Models.Owner
                    {
                        Names = nombres,
                        Surname = row.Cell(4).GetString().Trim(),
                        TypeOwner = tipoPropietario,
                        IdTypeIdNumber = ResolverTipoDocumento(row.Cell(5).GetString().Trim()),
                        IdNumber = row.Cell(6).GetString().Trim(),
                        Address = row.Cell(7).GetString().Trim(),
                        PhoneNumber = row.Cell(8).GetString().Trim(),
                        Email = row.Cell(9).GetString().Trim(),
                        IdBuilding = idBuilding,
                        IsActive = true
                    };

                    try
                    {
                        await _ec.AddNewRecordAsync(owner);
                    }
                    catch (Exception ex)
                    {
                        resultado.Errores.Add($"Propietarios, fila {row.RowNumber()}: no se pudo crear el propietario '{owner.Names}' -- {MensajeErrorReal(ex)}");
                        continue;
                    }
                    resultado.PropietariosCreados++;

                    // Unidades del grupo: la cabeza (ya resuelta, nueva o existente) + los
                    // agregados de ESTE archivo que la referencian -- un agregado que ya
                    // existía de antes sin grupo no se reasigna acá (limitación conocida,
                    // reingréselo también en 'Unidades' si es el caso).
                    var cabezaResuelta = codigoToUnidad[cabeza];
                    var agregados = filasUnidad.Where(f => f.Cabeza != null && f.Cabeza.Equals(cabeza, StringComparison.OrdinalIgnoreCase)).ToList();
                    var areaTotal = cabezaResuelta.Area + agregados.Sum(a => a.Area);

                    var idGroupOwner = Guid.NewGuid();
                    var ownerUnit = new Models.OwnerUnit
                    {
                        IdGroupOwner = idGroupOwner,
                        IdOwner = owner.IdOwner,
                        GroupName = (cabezaResuelta.TipoUnidad == 4 ? "OFICINA " : "DPTO ") + cabeza,
                        GroupNumber = int.TryParse(cabeza, out var gn) ? gn : 0,
                        TypeOwner = 1, // Titular
                        AreaTotal = areaTotal
                    };
                    try
                    {
                        await _ec.AddNewRecordAsync(ownerUnit);

                        await _ec.AddNewRecordAsync(new Models.GroupUnit
                        {
                            IdUnit = cabezaResuelta.IdUnit,
                            IdGroupOwner = idGroupOwner,
                            TypeGroupUnit = Models.GroupUnitType.Individual
                        });

                        foreach (var agregado in agregados)
                        {
                            var idUnit = codigoToUnidad[agregado.Codigo].IdUnit;
                            await _ec.AddNewRecordAsync(new Models.GroupUnit
                            {
                                IdUnit = idUnit,
                                IdGroupOwner = idGroupOwner,
                                TypeGroupUnit = Models.GroupUnitType.Shared
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        resultado.Errores.Add($"Propietarios, fila {row.RowNumber()}: '{owner.Names}' se creó, pero no se pudo armar su grupo de unidades ('{cabeza}') -- {MensajeErrorReal(ex)}");
                    }
                }
            }

            return resultado;
        }

        // Importador de la plantilla "Lecturas de Agua Históricas". Un ServiceReading
        // por periodo distinto del archivo, con un ServiceReadingDetail por unidad
        // dentro de ese periodo.
        //
        // A diferencia de Unidades/Owner/RealEstateUnit, ServiceReading y
        // ServiceReadingDetail NO auto-generan su Guid en BDLayout.AddNewRecordAsync
        // -- hay que asignarlo acá antes de llamar al servicio (ver
        // BDLayout.Add.cs:550-607, INS_ServiceReading/INS_ServiceReadingDetail toman
        // el Id tal cual viene).
        //
        // 'IdGroupUnit' de cada detalle es el MISMO Guid que usa Installment.IdGroupUnit
        // (confirmado en InstallmentExportService, que cruza ambos por ese campo) --
        // no es RealEstateUnit.IdUnit, es el IdGroupOwner de la unidad (nace al
        // asignarle un propietario, ver ImportarUnidadesYPropietariosAsync). Por eso
        // una unidad sin propietario/grupo asignado no puede recibir una lectura
        // todavía -- se resuelve vía GetUnitsByBuildingAsync, que ya devuelve ese
        // campo denormalizado (Guid.Empty para las unidades libres, confirmado porque
        // AssignUnits.razor usa el mismo llamado para listar "unidades libres" sin
        // reventar).
        //
        // CalculatedAmount se guarda en 0 -- no se recalcula la tarifa de agua
        // histórica (bandas de consumo, cargo fijo, IGV) para un import de lecturas
        // puras; el monto ya facturado de cada periodo, si se conoce, se carga aparte
        // como Extraordinaria "Reg. Agua" en la plantilla de Cuotas y Pagos.
        //
        // ServiceReading.IdPeriod SÍ es obligatorio (Guid no nullable, con FK real
        // "FK_Readings_Periods_Services" hacia dbo.Periods) -- confirmado en
        // producción: dejarlo en Guid.Empty tumbaba el INSERT en todos los periodos.
        // Por cada mes calendario del archivo se reutiliza el Period existente que ya
        // cubra ese mes (cualquiera sea su granularidad -- mensual, bimestral, etc,
        // ver idPeriodPorMes más abajo) o, si ninguno lo cubre, se crea uno mensual
        // nuevo (Status = Cerrado, IsCurrentPeriod = false -- un mes histórico nunca
        // debe tocar el periodo vigente real del edificio). Se crea con BDLayout
        // directo (no IPeriodService.CreatePeriodAsync, que atrapa cualquier error
        // -- incluida la superposición real -- y solo devuelve false, sin mensaje).
        //
        // Idempotente por mes: si el edificio ya tiene un ServiceReading para un
        // periodo (de una corrida anterior de este mismo archivo, o cargado a mano
        // desde la UI), esa fila se omite con una Advertencia en vez de reintentarse
        // -- no existe ninguna función de borrado de lecturas en la app hoy (no hay
        // DeleteServiceReadingAsync ni un DEL_ServiceReading), así que sin este chequeo
        // reimportar el mismo archivo completo después de corregir un dato puntual
        // duplicaría todos los periodos que ya se habían guardado bien.
        public async Task<MigrationImportResult> ImportarLecturasAguaAsync(Guid idBuilding, Stream archivo)
        {
            var resultado = new MigrationImportResult();

            List<Models.RealEstateUnit> unidadesConGrupo;
            try
            {
                unidadesConGrupo = await _buildingService.GetUnitsByBuildingAsync(idBuilding);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo obtener las unidades del edificio: {ex.Message}");
                return resultado;
            }

            var idGroupUnitPorCodigo = unidadesConGrupo
                .Where(u => !string.IsNullOrWhiteSpace(u.UnitNumber))
                .GroupBy(u => u.UnitNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().IdGroupOwner, StringComparer.OrdinalIgnoreCase);

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(archivo);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo abrir el archivo -- ¿es un .xlsx válido? ({ex.Message})");
                return resultado;
            }
            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet("Lecturas", out var ws))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Lecturas' -- ¿es la plantilla correcta?");
                    return resultado;
                }

                var filas = new List<(string Codigo, DateTime Periodo, decimal Lectura, decimal? LecturaInicial, DateTime FechaLectura)>();
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var codigo = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(codigo)) continue;

                    if (!TryParsePeriodo(row.Cell(2), out var periodo))
                    {
                        resultado.Errores.Add($"Lecturas, fila {row.RowNumber()}: 'Periodo' inválido -- use AAAA-MM.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(3).GetString().Trim(), out var lectura))
                    {
                        resultado.Errores.Add($"Lecturas, fila {row.RowNumber()}: 'Lectura' no es un número válido.");
                        continue;
                    }

                    decimal? lecturaInicial = null;
                    if (!row.Cell(4).IsEmpty())
                    {
                        if (decimal.TryParse(row.Cell(4).GetString().Trim(), out var li))
                            lecturaInicial = li;
                        else
                        {
                            resultado.Errores.Add($"Lecturas, fila {row.RowNumber()}: 'Lectura Inicial' no es un número válido.");
                            continue;
                        }
                    }

                    DateTime fechaLectura;
                    if (row.Cell(5).IsEmpty() || !DateTime.TryParse(row.Cell(5).GetString().Trim(), out fechaLectura))
                        fechaLectura = new DateTime(periodo.Year, periodo.Month, DateTime.DaysInMonth(periodo.Year, periodo.Month));

                    if (!idGroupUnitPorCodigo.TryGetValue(codigo, out var idGroupUnit) || idGroupUnit == Guid.Empty)
                    {
                        resultado.Errores.Add($"Lecturas, fila {row.RowNumber()}: la unidad '{codigo}' no existe o no tiene propietario/grupo asignado -- cárguela primero en 'Unidades y Propietarios'.");
                        continue;
                    }

                    filas.Add((codigo, periodo, lectura, lecturaInicial, fechaLectura));
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                // Cada unidad necesita 'Lectura Inicial' en su periodo más antiguo DEL
                // ARCHIVO -- no hay forma de traer "la última lectura ya guardada en BD
                // antes de este import" sin una consulta dedicada que hoy no existe
                // (limitación conocida: si el edificio ya tenía lecturas cargadas antes,
                // este import debe empezar justo donde se quedaron, con Lectura Inicial
                // en el primer periodo nuevo).
                var primerPeriodoPorUnidad = filas
                    .GroupBy(f => f.Codigo, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.OrderBy(f => f.Periodo).First(), StringComparer.OrdinalIgnoreCase);

                foreach (var (codigo, primera) in primerPeriodoPorUnidad)
                {
                    if (primera.LecturaInicial == null)
                        resultado.Errores.Add($"Lecturas: la unidad '{codigo}' no tiene 'Lectura Inicial' en su primer periodo ({primera.Periodo:yyyy-MM}) -- es obligatoria para calcular el primer consumo.");
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                var lecturaAnteriorPorUnidad = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

                // Mapea cada mes calendario (Año, Mes) al Period que ya lo cubre --
                // "cubre" en el sentido de StartDate/EndDate, sin asumir que todo Period
                // es mensual (uno Bimestral/Anual cubre varios meses a la vez, y ese mismo
                // IdPeriod es el correcto para todos ellos).
                List<Models.Period> periodosExistentes;
                try
                {
                    periodosExistentes = await _ec.GetPeriodsByBuildingAsync(idBuilding);
                }
                catch (Exception)
                {
                    periodosExistentes = new List<Models.Period>();
                }

                var idPeriodPorMes = new Dictionary<(int Year, int Month), Guid>();
                foreach (var p in periodosExistentes)
                {
                    for (var mes = new DateTime(p.StartDate.Year, p.StartDate.Month, 1); mes <= p.EndDate; mes = mes.AddMonths(1))
                        idPeriodPorMes.TryAdd((mes.Year, mes.Month), p.IdPeriod);
                }

                // Idempotencia: si ya existe un ServiceReading de este edificio para un mes
                // (de una corrida anterior de este mismo importador, o cargado a mano desde
                // la UI), NO se vuelve a crear -- no hay forma de borrar lecturas desde la
                // app hoy (no existe DeleteServiceReadingAsync ni un DEL_ServiceReading), así
                // que reimportar el mismo archivo completo después de corregir un dato
                // puntual en el Excel duplicaría todos los periodos ya cargados si no se
                // omiten acá.
                List<Models.ServiceReading> lecturasExistentes;
                try
                {
                    lecturasExistentes = await _ec.GetServiceReadingListAsync(idBuilding);
                }
                catch (Exception)
                {
                    lecturasExistentes = new List<Models.ServiceReading>();
                }
                var mesesYaImportados = new HashSet<(int Year, int Month)>(lecturasExistentes.Select(sr => (sr.Period.Year, sr.Period.Month)));

                foreach (var grupoPeriodo in filas.GroupBy(f => f.Periodo).OrderBy(g => g.Key))
                {
                    var periodo = grupoPeriodo.Key;
                    var idServiceReading = Guid.NewGuid();
                    var detalles = new List<Models.ServiceReadingDetail>();
                    var claveMes = (periodo.Year, periodo.Month);
                    var yaImportado = mesesYaImportados.Contains(claveMes);

                    var idPeriod = Guid.Empty;
                    if (!yaImportado && !idPeriodPorMes.TryGetValue(claveMes, out idPeriod))
                    {
                        var finDeMes = new DateTime(periodo.Year, periodo.Month, DateTime.DaysInMonth(periodo.Year, periodo.Month));
                        var nuevoPeriodo = new Models.Period
                        {
                            IdPeriod = Guid.NewGuid(),
                            IdBuilding = idBuilding,
                            Name = periodo.ToString("MMMM-yy", System.Globalization.CultureInfo.InvariantCulture),
                            PeriodType = 1, // Mensual
                            StartDate = periodo,
                            EndDate = finDeMes,
                            ClosingDate = finDeMes.AddDays(15),
                            Status = 2, // Cerrado -- periodo histórico, no el vigente del edificio
                            IsCurrentPeriod = false,
                            Description = "Periodo histórico creado por la migración de Lecturas de Agua"
                        };
                        try
                        {
                            await _ec.AddNewRecordAsync(nuevoPeriodo);
                            await _ec.StampAuditAsync(Models.AuditableEntity.Period, nuevoPeriodo.IdPeriod, await GetPerformedByAsync(), isCreate: true);
                        }
                        catch (Exception ex)
                        {
                            resultado.Errores.Add($"Lecturas, periodo {periodo:yyyy-MM}: no se pudo crear el Periodo histórico -- {MensajeErrorReal(ex)}");
                            continue;
                        }
                        idPeriod = nuevoPeriodo.IdPeriod;
                        idPeriodPorMes[claveMes] = idPeriod;
                    }

                    foreach (var f in grupoPeriodo.OrderBy(f => f.Codigo))
                    {
                        var anterior = f.LecturaInicial ?? lecturaAnteriorPorUnidad[f.Codigo];
                        if (!yaImportado && f.Lectura < anterior)
                            resultado.Advertencias.Add($"Lecturas: '{f.Codigo}' en {periodo:yyyy-MM} tiene Lectura ({f.Lectura}) menor que la anterior ({anterior}) -- se guardó el consumo como 0.");
                        var consumo = Math.Max(0, f.Lectura - anterior);

                        detalles.Add(new Models.ServiceReadingDetail
                        {
                            IdServiceReadingDetail = Guid.NewGuid(),
                            IdGroupUnit = idGroupUnitPorCodigo[f.Codigo],
                            GroupNumber = int.TryParse(f.Codigo, out var gn) ? gn : 0,
                            Code = $"{f.Codigo}{periodo:MMyyyy}",
                            PreviousReading = (double)anterior,
                            CurrentReading = (double)f.Lectura,
                            Consumption = (double)consumo,
                            ReadingDate = f.FechaLectura,
                            CalculatedAmount = 0,
                            Minimum = false,
                            IdServiceReading = idServiceReading,
                            Period = periodo
                        });

                        lecturaAnteriorPorUnidad[f.Codigo] = f.Lectura;
                    }

                    if (yaImportado)
                    {
                        resultado.Advertencias.Add($"Lecturas, periodo {periodo:yyyy-MM}: ya existe una lectura cargada para este edificio en ese periodo -- se omitió (no hay forma de borrar lecturas desde la app todavía; si quiere reemplazarla, pida que se borre a mano en la base de datos).");
                        continue;
                    }

                    try
                    {
                        await _ec.AddNewRecordAsync(new Models.ServiceReading
                        {
                            IdServiceReading = idServiceReading,
                            Period = periodo,
                            Status = 1,
                            IdBuilding = idBuilding,
                            FileName = "Migración histórica",
                            IdPeriod = idPeriod
                        });
                        await _ec.AddNewRecordAsync(detalles);
                    }
                    catch (Exception ex)
                    {
                        resultado.Errores.Add($"Lecturas, periodo {periodo:yyyy-MM}: no se pudo guardar -- {MensajeErrorReal(ex)}");
                        continue;
                    }

                    resultado.LecturasCreadas += detalles.Count;
                }
            }

            return resultado;
        }

        // Importador de la plantilla "Presupuesto Histórico por Periodo". Un
        // BudgetHeader por periodo distinto del archivo (CreatePresupuestoAsync, sin
        // efectos secundarios) con un BudgetDetail 'header' de sección por cada
        // categoría usada y un BudgetDetail de línea por cada fila -- mismo criterio
        // de IsHeader/IdSection que usa InstallmentExportService.GetSections() para
        // imprimir el recibo por secciones.
        //
        // A propósito NO se usa IBudgetService.SaveBudgetAsync: ese método también
        // genera Installments reales con las reglas y propietarios VIGENTES HOY,
        // algo incorrecto para un presupuesto de un periodo de hace años (duplicaría
        // lo que ya carga la plantilla de Cuotas y Pagos, con datos del propietario
        // equivocados).
        //
        // 'Type' de cada línea queda en 1 (Por Unidad) por defecto -- la plantilla no
        // captura el tipo de distribución real de cada ítem histórico.
        public async Task<MigrationImportResult> ImportarPresupuestoHistoricoAsync(Guid idBuilding, Stream archivo)
        {
            var resultado = new MigrationImportResult();

            List<Models.Category> categorias;
            try
            {
                categorias = await _categoryService.GetCategoriesAsync(idBuilding);
            }
            catch (Exception)
            {
                categorias = new List<Models.Category>();
            }

            var categoriaPorNombre = categorias
                .Where(c => c.Nivel == 0)
                .GroupBy(c => c.Description, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(archivo);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo abrir el archivo -- ¿es un .xlsx válido? ({ex.Message})");
                return resultado;
            }
            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet("Presupuesto", out var ws))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Presupuesto' -- ¿es la plantilla correcta?");
                    return resultado;
                }

                var filas = new List<(DateTime Periodo, string Categoria, string Descripcion, decimal Monto)>();
                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var periodoCell = row.Cell(1);
                    if (periodoCell.IsEmpty()) continue;

                    if (!TryParsePeriodo(periodoCell, out var periodo))
                    {
                        resultado.Errores.Add($"Presupuesto, fila {row.RowNumber()}: 'Periodo' inválido -- use AAAA-MM.");
                        continue;
                    }

                    var categoriaTexto = row.Cell(2).GetString().Trim();
                    if (!categoriaPorNombre.ContainsKey(categoriaTexto))
                    {
                        resultado.Errores.Add($"Presupuesto, fila {row.RowNumber()}: la categoría '{categoriaTexto}' no existe en este edificio -- créela primero en Categorías.");
                        continue;
                    }

                    var descripcion = row.Cell(3).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(descripcion))
                    {
                        resultado.Errores.Add($"Presupuesto, fila {row.RowNumber()}: falta 'Descripción del Ítem'.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(4).GetString().Trim(), out var monto))
                    {
                        resultado.Errores.Add($"Presupuesto, fila {row.RowNumber()}: 'Monto Mensual' no es un número válido.");
                        continue;
                    }

                    filas.Add((periodo, categoriaTexto, descripcion, monto));
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                foreach (var grupoPeriodo in filas.GroupBy(f => f.Periodo).OrderBy(g => g.Key))
                {
                    var periodo = grupoPeriodo.Key;
                    var idBudgetHeader = Guid.NewGuid();
                    var totalMensual = grupoPeriodo.Sum(f => f.Monto);

                    await _budgetService.CreatePresupuestoAsync(new Models.BudgetHeader
                    {
                        IdBudgetHeader = idBudgetHeader,
                        BudgetName = $"Presupuesto histórico {periodo:MMMM yyyy}",
                        BudgetDate = periodo,
                        Amount = totalMensual,
                        AnnualAmount = totalMensual * 12,
                        BudgetType = "Histórico",
                        IdBuilding = idBuilding,
                        CreatedBy = "Migración",
                        Status = Models.BudgetStatus.Closed
                    });
                    resultado.PresupuestosCreados++;

                    var siguienteIdSection = 1;
                    foreach (var grupoCategoria in grupoPeriodo.GroupBy(f => f.Categoria, StringComparer.OrdinalIgnoreCase))
                    {
                        var categoria = categoriaPorNombre[grupoCategoria.Key];
                        var idSection = siguienteIdSection++;

                        await _budgetService.AddDetalleToPresupuestoAsync(new Models.BudgetDetail
                        {
                            IdBudgetDetail = Guid.NewGuid(),
                            IdCategory = categoria.IdCategory,
                            IdSection = idSection,
                            ItemNumber = idSection,
                            Description = categoria.Description,
                            MonthlyAmount = 0,
                            AnnualAmount = 0,
                            Frequency = 0,
                            Type = 0,
                            IsHeader = true,
                            IdBudgetHeader = idBudgetHeader,
                            IdParent = Guid.Empty
                        });

                        var secuencia = 1;
                        foreach (var f in grupoCategoria)
                        {
                            await _budgetService.AddDetalleToPresupuestoAsync(new Models.BudgetDetail
                            {
                                IdBudgetDetail = Guid.NewGuid(),
                                IdCategory = categoria.IdCategory,
                                IdSection = idSection,
                                ItemNumber = idSection + secuencia * 0.01m,
                                Description = f.Descripcion,
                                MonthlyAmount = f.Monto,
                                AnnualAmount = f.Monto * 12,
                                Frequency = 1,
                                Type = 1,
                                IsHeader = false,
                                IdBudgetHeader = idBudgetHeader,
                                IdParent = Guid.Empty
                            });
                            resultado.ItemsPresupuestoCreados++;
                            secuencia++;
                        }
                    }
                }
            }

            return resultado;
        }

        // Importador de la plantilla "Cuotas y Pagos Históricos". Agrupa filas por
        // (Periodo, Unidad, Tipo, Concepto): cada grupo es UNA Installment; si dentro
        // del grupo hay más de una fila con pago, cada una se guarda como un
        // InstallmentPaid separado contra esa misma cuota (fraccionado, tal como lo
        // describe la hoja de Instrucciones de la plantilla).
        //
        // IdBudgetHeader: si ya existe un presupuesto para ese Periodo en este
        // edificio (cargado por ImportarPresupuestoHistoricoAsync, o ya existente
        // desde la app) se reutiliza; si no, se crea uno sintético mínimo ("Cuota
        // histórica migrada", sin desglose de BudgetDetail) -- Installment exige un
        // IdBudgetHeader válido y no tiene sentido bloquear la carga de cuotas por
        // esto.
        //
        // Percent/TotalArea se aproximan con el Área ya cargada de cada unidad
        // (GetUnitsByBuildingAsync) contra la suma de áreas de TODAS las unidades con
        // grupo del edificio -- no hay un método que traiga Building.TotalArea desde
        // acá, así que esto es una aproximación, no el mismo cálculo exacto que usa
        // el generador de presupuestos en vivo.
        //
        // 'Cuenta Bancaria del Pago' y 'Referencia de Pago' de la plantilla NO se usan
        // todavía -- conciliar cada pago contra un movimiento real de
        // ImportarEstadoDeCuentaAsync queda pendiente (ver
        // Docs/Pendientes-Negocio-Migracion.md); todo pago migrado queda con
        // IdTransaction = Guid.Empty, sin bloquear el registro del pago en sí.
        public async Task<MigrationImportResult> ImportarCuotasYPagosAsync(Guid idBuilding, Stream archivo)
        {
            var resultado = new MigrationImportResult();
            var performedBy = await GetPerformedByAsync();

            List<Models.RealEstateUnit> unidadesConGrupo;
            try
            {
                unidadesConGrupo = await _buildingService.GetUnitsByBuildingAsync(idBuilding);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo obtener las unidades del edificio: {ex.Message}");
                return resultado;
            }

            var unidadPorCodigo = unidadesConGrupo
                .Where(u => !string.IsNullOrWhiteSpace(u.UnitNumber))
                .GroupBy(u => u.UnitNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            var areaTotalEdificio = unidadPorCodigo.Values.Sum(u => u.AreaTotal);

            List<Models.BudgetHeader> presupuestosExistentes;
            try
            {
                presupuestosExistentes = await _budgetService.GetPresupuestosAsync(idBuilding);
            }
            catch (Exception)
            {
                presupuestosExistentes = new List<Models.BudgetHeader>();
            }

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(archivo);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo abrir el archivo -- ¿es un .xlsx válido? ({ex.Message})");
                return resultado;
            }
            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet("Cuotas", out var ws))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Cuotas' -- ¿es la plantilla correcta?");
                    return resultado;
                }

                var filas = new List<(DateTime Periodo, string Unidad, int Tipo, string Concepto, decimal Monto,
                    DateTime Vencimiento, decimal MontoPagado, DateTime? FechaPago)>();

                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    if (!TryParsePeriodo(row.Cell(1), out var periodo))
                    {
                        if (!row.Cell(1).IsEmpty())
                            resultado.Errores.Add($"Cuotas, fila {row.RowNumber()}: 'Periodo' inválido -- use AAAA-MM.");
                        continue;
                    }

                    var unidad = row.Cell(2).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(unidad)) continue;

                    if (!unidadPorCodigo.TryGetValue(unidad, out var unidadResuelta) || unidadResuelta.IdGroupOwner == Guid.Empty)
                    {
                        resultado.Errores.Add($"Cuotas, fila {row.RowNumber()}: la unidad '{unidad}' no existe o no tiene propietario/grupo asignado -- cárguela primero en 'Unidades y Propietarios'.");
                        continue;
                    }

                    var tipoTexto = row.Cell(3).GetString().Trim();
                    int tipo;
                    if (tipoTexto.Equals("Ordinaria", StringComparison.OrdinalIgnoreCase)) tipo = (int)Models.InstallmentType.Ordinaria;
                    else if (tipoTexto.Equals("Extraordinaria", StringComparison.OrdinalIgnoreCase)) tipo = (int)Models.InstallmentType.Extraordinaria;
                    else
                    {
                        resultado.Errores.Add($"Cuotas, fila {row.RowNumber()}: 'Tipo de Cuota' debe ser Ordinaria o Extraordinaria.");
                        continue;
                    }

                    var concepto = row.Cell(4).GetString().Trim();

                    if (!decimal.TryParse(row.Cell(5).GetString().Trim(), out var monto))
                    {
                        resultado.Errores.Add($"Cuotas, fila {row.RowNumber()}: 'Monto Cuota' no es un número válido.");
                        continue;
                    }

                    var vencimiento = !row.Cell(6).IsEmpty() && DateTime.TryParse(row.Cell(6).GetString().Trim(), out var venc)
                        ? venc
                        : new DateTime(periodo.Year, periodo.Month, DateTime.DaysInMonth(periodo.Year, periodo.Month));

                    decimal montoPagado = 0;
                    if (!row.Cell(7).IsEmpty() && !decimal.TryParse(row.Cell(7).GetString().Trim(), out montoPagado))
                    {
                        resultado.Errores.Add($"Cuotas, fila {row.RowNumber()}: 'Monto Pagado' no es un número válido.");
                        continue;
                    }

                    DateTime? fechaPago = null;
                    if (montoPagado > 0)
                    {
                        fechaPago = !row.Cell(8).IsEmpty() && DateTime.TryParse(row.Cell(8).GetString().Trim(), out var fp)
                            ? fp
                            : vencimiento;
                    }

                    filas.Add((periodo, unidad, tipo, concepto, monto, vencimiento, montoPagado, fechaPago));
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                // Cache de BudgetHeader por (Año, Mes) -- reusa uno existente o crea el
                // sintético una sola vez por periodo, aunque el periodo se repita en
                // muchas filas de Cuotas.
                var budgetHeaderPorPeriodo = new Dictionary<(int, int), Guid>();

                foreach (var grupo in filas.GroupBy(f => (f.Periodo, Unidad: f.Unidad, f.Tipo, Concepto: f.Concepto ?? "")))
                {
                    var (periodo, unidadCodigo, tipo, _) = grupo.Key;
                    var primera = grupo.First();
                    var unidadResuelta = unidadPorCodigo[unidadCodigo];

                    var claveMes = (periodo.Year, periodo.Month);
                    if (!budgetHeaderPorPeriodo.TryGetValue(claveMes, out var idBudgetHeader))
                    {
                        var existente = presupuestosExistentes.FirstOrDefault(b => b.BudgetDate.Year == periodo.Year && b.BudgetDate.Month == periodo.Month);
                        if (existente != null)
                        {
                            idBudgetHeader = existente.IdBudgetHeader;
                        }
                        else
                        {
                            var nuevo = await _budgetService.CreatePresupuestoAsync(new Models.BudgetHeader
                            {
                                BudgetName = $"Cuota histórica migrada {periodo:MMMM yyyy}",
                                BudgetDate = periodo,
                                Amount = 0,
                                AnnualAmount = 0,
                                BudgetType = "Histórico",
                                IdBuilding = idBuilding,
                                CreatedBy = performedBy,
                                Status = Models.BudgetStatus.Closed
                            });
                            idBudgetHeader = nuevo.IdBudgetHeader;
                            presupuestosExistentes.Add(nuevo);
                        }
                        budgetHeaderPorPeriodo[claveMes] = idBudgetHeader;
                    }

                    var totalPagado = grupo.Sum(f => f.MontoPagado);
                    var deuda = Math.Max(0, primera.Monto - totalPagado);
                    var status = totalPagado <= 0
                        ? Models.ConcilationType.NoConciliada
                        : (deuda > 0 ? Models.ConcilationType.Parcial : Models.ConcilationType.Conciliada);

                    var percent = areaTotalEdificio > 0 ? (unidadResuelta.AreaTotal / areaTotalEdificio) * 100 : 0;

                    var installment = new Models.Installment
                    {
                        IdInstallment = Guid.NewGuid(),
                        IdBudgetHeader = idBudgetHeader,
                        Number = 1,
                        UnitName = unidadCodigo,
                        OwnerName = $"{unidadResuelta.Names} {unidadResuelta.Surname}".Trim(),
                        CreationDate = DateTime.Now,
                        Amount = primera.Monto,
                        Percent = percent,
                        TotalArea = unidadResuelta.AreaTotal,
                        Period = periodo,
                        CreatedBy = performedBy,
                        Status = status,
                        AmountPaid = totalPagado,
                        Debt = deuda,
                        IdGroupUnit = unidadResuelta.IdGroupOwner,
                        DueDate = primera.Vencimiento,
                        Type = (Models.InstallmentType)tipo,
                        Concept = grupo.Key.Concepto,
                        SourceInstallmentId = Guid.Empty
                    };
                    await _installmentService.AddInstallmentAsync(installment);
                    resultado.CuotasCreadas++;

                    foreach (var f in grupo.Where(f => f.MontoPagado > 0))
                    {
                        var esParcial = f.MontoPagado < primera.Monto || grupo.Count(g => g.MontoPagado > 0) > 1;
                        await _installmentService.AgregarPagoAsync(new Models.InstallmentPaid
                        {
                            IdPaid = Guid.NewGuid(),
                            IdInstallment = installment.IdInstallment,
                            PaymentDate = f.FechaPago ?? f.Vencimiento,
                            IdTransaction = Guid.Empty,
                            Amount = f.MontoPagado,
                            CreatedBy = performedBy,
                            Status = esParcial ? Models.ConcilationType.Parcial : Models.ConcilationType.Conciliada,
                            IsAutoReconcile = false,
                            IsPartialPayment = esParcial
                        });
                        resultado.PagosCreados++;
                    }
                }
            }

            return resultado;
        }

        // Importador de la plantilla "Estado de Cuenta Histórico". Un
        // TransactionBankHeader ("Carga histórica -- migración") por cuenta bancaria
        // usada en el archivo, con un TransactionBankDetail por fila.
        //
        // 'Categoría' de la plantilla NO se guarda -- TransactionBankDetail no tiene
        // una columna de categoría propia (la categorización real vive en Expense,
        // conciliado aparte contra el movimiento vía la pantalla de Conciliación);
        // cargar egresos ya categorizados como Expense es un alcance más grande, fuera
        // de este importador.
        //
        // 'Es Saldo Inicial' = Sí en una fila: además de guardarse como movimiento
        // normal, actualiza BankAccount.InitialBalance de esa cuenta (con el valor
        // absoluto del Monto). Como máximo una fila por cuenta puede marcarlo -- es
        // ambiguo si hay más de una.
        public async Task<MigrationImportResult> ImportarEstadoDeCuentaAsync(Guid idBuilding, Stream archivo)
        {
            var resultado = new MigrationImportResult();

            // IBankAccountService.ObtenerCuentasBancariasAsync atrapa cualquier error con
            // un catch mudo (Console.WriteLine, sin throw) y devuelve una lista vacía --
            // mismo antipatrón ya encontrado en Unidades/Lecturas de Agua. Acá es
            // particularmente engañoso: con la lista vacía, CADA fila del archivo reporta
            // "la cuenta no existe en este edificio" aunque la cuenta sí exista, ocultando
            // la falla real (confirmado: una cuenta verificada por SQL directo en la BD
            // salió como inexistente para el importador). Se usa BDLayout directo, que sí
            // relanza con el error real.
            List<Models.BankAccount> cuentas;
            try
            {
                cuentas = await _ec.GetBankAccountsByBuildingAsync(idBuilding);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo obtener las cuentas bancarias del edificio -- {MensajeErrorReal(ex)}");
                return resultado;
            }

            var cuentaPorNumero = cuentas
                .Where(c => !string.IsNullOrWhiteSpace(c.AccountNumber))
                .GroupBy(c => c.AccountNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(archivo);
            }
            catch (Exception ex)
            {
                resultado.Errores.Add($"No se pudo abrir el archivo -- ¿es un .xlsx válido? ({ex.Message})");
                return resultado;
            }
            using (workbook)
            {
                if (!workbook.Worksheets.TryGetWorksheet("Movimientos", out var ws))
                {
                    resultado.Errores.Add("El archivo no tiene una hoja llamada 'Movimientos' -- ¿es la plantilla correcta?");
                    return resultado;
                }

                var filas = new List<(string Cuenta, DateTime Fecha, bool EsIngreso, string Descripcion, string Moneda, decimal Itf, decimal Monto, bool EsSaldoInicial)>();

                foreach (var row in ws.RowsUsed().Skip(1))
                {
                    var cuenta = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(cuenta)) continue;

                    if (!cuentaPorNumero.ContainsKey(cuenta))
                    {
                        resultado.Errores.Add($"Movimientos, fila {row.RowNumber()}: la cuenta '{cuenta}' no existe en este edificio.");
                        continue;
                    }

                    if (!DateTime.TryParse(row.Cell(2).GetString().Trim(), out var fecha))
                    {
                        resultado.Errores.Add($"Movimientos, fila {row.RowNumber()}: 'Fecha' inválida.");
                        continue;
                    }

                    var tipoTexto = row.Cell(3).GetString().Trim();
                    bool esIngreso;
                    if (tipoTexto.Equals("Ingreso", StringComparison.OrdinalIgnoreCase)) esIngreso = true;
                    else if (tipoTexto.Equals("Egreso", StringComparison.OrdinalIgnoreCase)) esIngreso = false;
                    else
                    {
                        resultado.Errores.Add($"Movimientos, fila {row.RowNumber()}: 'Tipo' debe ser Ingreso o Egreso.");
                        continue;
                    }

                    var descripcion = row.Cell(5).GetString().Trim();
                    var moneda = row.Cell(6).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(moneda)) moneda = "S/";

                    decimal itf = 0;
                    if (!row.Cell(7).IsEmpty() && !decimal.TryParse(row.Cell(7).GetString().Trim(), out itf))
                    {
                        resultado.Errores.Add($"Movimientos, fila {row.RowNumber()}: 'ITF' no es un número válido.");
                        continue;
                    }

                    if (!decimal.TryParse(row.Cell(8).GetString().Trim(), out var monto))
                    {
                        resultado.Errores.Add($"Movimientos, fila {row.RowNumber()}: 'Monto' no es un número válido.");
                        continue;
                    }

                    var esSaldoInicial = row.Cell(9).GetString().Trim().Equals("Sí", StringComparison.OrdinalIgnoreCase)
                        || row.Cell(9).GetString().Trim().Equals("Si", StringComparison.OrdinalIgnoreCase);

                    filas.Add((cuenta, fecha, esIngreso, descripcion, moneda, itf, monto, esSaldoInicial));
                }

                foreach (var grupoCuenta in filas.GroupBy(f => f.Cuenta, StringComparer.OrdinalIgnoreCase))
                {
                    var marcasSaldoInicial = grupoCuenta.Count(f => f.EsSaldoInicial);
                    if (marcasSaldoInicial > 1)
                        resultado.Errores.Add($"Movimientos: la cuenta '{grupoCuenta.Key}' tiene más de una fila marcada 'Es Saldo Inicial' -- solo puede haber una.");
                }

                if (resultado.Errores.Count > 0)
                    return resultado;

                var idUser = (await _authService.GetCurrentUserAsync())?.IdUser ?? Guid.Empty;

                foreach (var grupoCuenta in filas.GroupBy(f => f.Cuenta, StringComparer.OrdinalIgnoreCase))
                {
                    var cuentaBancaria = cuentaPorNumero[grupoCuenta.Key];
                    var idStatementHeader = Guid.NewGuid();
                    var detallesGrupo = grupoCuenta.ToList();

                    await _bankAccountService.AddTransactionBankHeaderAsync(new Models.TransactionBankHeader
                    {
                        IdStatementHeader = idStatementHeader,
                        UploadDate = DateTime.Now,
                        FileName = "Migración histórica",
                        IdUser = idUser,
                        TotalRecords = detallesGrupo.Count,
                        UploadState = 1,
                        IdBankAccount = cuentaBancaria.IdBankAccount,
                        Details = new List<Models.TransactionBankDetail>()
                    });

                    var secuencia = 1;
                    foreach (var f in detallesGrupo.OrderBy(f => f.Fecha))
                    {
                        var montoFirmado = f.EsIngreso ? Math.Abs(f.Monto) : -Math.Abs(f.Monto);

                        await _bankAccountService.AddTransactionFromEECCAsync(new Models.TransactionBankDetail
                        {
                            IdStatementDetail = Guid.NewGuid(),
                            IdBankAccount = cuentaBancaria.IdBankAccount,
                            IdStatementHeader = idStatementHeader,
                            IdParent = Guid.Empty,
                            IdGroupUnit = Guid.Empty,
                            StatementDate = f.Fecha,
                            Description = f.Descripcion,
                            ITF = f.Itf,
                            Amount = montoFirmado,
                            SequenceNumber = secuencia++,
                            Currency = f.Moneda,
                            Origen = Models.TransactionOrigen.BankAccountState,
                            ReconciliationStatus = Models.ConcilationType.NoConciliada,
                            ReconciliationDate = null,
                            AmountPaid = 0,
                            Balance = montoFirmado
                        });
                        resultado.MovimientosCreados++;
                    }

                    if (detallesGrupo.Any(f => f.EsSaldoInicial))
                    {
                        var filaSaldoInicial = detallesGrupo.First(f => f.EsSaldoInicial);
                        cuentaBancaria.InitialBalance = Math.Abs(filaSaldoInicial.Monto);
                        await _bankAccountService.UpdateBankAccount(cuentaBancaria);
                    }
                }
            }

            return resultado;
        }

        // Parameter.IdParent == (int)ParamParent.DocumentType (11) -- catálogo de
        // Tipo de Documento (DNI/RUC/CE/...). Compara por ShortDescription, sin
        // distinguir mayúsculas/tildes exactas; si no reconoce el texto, usa el
        // primer valor del catálogo en vez de fallar toda la fila por un dato
        // secundario (el Owner igual se crea con Número de Documento correcto).
        private int ResolverTipoDocumento(string texto)
        {
            var catalogo = _parameterService.ListParameters
                .Where(p => p.IdParent == (int)Models.ParamParent.DocumentType)
                .ToList();

            if (catalogo.Count == 0) return 0;

            var match = catalogo.FirstOrDefault(p => p.ShortDescription.Equals(texto, StringComparison.OrdinalIgnoreCase))
                ?? catalogo.FirstOrDefault(p => p.Description.Equals(texto, StringComparison.OrdinalIgnoreCase));

            return match?.Value ?? catalogo.First().Value;
        }
    }
}
