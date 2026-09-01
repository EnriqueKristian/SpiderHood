using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Installment
    {
        public Guid IdInstallment { get; set; }
        public Guid IdBudgetHeader { get; set; }
        public int Number { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        [Precision(18, 2)] public decimal Amount { get; set; }
        [Precision(18, 2)] public decimal Percent { get; set; }
        [Precision(18, 2)] public decimal TotalArea { get; set; }
        public DateTime Period { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public ConcilationType Status { get; set; }
        [Precision(18, 2)] public decimal AmountPaid { get; set; }
        [Precision(18, 2)] public decimal Debt { get; set; }
        public Guid IdGroupUnit { get; set; }
        public DateTime DueDate { get; set; }
        // Ordinaria (default, cuota mensual normal) vs. Extraordinaria/Multa/Mora,
        // generadas por ExtraChargeService bajo su propio BudgetHeader. Requiere la
        // columna Installment.Type — ver
        // Database/Migrations/2026-08-28_CuotasExtraordinarias_MultasMora.sql.
        public InstallmentType Type { get; set; } = InstallmentType.Ordinaria;
        // Descripción libre para cuotas que no vienen de BudgetDetail (p.ej. "Fondo de
        // obras - pintado fachada", "Mora (2 meses de atraso) - Cuota Jun-2026"). Las
        // Ordinarias la dejan vacía porque su desglose sale de BudgetHeader.Details.
        public string Concept { get; set; } = string.Empty;
        // Para Multa/Mora: IdInstallment de la cuota Ordinaria vencida que originó el
        // cargo. Permite calcular mora incremental (cuánto ya se cobró de más contra
        // esa cuota) sin duplicar ni necesitar UPDATE. Guid.Empty para Ordinaria/Extraordinaria.
        public Guid SourceInstallmentId { get; set; } = Guid.Empty;
        [NotMapped]
        public bool IsPaid { get; set; } = false;
        [NotMapped]
        public bool Reconciled { get; set; } = false;
        [NotMapped]
        public Guid ReconciledTransactionId { get; set; }
        [NotMapped]
        public bool AutoReconcile { get; set; } = false;
        [NotMapped]
        public DateTime LastPartialPaymentDate { get; set; }
        [NotMapped]
        public List<TransactionBankDetail> PosiblesMatches { get; set; } = [];
        [NotMapped]
        public List<InstallmentPaid> Paids { get; set; } = [];
        [NotMapped]
        public List<TransactionBankDetail> PreviousPaid { get; set; } = [];
    }
}
