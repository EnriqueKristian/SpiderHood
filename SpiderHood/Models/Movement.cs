using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{

    public class MovementHeader
    {
        public Guid IdStatementHeader { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime UploadDate { get; set; }
        public string FileName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El Usuario es obligatorio")]
        public Guid IdUser { get; set; }

        public int TotalRecords { get; set; } 

        public int UploadState { get; set; }
        public Guid IdBankAccount { get; set; }

        public required List<MovementDetail> Details { get; set; }
    }

    public class MovementDetail
    {
        public Guid IdMovHeader { get; set; }
        public Guid IdMovDetail { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime MovDate { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria")]
        [StringLength(200, ErrorMessage = "La descripción no puede exceder 200 caracteres")]
        public string Description { get; set; }= string.Empty;

        [Required(ErrorMessage = "El ITF es obligatorio")]
        [Precision(18, 2)]
        public decimal ITF { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria")]
        public required string Currency { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        [Precision(18, 2)]
        public decimal Amount { get; set; } = decimal.Zero;
        public int UploadDetState { get; set; }

        public string Validation { get; set; } = string.Empty;

        public string KeyDuplicate
        {
            get
            {
                return $"{MovDate:yyyyMMdd}|{Description}|{Amount:F2}";
            }
        }

        public decimal FinalAmount { get { return ITF + Amount; } } 
    }

    public class MovDetKey
    {
        public Guid IdStatementDetail { get; set; }
        public DateTime StatementDate { get; set; }
        public string Description { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Amount { get; set; } = decimal.Zero;

        public string clavesBD
        {
            get
            {
                return $"{StatementDate:yyyyMMdd}|{Description}|{Amount}";
            }
        }
    }
}