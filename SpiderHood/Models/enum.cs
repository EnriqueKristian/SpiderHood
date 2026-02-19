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
}
