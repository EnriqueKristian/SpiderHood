using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
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

    public class WaterReadingDetail
    {
        public Guid IdWaterReadingDetail { get; set; }
        public Guid IdGroupUnit { get; set; }
        public int GroupNumber { get; set; }
        public string Code { get; set; } = string.Empty; // Ej: "101112025"
        public double PreviousReading { get; set; }
        public double CurrentReading { get; set; }
        public double Consumption { get; set; }
        public DateTime ReadingDate { get; set; }
        public decimal CalculatedAmount { get; set; }
        public bool Minimum { get; set; }
        public Guid IdWaterReading { get; set; }
        public DateTime Period { get; set; }
        [NotMapped]
        public bool Procesed { get; set; } = false;
        [NotMapped]
        public CalculoResultado? CalculationDetail { get; set; }

    }

    public class WaterReading {
        public Guid IdWaterReading { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime Period { get; set; } // Ej: "November-25"
        public int Status { get; set; }
        public Guid IdBuilding{ get; set; }
        public string FileName { get; set; } = string.Empty;
        public List<WaterReadingDetail>? WaterReadingDetail { get; set; }
        [NotMapped]
        public decimal TotalAmount { get; set; }

        [NotMapped]
        public List<string> errors { get; set; } = new List<string>();
    }


    public class ExcelExportService
    {
        public async Task ExportWaterReadingsAsync(
            List<WaterReadingDetail> lecturas,
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




       












    }


}
