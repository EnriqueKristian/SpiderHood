using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{

    // Nombres reales de tabla (confirmados en la BD, no coinciden con la clase C#
    // ni con los Stored Procedures que los usan -- mismo patrón que Owner/
    // ApartmentOwner y Period/Periods): TransactionBankHeader vive en
    // dbo.AccountStatementHeader (SPs: INS_MovementHeader/GET_MovementHeaders) y
    // TransactionBankDetail vive en dbo.AccountStatementDetail (SPs:
    // INS_AccountStatementDetail/GET_AccountStatementDetailByHeader). Relevante si
    // escribes SQL crudo contra estas tablas (ver Database/Scripts/2026-09-08_56_...sql).
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

        // Tipo de cambio real aplicado a TODO este lote (un estado de cuenta = una
        // carga = un valor) -- sólo tiene sentido cuando la Cuenta Bancaria es de
        // otra moneda que la de reporte del edificio; si son la misma, queda NULL.
        // Ver INS_AccountStatementDetail: se usa para calcular
        // AmountInReportingCurrency de cada fila al guardar.
        [Precision(18, 6)]
        public decimal? ExchangeRate { get; set; }

        public required List<TransactionBankDetail> Details { get; set; }
    }

    // Catálogo chico y cerrado (Docs/Pendientes-Negocio-Conciliacion.md #1) -- se guarda
    // como INT en dbo.AccountStatementDetail.IgnoredType. Un enum C# alcanza acá (no un
    // grupo más de Parameter, que además es por-edificio o requiere el mecanismo Sistema/
    // Mixto): son 3 valores fijos, iguales para todos los edificios, y el propio pendiente
    // los deja como "a evaluar casos" -- si en el futuro hace falta que cada edificio
    // agregue los suyos, ahí sí conviene migrar a Parameter.
    public enum IgnoredReasonType
    {
        ErrorBancario = 1,
        DepositoRevertido = 2,
        Otro = 3
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

        // = Amount cuando la cuenta está en la moneda de reporte del edificio (caso
        // normal, siempre). Cuando la cuenta es de otra moneda, = Amount * el
        // ExchangeRate del lote (TransactionBankHeader.ExchangeRate) -- se calcula
        // server-side en INS_AccountStatementDetail, no acá. Conciliación y Reportes
        // suman ESTA columna, nunca Amount crudo -- Amount se sigue mostrando tal
        // cual para que cuadre contra el estado de cuenta real del banco.
        [Precision(18, 2)]
        public decimal AmountInReportingCurrency { get; set; }
        public int SequenceNumber { get; set; }

        // Solo para migración de datos históricos (Services/IMigrationImportService.cs) --
        // el identificador que traía el movimiento en el sistema anterior del edificio
        // (ej. la columna 'ID' del Consolidado de Excel), NO el SequenceNumber que asigna
        // SpiderHood al cargar (ese es un contador propio, se desfasa apenas se descarta
        // una fila del archivo original, no sirve como referencia estable). Ninguna
        // pantalla ni consulta de uso diario lee esta columna -- solo el importador de
        // Cuotas y Pagos, para encontrar qué movimiento pagó cada cuota migrada.
        public string? OriginalReference { get; set; }

        [Required(ErrorMessage = "La moneda es obligatoria")]
        public string Currency { get; set; } = string.Empty;
        [NotMapped]
        public Guid IdEntityRef { get; set; }

        public TransactionOrigin Origin { get; set; }
        public ConcilationType ReconciliationStatus { get; set; }
        public DateTime? ReconciliationDate { get; set; }

        [Precision(18, 2)]
        public decimal AmountPaid { get; set; }
        [Precision(18, 2)]
        public decimal Balance { get; set; }

        public List<ViewExpense> PossibleExpenseMatches { get; set; } = [];
        public List<Installment> PossibleInstallmentMatches { get; set; } = [];

        // Docs/Pendientes-Negocio-Conciliacion.md #1 -- antes era [NotMapped] y
        // GET_BankTransactionsNoConcilied lo devolvía como literal @FALSE: "Ignorar"
        // no se guardaba en ningún lado, ni tenía motivo/tipo. Ahora es una columna real
        // de dbo.AccountStatementDetail (ver
        // Database/Scripts/2026-09-09_70_AccountStatementDetail_Ignored.sql).
        public bool Ignored { get; set; } = false;
        public string? IgnoredReason { get; set; }
        public IgnoredReasonType? IgnoredType { get; set; }
        [NotMapped]
        public bool Selected { get; set; } = false;
        public string Reference => (Amount < 0 ? "G" : "I") + SequenceNumber.ToString("D4");
        public string Notes => (Amount < 0 ? "Gasto #" : "Ingreso #") + SequenceNumber.ToString("D4");
        public string Tipo => Amount < 0 ? "Gasto" : "Ingreso";
        [NotMapped]
        public ViewExpense? ReconciledExpense { get; set; }
        [NotMapped]
        public Installment? ReconciledInstallment { get; set; }
        // Conciliación en dos pasos (Fase B): un match (automático o manual) queda
        // "propuesto" acá -- en memoria, ReconciliationStatus SIN TOCAR -- hasta que el
        // usuario confirma el lote completo con "Enviar a Conciliar". Antes de esto,
        // cualquier match (incluso el automático por monto exacto) escribía en BD al
        // toque, sin poder revisarlo ni deshacerlo gratis antes de confirmar.
        [NotMapped]
        public bool PendingProposal { get; set; } = false;
        [NotMapped]
        public bool AutomaticProposal { get; set; } = false;
        // Distingue, dentro de una propuesta de Gasto, si ReconciledExpense ya existe en BD
        // (viene de un match con un gasto previamente guardado) o si todavía es un
        // ViewExpense armado en memoria desde CreateExpenseFromTransactionModal que aún no
        // se insertó -- EnviarAConciliar usa esto para saber si tiene que crear el gasto
        // recién al confirmar (Docs/Pendientes-Negocio-Conciliacion.md #6).
        [NotMapped]
        public bool NewExpenseProposal { get; set; } = false;
        [NotMapped]
        public List<Installment> ProposedInstallments { get; set; } = new();
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
    // AmountPaid, IdGroupUnit, PossibleExpenseMatches, etc.) que no aplican a esta vista de solo
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
        [Precision(18, 2)]
        public decimal AmountInReportingCurrency { get; set; }
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

        public string DbKey
        {
            get
            {
                return $"{StatementDate:yyyyMMdd}|{Description}|{Amount}";
            }
        }
    }
}