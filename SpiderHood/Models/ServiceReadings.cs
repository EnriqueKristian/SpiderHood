using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Mono.TextTemplating;
using OfficeOpenXml;
using SpiderHood.Data;
using SpiderHood.Models;
using SpiderHood.Services;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Departamento
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Codigo { get; set; } = string.Empty;
        public double LecturaActual { get; set; }
        public double LecturaAnterior { get; set; }
        public double ConsumoActual => Math.Max(0, LecturaActual - LecturaAnterior);
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [Precision(18, 2)]
        public decimal AreaM2 { get; set; }
        [Precision(18, 2)]
        public decimal PorcentajeArea { get; set; }
        public bool Activo { get; set; }
    }

    public class ConsumoHistorico
    {
        public int Id { get; set; }
        public int DepartamentoId { get; set; }
        public int Anio { get; set; }
        public double Consumo { get; set; }
    }

    public class ServiceReadingDetail
    {
        public Guid IdServiceReadingDetail { get; set; }
        public Guid IdGroupUnit { get; set; }
        public int GroupNumber { get; set; }
        public string Code { get; set; } = string.Empty; // Ej: "101112025"
        public double PreviousReading { get; set; }
        public double CurrentReading { get; set; }
        public double Consumption { get; set; }
        public DateTime ReadingDate { get; set; }
        [Precision(18, 2)]
        public decimal CalculatedAmount { get; set; }
        public bool Minimum { get; set; }
        public Guid IdServiceReading { get; set; }
        public DateTime Period { get; set; }
        [NotMapped]
        public bool Procesed { get; set; } = false;
        [NotMapped]
        public CalculoResultado? CalculationDetail { get; set; }

        public ServiceReadingDetail Clone()
        {
            return new ServiceReadingDetail
            {
                IdServiceReadingDetail = this.IdServiceReadingDetail,
                IdGroupUnit = this.IdGroupUnit,
                GroupNumber = this.GroupNumber,
                Code = this.Code,
                PreviousReading = this.PreviousReading,
                CurrentReading = this.CurrentReading,
                Consumption = this.Consumption,
                ReadingDate = this.ReadingDate,
                CalculatedAmount = this.CalculatedAmount,
                Minimum = this.Minimum,
                IdServiceReading = this.IdServiceReading,
                Period = this.Period,
                Procesed = this.Procesed,
                CalculationDetail = this.CalculationDetail // Shallow copy; deep copy if needed
            };
        }
    }

    public class ServiceReading
    {
        public Guid IdServiceReading { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime Period { get; set; } // Ej: "November-25"
        public int Status { get; set; }
        public Guid IdBuilding { get; set; }
        public string FileName { get; set; } = string.Empty;
        public Guid IdPeriod { get; set; }
        public List<ServiceReadingDetail>? WaterReadingDetail { get; set; }
        [NotMapped]
        public decimal TotalAmount { get; set; }

        [NotMapped]
        public ValidationResult? ValidationErrors { get; set; } 
        [NotMapped]
        public bool HasErrors => ValidationErrors == null ? false :  ValidationErrors!.Errors.Count > 0;
    }

    public class ServiceReadingState{
        public ServiceReading CurrentReading { get; set; } = new();

        private List<ServiceReadingDetail> _currentReadingDetail = new List<ServiceReadingDetail>();
        public List<ServiceReadingDetail> CurrentReadingDetail
        {
            get => _currentReadingDetail;
            set
            {
                _currentReadingDetail = value;
                CurrentReading.WaterReadingDetail = value ?? null; // Sync with CurrentReading
            }
        }

        public List<ServiceReadingDetail> PreviousReadingDetail { get; set; } = [];

        public decimal CargoFijo { get; set; } = 0;

        public bool LoadFromExcel => !(CurrentReading.Status == 1);

        public bool ShowSaveReadings
        {
            get
            {
                try
                {
                    bool exist = _currentReadingDetail.Any(c => !c.Procesed);
                    bool existError = CurrentReading.HasErrors;
                    bool valstatus = CurrentReading.Status == 1 ? true : false;
                    return (valstatus && !exist && !existError); // error y pendiente
                }
                catch
                {
                    return false;
                }
                
            }
        }

        public DateTime Period { get; set; }

        public bool OpenFromModal
        {
            get
            {
                return !(Period == DateTime.MinValue);
            }
        }

    }
}
