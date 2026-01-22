using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
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
        Task<ServiceReading> ImportarDesdeExcelAsync(MemoryStream fileStream, Guid IdBuilding, DateTime period, List<ServiceReadingDetail> previous,string filename, BDLayout ec);
        Task<List<Models.ServiceReadingDetail>> ProcesarLecturasBloqueAsync(List<Models.ServiceReadingDetail> lecturas, decimal cargoFijo);
        Task<List<Models.ServiceReadingDetail>> ObtenerLecturasPorPeriodoAsync(BDLayout ec, ServiceReading lectura);
        Task<List<Models.ServiceReadingDetail>> GetFirstWaterReadingDetailList(BDLayout ec, Guid IdBuilding);
        Task<List<Models.ServiceReading>> GetPeriodsAsync(BDLayout ec, Guid IdBuilding);
    }

    public class CalculoResultado
    {
        public decimal Subtotal { get; set; }
        public decimal CargoFijo { get; set; }
        public decimal TotalSinIGV { get; set; }
        public decimal IGV { get; set; } = 0.18m; // 18%
        public decimal TotalConIGV => TotalSinIGV * (1 + IGV);
        public List<DetalleCalculo> Detalles { get; set; } = new();
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
        private List<TarifaAgua> _tarifas = new();
        private List<ConsumoHistorico> _historicos = new();
        //private List<Departamento> _departamentos = new();

        public CalculoService()
        {
            // Tarifas por defecto según la tabla proporcionada
            _tarifas = new List<TarifaAgua>
            {
                new() { Id = 1, Rango = "R1", Minimo = 0, Maximo = 10, Potable = 1.15m, Alcantarillado = 0.52m, Diferencial = 1.67m },
                new() { Id = 2, Rango = "R2", Minimo = 10, Maximo = 25, Potable = 1.34m, Alcantarillado = 0.61m, Diferencial = 0.27m },
                new() { Id = 3, Rango = "R3", Minimo = 25, Maximo = 50, Potable = 2.96m, Alcantarillado = 1.33m, Diferencial = 2.35m },
                new() { Id = 4, Rango = "R4", Minimo = 50, Maximo = -1, Potable = 5.01m, Alcantarillado = 2.26m, Diferencial = 2.99m }
            };

            // Datos de ejemplo
            _historicos = new List<ConsumoHistorico>
            {
                new() { Id = 1, DepartamentoId = 1, Anio = 2023, Consumo = 1338.99 },
                new() { Id = 2, DepartamentoId = 1, Anio = 2022, Consumo = 14865.86 }
            };
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

        public Task<ServiceReading> ImportarDesdeExcelAsync(MemoryStream fileStream, Guid IdBuilding, DateTime period, List<ServiceReadingDetail> previous, string filename,BDLayout ec)
        {

            ServiceReading reading = new ServiceReading();

            var lecturas = new List<Models.ServiceReadingDetail>();
            List<string> errores = new();

            //Cargar Cabecera de Lectura
            reading.IdWaterReading = Guid.NewGuid();
            reading.Period = period;
            reading.CreatedOn = DateTime.Now;
            reading.Status = 1;
            reading.IdBuilding = IdBuilding;
            reading.FileName = filename;

            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);

            int fila = 2; // Comienza después del encabezado
            foreach (var row in worksheet.RowsUsed().Skip(1))
            {

                var Departamento = row.Cell(1).GetValue<string>();
                var Periodo = row.Cell(2).GetValue<string>()?.Trim();
                var Lectura = row.Cell(3).GetValue<string>()?.Trim();
                var fechaStr = row.Cell(4).GetValue<string>()?.Trim();
                bool _procesed = true;

                // Validaciones

                if (!DateTime.TryParse(Periodo, out DateTime periodo)) { 
                    errores.Add($"Fila {fila-1}: Periodo inválido");
                    _procesed = false;
                }
                else if (periodo > DateTime.Today) { 
                    errores.Add($"Fila {fila-1}: Fecha futura no permitida");
                    _procesed = false;
                     }

                if (!DateTime.TryParse(fechaStr, out DateTime fecha))
                {
                    _procesed = false;
                    errores.Add($"Fila {fila-1}: Fecha inválida");
                }
                else if (fecha > DateTime.Today)
                {
                    _procesed = false; 
                    errores.Add($"Fila {fila - 1}: Fecha futura no permitida");
                }


                if (!int.TryParse(Departamento, out int Number))
                {
                    _procesed = false;
                    errores.Add($"Fila {fila - 1}: Formato inválido. Debe indicar el un número de Dpto: 101, 102");
                }


                if (!double.TryParse(Lectura, out double value))
                {
                    errores.Add($"Fila {fila - 1}: Lectura Actual inválido (debe ser un número positivo)");
                    _procesed = false;
                }
                // Si no hay errores en esta fila, agregar a la lista
                /*if (errores.Any(e => e.Contains($"Fila {fila-1}")))
                {
                    strCheck = "Error de Dato";
                }*/

                var prev = previous.Where(c => c.GroupNumber == Number).FirstOrDefault();

                if (_procesed)
                {
                    var lectura = new Models.ServiceReadingDetail
                    {

                        IdWaterReadingDetail = Guid.NewGuid(),
                        IdWaterReading = reading.IdWaterReading,
                        IdGroupUnit = prev!.IdGroupUnit,
                        GroupNumber = Number, 
                        Code = Departamento + period.Month + period.Year,
                        CurrentReading = value,
                        PreviousReading = prev!.CurrentReading,
                        ReadingDate = fecha,
                        CalculatedAmount = 0,
                        Procesed = _procesed
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
                        IdWaterReadingDetail = Guid.NewGuid(),
                        IdWaterReading = reading.IdWaterReading,
                        IdGroupUnit = item.IdGroupUnit,
                        GroupNumber = item.GroupNumber,
                        Code = item.GroupNumber.ToString() + period.Month + period.Year,
                        CurrentReading = 0,
                        PreviousReading = item.CurrentReading,
                        ReadingDate = period,
                        CalculatedAmount = 0
                    };
                    lecturas.Add(lectura);
                }
            }

            //return lecturas;
            reading.errors = errores;
            reading.WaterReadingDetail = lecturas.OrderBy(h => h.GroupNumber).ToList();
            return Task.FromResult(reading);
        }

        public Task<List<Models.ServiceReadingDetail>> ProcesarLecturasBloqueAsync(List<Models.ServiceReadingDetail> lecturas, decimal cargoFijo)
        {
            foreach (var lectura in lecturas.Where(c => c.Procesed))
            {

                var consumo = lectura.PreviousReading == 0 ?
                                Math.Max(0, lectura.CurrentReading - lectura.PreviousReading) : lectura.Consumption;

                if (consumo > 0) { 
                    var calculo = CalcularConsumoAsync(consumo, cargoFijo).Result;
                    lectura.CalculationDetail = calculo;
                    lectura.CalculatedAmount = calculo.TotalConIGV;
                    lectura.Consumption = consumo;
                }
                lectura.Minimum = lectura.Consumption <= 0 ? true : false;
                lectura.Procesed = !lectura.Minimum;
            }

            return Task.FromResult(lecturas);
        }

        public Task<List<Models.ServiceReadingDetail>> ObtenerLecturasPorPeriodoAsync(BDLayout ec, ServiceReading lectura)
        {
            return ec.GetWaterReadingDetailList(lectura);
        }

        public Task<List<Models.ServiceReadingDetail>> GetFirstWaterReadingDetailList(BDLayout ec, Guid IdBuilding)
        {
            return ec.GetFirstWaterReadingDetailList(IdBuilding);
        }

        public Task<List<Models.ServiceReading>> GetPeriodsAsync(BDLayout ec, Guid IdBuilding)
        {
            return ec.GetWaterReadingList(IdBuilding);
        }
    }
}
