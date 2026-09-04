namespace SpiderHood.Models
{
    public class SubscriptionPlan
    {
        public int IdSubscriptionPlan { get; set; }
        public string Name { get; set; } = string.Empty;

        // Cantidad máxima de edificios que puede administrar una cuenta en este
        // plan. NULL = sin límite (Trial y Empresarial); Básico = 1. Ver
        // ISubscriptionService.EnsureCanCreateBuildingAsync.
        public int? MaxBuildings { get; set; }
        public bool IsActive { get; set; } = true;

        // Precio mensual real -- a diferencia de Stripe (que exigía un Price
        // pre-creado en su Dashboard), la Preapproval API de MercadoPago acepta
        // el monto directo en la llamada, así que se guarda acá. NULL en el
        // plan Trial a propósito -- nunca se cobra, IPaymentService.CreateCheckoutSessionAsync
        // rechaza intentarlo.
        public decimal? Amount { get; set; }
        public string? CurrencyId { get; set; } // "PEN"
    }

    // Suscripción SaaS del Administrador (Docs/Design-Subscripcion-Administrador.md):
    // lo que un Administrador le paga a SpiderHood por usar el sistema -- no
    // confundir con las cuotas/expensas que un Residente le paga al edificio
    // (Installment). Se ata al usuario (IdUser), no al Building -- un
    // Administrador con plan Empresarial puede tener varios edificios bajo una
    // sola suscripción. Denormalizada (trae PlanName/MaxBuildings del join con
    // SubscriptionPlan directo, ver GET_SubscriptionByUser) para no necesitar un
    // segundo round-trip cada vez que se lee.
    public class Subscription
    {
        public Guid IdSubscription { get; set; }
        public Guid IdUser { get; set; }
        // Cuenta de facturación dueña de esta suscripción (Docs/Design-Account-Facturacion.md).
        // NULL sólo en filas de antes de ese feature que el backfill no pudo resolver
        // (no debería pasar en la práctica -- ver 2026-09-04_48_Account.sql).
        public Guid? IdAccount { get; set; }
        public int IdSubscriptionPlan { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int? MaxBuildings { get; set; }
        public string Status { get; set; } = "Trial"; // Trial, Active, Expired, Cancelled
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Id del recurso Preapproval en MercadoPago -- sólo se completa tras un
        // pago confirmado por el webhook (evento subscription_preapproval,
        // status "authorized"). Ver ISubscriptionService.ActivateSubscriptionAsync.
        // NULL en el Trial automático.
        public string? MercadoPagoPreapprovalId { get; set; }
    }
}
