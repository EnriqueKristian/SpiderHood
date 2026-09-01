namespace SpiderHood.Models
{
    public class @enum
    {
    }

    public enum ConcilationType
    {
        NoConciliada = 0,
        Conciliada = 1,
        Parcial = 2,
        Pendiente = 3
    }

    public enum TransactionOrigen
    {
        BankAccountState = 0,
        ExcessPayment = 1,
    }

    public enum TipoDistribucion
    {
        Fija,       // División igualitaria
        Porcentual  // Según porcentaje de área
    }
    public enum EstadoCuota
    {
        Pendiente,
        Generada,
        Procesada,
        Anulada,
        PagadaParcialmente
    }

    public enum EstadoPago
    {
        Pendiente,
        Pagado,
        Atrasado,
        Anulado
    }

    public enum TipoCategoriaGasto
    {
        Ordinario,
        Extraordinario,
        Mantenimiento,
        Administrativo,
        Otros
    }
    public enum PaymentMethod
    {
        Cash = 1,
        CreditCard = 2,
        BankTransfer = 3,
        Check = 4,
        DebitCard = 5,
        Other = 6
    }

    public enum StatusExpense
    {
        Pending,
        Approved,
        Rejected,
        Paid
    }
    public enum BudgetStatus
    {
        All = 0,
        Created = 1,
        Check = 2,
        Approved = 3,
        Active = 4,
        Rejected = 5,
        Closed = 6
    }

    // Distingue el origen de una cuota (Installment): las Ordinarias vienen del ciclo
    // mensual normal (BudgetGenerator); Extraordinaria, Multa y Mora se generan aparte
    // (ExtraChargeService) y quedan agrupadas bajo su propio BudgetHeader
    // (BudgetType = "Extraordinario" / "Cargos") en vez de mezclarse con el presupuesto
    // mensual.
    public enum InstallmentType
    {
        Ordinaria = 0,
        Extraordinaria = 1,
        Multa = 2,
        Mora = 3
    }
}
