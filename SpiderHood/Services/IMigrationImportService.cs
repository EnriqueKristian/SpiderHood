using ClosedXML.Excel;

namespace SpiderHood.Services
{
    public class MigrationImportResult
    {
        public int UnidadesCreadas { get; set; }
        public int UnidadesOmitidas { get; set; }
        public int PropietariosCreados { get; set; }
        public List<string> Errores { get; } = new();
        public List<string> Advertencias { get; } = new();
        public bool Success => Errores.Count == 0;
    }

    // Importador de la plantilla "Unidades y Propietarios"
    // (IMigrationTemplateService.GenerarPlantillaUnidadesYPropietariosAsync). Primer
    // importador de migración -- los otros 4 (Cuotas, Estado de Cuenta, Lecturas,
    // Presupuesto) quedan pendientes, ver Docs/Pendientes-Negocio-Migracion.md.
    //
    // Reproduce a mano la misma secuencia de escritura que ya usa Owners.razor al
    // asignar un propietario (rastreada ahí, no hay documentación aparte del modelo):
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
    public interface IMigrationImportService
    {
        Task<MigrationImportResult> ImportarUnidadesYPropietariosAsync(Guid idBuilding, Stream archivo);
    }

    public class MigrationImportService : IMigrationImportService
    {
        private readonly IBuildingService _buildingService;
        private readonly IOwnerService _ownerService;
        private readonly ParameterService _parameterService;

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
            ParameterService parameterService)
        {
            _buildingService = buildingService;
            _ownerService = ownerService;
            _parameterService = parameterService;
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
                    await _buildingService.AddUnitAsync(unidad);
                    codigoToUnidad[f.Codigo] = new UnidadResuelta(unidad.IdUnit, unidad.TypeUnit, unidad.Area);
                    resultado.UnidadesCreadas++;
                }

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

                    var creado = await _ownerService.AddOwnerAsync(owner);
                    if (creado.IdOwner == Guid.Empty)
                    {
                        resultado.Errores.Add($"Propietarios, fila {row.RowNumber()}: no se pudo crear el propietario '{owner.Names}' (error al guardar).");
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
                        IdOwner = creado.IdOwner,
                        GroupName = (cabezaResuelta.TipoUnidad == 4 ? "OFICINA " : "DPTO ") + cabeza,
                        GroupNumber = int.TryParse(cabeza, out var gn) ? gn : 0,
                        TypeOwner = 1, // Titular
                        AreaTotal = areaTotal
                    };
                    await _buildingService.AddOwnerUnitAsync(ownerUnit);

                    await _buildingService.AddGroupUnitAsync(new Models.GroupUnit
                    {
                        IdUnit = cabezaResuelta.IdUnit,
                        IdGroupOwner = idGroupOwner,
                        TypeGroupUnit = Models.GroupUnitType.Individual
                    });

                    foreach (var agregado in agregados)
                    {
                        var idUnit = codigoToUnidad[agregado.Codigo].IdUnit;
                        await _buildingService.AddGroupUnitAsync(new Models.GroupUnit
                        {
                            IdUnit = idUnit,
                            IdGroupOwner = idGroupOwner,
                            TypeGroupUnit = Models.GroupUnitType.Shared
                        });
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
