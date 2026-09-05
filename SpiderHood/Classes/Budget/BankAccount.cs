using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{
    public class BankAccount
    {
        public Guid IdBankAccount { get; set; }
        public string AccountName { get; set; } = "";

        // Límite real de dbo.BankAccount.AccountNumber (ver
        // Database/Scripts/2026-09-05_50_BankAccount_Fixes.sql) -- sin esto, un
        // número con guiones de formato que superara los 30 caracteres fallaba
        // en el INSERT con un error de truncamiento que quedaba invisible para
        // el usuario (ver fix en BankAccountService.AddBankAccount).
        [Required(ErrorMessage = "El número de cuenta es requerido")]
        [StringLength(30, ErrorMessage = "Máximo 30 caracteres")]
        public string AccountNumber { get; set; } = "";

        [StringLength(30, ErrorMessage = "Máximo 30 caracteres")]
        public string CCI { get; set; } = "";

        [Required(ErrorMessage = "El banco es requerido")]
        public string BankName { get; set; } = "";
        public int AccountType { get; set; }

        // Se carga UNA SOLA VEZ al crear la cuenta -- la UI lo deshabilita al
        // editar (BuildingPage.razor). No confundir con CurrentBalance: éste no
        // se toca nunca después de la creación.
        [Precision(18, 2)]
        public decimal InitialBalance { get; set; }

        // Sólo lectura en el formulario -- arranca en 0 al crear la cuenta y lo
        // actualiza el proceso de Conciliación (ingresos/egresos reales), no la
        // creación/edición manual de la cuenta.
        [Precision(18, 2)]
        public decimal CurrentBalance { get; set; }
        [Precision(18, 2)]
        public decimal ReconciledBalance { get; set; }
        // Nullable: una cuenta recién creada todavía no tuvo ninguna
        // conciliación (ver Database/Scripts/2026-09-05_51_BankAccount_InitialBalance.sql).
        public DateTime? LastReconciliation { get; set; }
        public int Status { get; set; }
        public Guid IdBuilding { get; set; }

        public BankAccount Clone() => (BankAccount)this.MemberwiseClone();
    }
}
