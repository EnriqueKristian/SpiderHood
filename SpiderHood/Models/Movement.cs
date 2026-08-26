using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{

    public class TransactionBankHeader
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

        public required List<TransactionBankDetail> Details { get; set; }
    }

    public class TransactionBankDetail
    {
        public Guid IdStatementDetail { get; set; }
        public Guid IdBankAccount { get; set; }
        public Guid IdStatementHeader { get; set; }
        public Guid IdParent { get; set; }
        public Guid IdGroupUnit { get; set; } = Guid.Empty;

        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime StatementDate { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria")]
        [StringLength(200, ErrorMessage = "La descripción no puede exceder 200 caracteres")]
        public string Description { get; set; } = String.Empty;

        [Required(ErrorMessage = "El ITF es obligatorio")]
        [Precision(18, 2)]
        [NotMapped]
        public decimal ITF { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0")]
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public int SequenceNumber { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria")]
        public string Currency { get; set; } = string.Empty;
        [NotMapped]
        public Guid IdEntityRef { get; set; }

        public TransactionOrigen Origen { get; set; }
        public ConcilationType ReconciliationStatus { get; set; }
        public DateTime? ReconciliationDate { get; set; }

        [Precision(18, 2)]
        public decimal AmountPaid { get; set; }
        [Precision(18, 2)]
        public decimal Balance { get; set; }

        public List<ViewExpense> PosiblesMatches { get; set; } = [];
        public List<Installment> IPosiblesMatches { get; set; } = [];

        [NotMapped]
        public bool Ignored { get; set; } = false;
        [NotMapped]
        public bool Selected { get; set; } = false;
        public string Reference => (Amount < 0 ? "G" : "I") + SequenceNumber.ToString("D4");
        public string Notes => (Amount < 0 ? "Gasto #" : "Ingreso #") + SequenceNumber.ToString("D4");
        public string Tipo => Amount < 0 ? "Gasto" : "Ingreso";
        [NotMapped]
        public ViewExpense? GastoConciliado { get; set; }
        [NotMapped]
        public Installment? CuotaConciliada { get; set; }
        [NotMapped]
        public string Validation { get; set; } = string.Empty;
        public string KeyDuplicate
        {
            get
            {
                return $"{StatementDate:yyyyMMdd}|{Description}|{Amount:F2}";
            }
        }
        public decimal FinalAmount { get { return ITF + Amount; } }
    }

    // Forma liviana para listar el detalle de una carga de estado de cuenta (solo lectura).
    // A propósito NO reutiliza TransactionBankDetail: esa entidad trae columnas (Balance,
    // AmountPaid, IdGroupUnit, PosiblesMatches, etc.) que no aplican a esta vista de solo
    // lectura. ReconciliationStatus/ReconciliationDate sí se incluyen porque el usuario
    // necesita ver desde acá si cada movimiento ya fue conciliado.
    public class AccountStatementDetailView
    {
        public Guid IdStatementDetail { get; set; }
        public Guid IdStatementHeader { get; set; }
        public DateTime StatementDate { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal Amount { get; set; }
        public int SequenceNumber { get; set; }
        public ConcilationType ReconciliationStatus { get; set; }
        public DateTime? ReconciliationDate { get; set; }
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