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
        public int IdSubscriptionPlan { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public int? MaxBuildings { get; set; }
        public string Status { get; set; } = "Trial"; // Trial, Active, Expired, Cancelled
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
