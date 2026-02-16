using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IServiceReadingService {
        Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period);
        Task AddServiceReadingAsync(Models.ServiceReading newservice);

        Task AddPeriodAsync(Models.Period newperiod);
        Task AddServiceReadingDetailAsync(List<Models.ServiceReadingDetail> newdetails);
    }

    public class ServiceReadingService : IServiceReadingService
    {

        public SpiderHoodContext _context = default!;
        private readonly ILogger<IBudgetService> _logger;
        private BDLayout ec { get; set; }

        public ServiceReadingService(SpiderHoodContext context, ILogger<IBudgetService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            ec = new BDLayout(context);
        }

        public async Task<List<ServiceReadingDetail>> GetServiceReadingDetailbyPeriodAsync(DateTime period) {
            return await ec.GetServiceReadingDetailbyPeriodAsync(period);
        }



        public async Task AddServiceReadingDetailAsync(List<Models.ServiceReadingDetail> newdetails)
        {
            try
            {
                await ec.AddNewRecordAsync(newdetails);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el detail: {ex.Message}");
            }
        }
        public async Task AddServiceReadingAsync(Models.ServiceReading newservice)
        {
            try
            {
                await ec.AddNewRecordAsync(newservice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el contacto: {ex.Message}");
            }
        }

        public async Task AddPeriodAsync(Models.Period newperiod)
        {
            try
            {
                await ec.AddNewRecordAsync(newperiod);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al crear el periodo: {ex.Message}");
            }
        }

    }

    public class TarifaAgua
    {
        public int Id { get; set; }
        public string Rango { get; set; } = string.Empty;
        public int Minimo { get; set; }
        public int Maximo { get; set; } // -1 para "más"
        public decimal Potable { get; set; }
        public decimal Alcantarillado { get; set; }
        public decimal Total => Potable + Alcantarillado;
        public decimal Diferencial { get; set; }
        public bool Activo { get; set; } = true;

        // Para mostrar en UI
        public string DescripcionRango
        {
            get
            {
                if (Maximo == -1)
                    return $"R{Id}: {Minimo} a más";
                return $"R{Id}: {Minimo} a {Maximo}";
            }
        }
    }

    public interface ICalculoService
    {
        Task<List<TarifaAgua>> ObtenerTarifasAsync();
        Task GuardarTarifasAsync(List<TarifaAgua> tarifas);
        Task<CalculoResultado> CalcularConsumoAsync(double consumo, decimal cargoFijo);
        Task<List<ConsumoHistorico>> ObtenerHistoricoAsync(int departamentoId);
        Task GuardarLecturaAsync(Departamento departamento);
        Task<ServiceReading> ImportarDesdeExcelAsync(MemoryStream fileStream, ServiceReading reading, List<ServiceReadingDetail> previous);
        Task<List<Models.ServiceReadingDetail>> ProcesarLecturasBloqueAsync(List<Models.ServiceReadingDetail> lecturas, decimal cargoFijo);
        Task<Models.ServiceReading> ObtenerLecturaPorPeriodoAsync(DateTime period);
        Task<List<Models.ServiceReadingDetail>> ObtenerLecturasPorPeriodoAsync(DateTime period);
        Task<List<Models.ServiceReadingDetail>> GetFirstWaterReadingDetailList(Guid IdBuilding);
        Task<List<Models.ServiceReading>> GetServiceReadingsAsync(Guid IdBuilding);

    }

    public class CalculoResultado
    {
        public decimal Subtotal { get; set; }
        public decimal CargoFijo { get; set; }
        public decimal TotalSinIGV { get; set; }
        public decimal IGV { get; set; } = 0.18m; // 18%
        public decimal TotalConIGV => TotalSinIGV * (1 + IGV);
        public List<DetalleCalculo> Detalles { get; set; } = [];
    }

    public class DetalleCalculo
    {
        public string Rango { get; set; } = string.Empty;
        public double Consumo { get; set; }
        public decimal Tarifa { get; set; }
        public decimal Monto { get; set; }
    }

    // Implementación del servicio
    public class CalculoService : ICalculoService
    {
        private List<TarifaAgua> _tarifas = [];
        private List<ConsumoHistorico> _historicos = [];
        public SpiderHoodContext _context = default!;
        private BDLayout ec { get; set; }

        public CalculoService(SpiderHoodContext context)
        {
            // Tarifas por defecto según la tabla proporcionada
            _tarifas = [
                new() { Id = 1, Rango = "R1", Minimo = 0, Maximo = 10, Potable = 1.15m, Alcantarillado = 0.52m, Diferencial = 1.67m },
                new() { Id = 2, Rango = "R2", Minimo = 10, Maximo = 25, Potable = 1.34m, Alcantarillado = 0.61m, Diferencial = 0.27m },
                new() { Id = 3, Rango = "R3", Minimo = 25, Maximo = 50, Potable = 2.96m, Alcantarillado = 1.33m, Diferencial = 2.35m },
                new() { Id = 4, Rango = "R4", Minimo = 50, Maximo = -1, Potable = 5.01m, Alcantarillado = 2.26m, Diferencial = 2.99m }
            ];

            // Datos de ejemplo
            _historicos = [
            
                new() { Id = 1, DepartamentoId = 1, Anio = 2023, Consumo = 1338.99 },
                new() { Id = 2, DepartamentoId = 1, Anio = 2022, Consumo = 14865.86 }
            ];
            _context = context;
            ec = new BDLayout(context);
        }

        public Task<List<TarifaAgua>> ObtenerTarifasAsync()
        {
            return Task.FromResult(_tarifas);
        }

        public Task GuardarTarifasAsync(List<TarifaAgua> tarifas)
        {
            _tarifas = tarifas;
            return Task.CompletedTask;
        }

        public Task<CalculoResultado> CalcularConsumoAsync(double consumo, decimal cargoFijo)
        {
            var resultado = new CalculoResultado
            {
                CargoFijo = cargoFijo
            };

            double consumoRestante = consumo;
            decimal subtotal = 0;

            foreach (var tarifa in _tarifas.OrderBy(t => t.Minimo))
            {
                if (consumoRestante <= 0) break;

                double consumoEnRango = 0;

                if (tarifa.Maximo == -1) // Rango "más"
                {
                    consumoEnRango = consumoRestante;
                }
                else
                {
                    double capacidadRango = tarifa.Maximo - tarifa.Minimo;
                    consumoEnRango = Math.Min(consumoRestante, capacidadRango);
                }

                if (consumoEnRango > 0)
                {
                    decimal monto = (decimal)consumoEnRango * tarifa.Total;
                    subtotal += monto;

                    resultado.Detalles.Add(new DetalleCalculo
                    {
                        Rango = tarifa.Rango,
                        Consumo = consumoEnRango,
                        Tarifa = tarifa.Total,
                        Monto = monto
                    });

                    consumoRestante -= consumoEnRango;
                }
            }

            resultado.Subtotal = subtotal;
            resultado.TotalSinIGV = subtotal + cargoFijo;

            return Task.FromResult(resultado);
        }

        public Task<List<ConsumoHistorico>> ObtenerHistoricoAsync(int departamentoId)
        {
            var historico = _historicos
                .Where(h => h.DepartamentoId == departamentoId)
                .OrderByDescending(h => h.Anio)
                .ToList();

            return Task.FromResult(historico);
        }

        public Task GuardarLecturaAsync(Departamento departamento)
        {
            // Guardar en histórico
            _historicos.Add(new ConsumoHistorico
            {
                Id = _historicos.Count + 1,
                DepartamentoId = departamento.Id,
                Anio = DateTime.Now.Year,
                Consumo = departamento.ConsumoActual
            });

            // Actualizar departamento
            /*var existente = _departamentos.FirstOrDefault(d => d.Id == departamento.Id);
            if (existente != null)
            {
                existente.LecturaAnterior = existente.LecturaActual;
                existente.LecturaActual = departamento.LecturaActual;
                existente.FechaRegistro = DateTime.Now;
            }
            else
            {
                departamento.Id = _departamentos.Count + 1;
                _departamentos.Add(departamento);
            }*/

            return Task.CompletedTask;
        }

        public Task<ServiceReading> ImportarDesdeExcelAsync(MemoryStream fileStream, ServiceReading reading, List<ServiceReadingDetail> previous)
        {

            //ServiceReading reading = new();

            var lecturas = new List<Models.ServiceReadingDetail>();
            List<string> errores = [];

            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);

            ServiceLoadExcel readExcel = new ServiceLoadExcel();

            ValidationResult?  readingValidation = new();

            int fila = 2; // Comienza después del encabezado
            foreach (var row in worksheet.RowsUsed().Skip(1))
            {

                readExcel.Aparment = row.Cell(1).GetValue<string>();
                readExcel.Period = row.Cell(2).GetValue<string>().Trim();
                readExcel.Reading = row.Cell(3).GetValue<string>().Trim();
                readExcel.sDateReading = row.Cell(4).GetValue<string>().Trim();
                readExcel.Procesed = true;

                // Validaciones
                WaterReadingValidator _validator = new WaterReadingValidator();

                readingValidation  = _validator.ValidateLoadExcelReading(readExcel, readingValidation);

                var prev = previous.Where(c => c.GroupNumber == readExcel.Number).FirstOrDefault();

                if (readExcel.Procesed)
                {
                    var lectura = new Models.ServiceReadingDetail
                    {

                        IdServiceReadingDetail = Guid.NewGuid(),
                        IdServiceReading = reading.IdServiceReading,
                        IdGroupUnit = prev!.IdGroupUnit,
                        GroupNumber = readExcel.Number, 
                        Code = readExcel.Aparment + reading.Period.Month + reading.Period.Year,
                        CurrentReading = readExcel.ReadingValue,
                        PreviousReading = prev!.CurrentReading,
                        //PreviousReading = prev!.PreviousReading,
                        ReadingDate = readExcel.DateReading,
                        Period = reading.Period,
                        CalculatedAmount = 0,
                        Procesed = readExcel.Procesed
                    };
                    lecturas.Add(lectura);
                }

                fila++;
            }

            foreach (var item in previous) {

                int exists = lecturas.Count(c => c.GroupNumber == item.GroupNumber);

                if (exists == 0) {
                    var lectura = new Models.ServiceReadingDetail
                    {
                        IdServiceReadingDetail = Guid.NewGuid(),
                        IdServiceReading = reading.IdServiceReading,
                        IdGroupUnit = item.IdGroupUnit,
                        GroupNumber = item.GroupNumber,
                        Code = item.GroupNumber.ToString() + reading.Period.Month + reading.Period.Year,
                        CurrentReading = 0,
                        PreviousReading = item.CurrentReading,
                        ReadingDate = reading.Period,
                        Period = reading.Period,
                        CalculatedAmount = 0
                    };
                    lecturas.Add(lectura);
                }
            }

            //return lecturas;
            reading.ValidationErrors = readingValidation!;
            reading.WaterReadingDetail = [..lecturas.OrderBy(h => h.GroupNumber)];
            return Task.FromResult(reading);
        }

        public Task<List<Models.ServiceReadingDetail>> ProcesarLecturasBloqueAsync(List<Models.ServiceReadingDetail> lecturas, decimal cargoFijo)
        {
            foreach (var lectura in lecturas.Where(c => c.Procesed))
            {

                var consumo =  Math.Max(0, lectura.CurrentReading - lectura.PreviousReading);

                if (consumo > 0) { 
                    var calculo = CalcularConsumoAsync(consumo, cargoFijo).Result;
                    lectura.CalculationDetail = calculo;
                    lectura.CalculatedAmount = calculo.TotalConIGV;
                    lectura.Consumption = consumo;
                }
                lectura.Minimum = lectura.Consumption <= 0;
                lectura.Procesed = !lectura.Minimum;
            }

            return Task.FromResult(lecturas);
        }

        public async Task<ServiceReading> ObtenerLecturaPorPeriodoAsync(DateTime period)
        {
            var lista = await ec.GetServiceReadingbyPeriodAsync(period); // Espera la tarea para obtener la lista
            return lista.FirstOrDefault()!; // Ahora sí puedes usar FirstOrDefault()
        }


        public Task<List<Models.ServiceReadingDetail>> ObtenerLecturasPorPeriodoAsync(DateTime period)
        {
            return ec.GetServiceReadingDetailbyPeriodAsync(period);
        }

        public Task<List<Models.ServiceReadingDetail>> GetFirstWaterReadingDetailList(Guid IdBuilding)
        {
            return ec.GetFirstWaterReadingDetailListAsync(IdBuilding);
        }

        public Task<List<Models.ServiceReading>> GetServiceReadingsAsync(Guid IdBuilding)
        {
            return ec.GetServiceReadingListAsync(IdBuilding);
        }
    }


    public class ServiceLoadExcel {

        public string Aparment { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public string Reading { get; set; } = string.Empty;
        public string sDateReading { get; set; } = string.Empty;

        public int row = 0;
        public bool Procesed = true;
        public DateTime DateReading => DateTime.TryParse(sDateReading, out DateTime date) ? date : DateTime.MinValue;
        public double ReadingValue => double.TryParse(Reading, out double value) ? value : 0;
        public int Number => int.TryParse(Aparment, out int num) ? num : 0;

    }

    public class WaterReadingValidator
    {
        private readonly ValidationSettings? _settings;

        public WaterReadingValidator(ValidationSettings settings)
        {
            _settings = settings;
        }

        public WaterReadingValidator()
        {
        }
        public ValidationResult ValidateFile(IBrowserFile file)
        {
            var result = new ValidationResult();

            // Validar tamaño
            if (file.Size > _settings.MaxFileSize)
            {
                result.AddError("FILE_SIZE",
                    $"El archivo excede el tamaño máximo permitido ({_settings.MaxFileSize / 1024 / 1024}MB)");
            }

            // Validar extensión
            var validExtensions = new[] { ".xlsx", ".xls" };
            var extension = Path.GetExtension(file.Name);
            if (!validExtensions.Contains(extension.ToLower()))
            {
                result.AddError("FILE_EXTENSION",
                    $"Extensión no válida. Use: {string.Join(", ", validExtensions)}");
            }

            return result;
        }

        public ValidationResult ValidateLoadExcelReading(ServiceLoadExcel reading, ValidationResult result)
        {
            //var result = new ValidationResult();

                if (!DateTime.TryParse(reading.Period, out DateTime periodo))
                {
                    //errores.Add($"Fila {.row - 1}: Periodo inválido");
                    result!.AddError("PERIODO_INVALIDO", $"Fila {reading.row - 1}: Periodo inválido");
                    reading.Procesed = false;
                }
                else if (periodo > DateTime.Today)
                {
                    //errores.Add($"Fila {fila - 1}: Fecha futura no permitida");
                    result!.AddError("PERIODO_INVALIDO", $"Fila {reading.row - 1}: Periodo con Fecha Futura");
                    reading.Procesed = false;
            }

                if (!DateTime.TryParse(reading.sDateReading , out DateTime fecha))
                {
                    result!.AddError("FECHA_LECTURA", $"Fila {reading.row - 1}: Fecha inválida");
                    reading.Procesed = false;
                    //errores.Add($"Fila {fila - 1}: Fecha inválida");
                }
                else if (fecha > DateTime.Today)
                {
                    result!.AddError("FECHA_LECTURA", $"Fila {reading.row - 1}: Fecha futura no permitida");
                    reading.Procesed = false;
                    //errores.Add($"Fila {fila - 1}: Fecha futura no permitida");
                }

                if (!int.TryParse(reading.Aparment, out int Number))
                {
                    result!.AddError("DPTO_INVALIDO", $"Fila {reading.row - 1}: Formato inválido. Debe indicar el un número de Dpto: 101, 102");
                    reading.Procesed = false;
                    //errores.Add($"Fila {fila - 1}: Formato inválido. Debe indicar el un número de Dpto: 101, 102");
                }

                if (!double.TryParse(reading.Reading, out double value))
                {
                    result!.AddError("LECTURA_INVALIDO", $"Fila {reading.row - 1}: Lectura Actual inválido (debe ser un número positivo)");
                    reading.Procesed = false;
                    //errores.Add($"Fila {fila - 1}: Lectura Actual inválido (debe ser un número positivo)");
                }

            return result!;
        }

        public ValidationResult ValidateReading(ServiceReadingDetail reading)
        {
            var result = new ValidationResult();

            if (reading.Consumption < 0)
                result.AddError("CONSUMPTION_NEGATIVE", "El consumo no puede ser negativo");

            if (reading.ReadingDate > DateTime.Now.AddDays(1))
                result.AddError("FUTURE_DATE", "La fecha de lectura no puede ser futura");

            //if (string.IsNullOrWhiteSpace(reading.ApartmentCode))
            //    result.AddError("MISSING_APARTMENT", "El código de departamento es requerido");

            return result;
        }

        public ValidationResult ValidateBeforeSave(ServiceReading reading,
            List<ServiceReadingDetail> details)
        {
            var result = new ValidationResult();

            // Validar que haya lecturas
            if (!details.Any())
                result.AddError("NO_READINGS", "No hay lecturas para guardar");

            // Validar que todas estén procesadas
            var unprocessed = details.Where(d => !d.Procesed).ToList();
            if (unprocessed.Any())
                result.AddError("UNPROCESSED_READINGS",
                    $"Hay {unprocessed.Count} lecturas sin procesar");

            // Validar totales
            var total = details.Sum(d => d.CalculatedAmount);
            if (total <= 0)
                result.AddError("INVALID_TOTAL", "El total general debe ser mayor a 0");

            // Validar período
            if (reading.Period > DateTime.Now)
                result.AddError("FUTURE_PERIOD", "El período no puede ser futuro");

            return result;
        }
    }

    // ========== CLASES DE SOPORTE ==========
    public class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<ValidationError> Errors { get; } = new();
        public Dictionary<string, object> Metadata { get; } = new();

        public void AddError(string code, string message)
        {
            Errors.Add(new ValidationError(code, message));
        }
    }

    public class ValidationError
    {
        public string Code { get; }
        public string Message { get; }

        public ValidationError(string code, string message)
        {
            Code = code;
            Message = message;
        }
    }

    public class ValidationSettings
    {
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
        public decimal MinConsumption { get; set; } = 5.0m;
        public int MaxReadingsPerFile { get; set; } = 1000;
    }

}
