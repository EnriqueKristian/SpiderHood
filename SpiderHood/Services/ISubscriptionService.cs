using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Suscripción SaaS del Administrador -- ver Docs/Design-Subscripcion-Administrador.md.
    public interface ISubscriptionService
    {
        Task<Subscription?> GetSubscriptionByUserAsync(Guid idUser);

        Task<List<SubscriptionPlan>> GetAllPlansAsync();

        // Alta "Piloto" (AuthService.RegisterNewAdministratorAsync): todo
        // Administrador nuevo arranca en el plan Trial, sin límite de edificios
        // ni vencimiento real todavía. idAccount es la Account recién creada para
        // ese registro (Docs/Design-Account-Facturacion.md) -- a diferencia de los
        // otros 3 métodos de esta interfaz, éste no necesita mantener una firma
        // vieja por IdUser porque sólo lo llama ese único lugar.
        Task CreateTrialSubscriptionAsync(Guid idUser, Guid idAccount);

        // Chequeo antes de crear un edificio (BuildingService.CreateBuildingAsync).
        // Fail-open: si la cuenta no tiene ninguna Subscription todavía, o su plan
        // no tiene MaxBuildings, no se restringe nada.
        Task<OperationResult> EnsureCanCreateBuildingAsync(Guid idUser, string role);

        // Llamado únicamente desde el webhook de MercadoPago (evento
        // subscription_preapproval, status "authorized"), nunca desde el redirect
        // de éxito del navegador -- ver Docs/Design-Subscripcion-Administrador.md.
        Task ActivateSubscriptionAsync(Guid idUser, int idSubscriptionPlan, string mercadoPagoPreapprovalId);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private BDLayout Ec { get; }

        public SubscriptionService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            Ec = new BDLayout(contextFactory);
        }

        public async Task<Subscription?> GetSubscriptionByUserAsync(Guid idUser)
        {
            return await Ec.GetSubscriptionByUserAsync(idUser);
        }

        public async Task<List<SubscriptionPlan>> GetAllPlansAsync()
        {
            return await Ec.GetAllSubscriptionPlansAsync();
        }

        // Si el plan Trial no existe todavía (seed del script no corrido), no
        // rompe el registro -- el usuario sólo queda sin ninguna fila en
        // Subscription, y EnsureCanCreateBuildingAsync no restringe a quien no
        // tiene ninguna.
        public async Task CreateTrialSubscriptionAsync(Guid idUser, Guid idAccount)
        {
            var plans = await Ec.GetAllSubscriptionPlansAsync();
            var trial = plans.FirstOrDefault(p => p.Name == "Trial");
            if (trial == null)
                return;

            // 60 días de prueba (prometidos en la landing, wwwroot/index.html: "Todos
            // los planes incluyen 60 días de prueba gratuita") -- antes quedaba en
            // NULL, sin ningún vencimiento real. Settings.razor avisa cuántos días
            // quedan; qué pasa efectivamente al vencer (bloquear, degradar a Básico,
            // etc.) queda para cuando haya una tarea programada que lo chequee -- por
            // ahora sólo informa.
            var now = DateTime.UtcNow;
            await Ec.AddNewRecordAsync(new Subscription
            {
                IdSubscription = Guid.NewGuid(),
                IdUser = idUser,
                IdAccount = idAccount,
                IdSubscriptionPlan = trial.IdSubscriptionPlan,
                Status = "Trial",
                StartDate = now,
                EndDate = now.AddDays(60)
            });
        }

        // Sólo el rol "Administrador" administra edificios propios -- SysAdmin
        // (edificios Template, soporte) nunca se restringe acá. El conteo es por
        // Account (Docs/Design-Account-Facturacion.md): los edificios que ve un
        // colaborador cuentan igual que los del Owner, no duplican el límite.
        // Fail-open: si el usuario todavía no tiene ninguna Account (cuentas de
        // antes de este feature), cae al conteo viejo por UserBuildingAssociation.
        public async Task<OperationResult> EnsureCanCreateBuildingAsync(Guid idUser, string role)
        {
            if (role != "Administrador")
                return OperationResult.Success();

            var subscription = await GetSubscriptionByUserAsync(idUser);
            if (subscription?.MaxBuildings is not { } max)
                return OperationResult.Success();

            var account = await Ec.GetAccountByUserAsync(idUser);
            int currentCount;
            if (account != null)
            {
                var buildings = await Ec.GetBuildingsByAccountAsync(account.IdAccount);
                currentCount = buildings.Count;
            }
            else
            {
                var associations = await Ec.GetUserBuildingAssociationAsync(idUser);
                currentCount = associations
                    .Where(a => a.Role == "Administrador" && a.IsApproved)
                    .Select(a => a.IdBuilding)
                    .Distinct()
                    .Count();
            }

            if (currentCount >= max)
            {
                return OperationResult.Failure(
                    $"Tu plan {subscription.PlanName} permite administrar hasta {max} edificio(s). " +
                    "Actualizá tu plan para crear más.");
            }

            return OperationResult.Success();
        }

        public async Task ActivateSubscriptionAsync(Guid idUser, int idSubscriptionPlan, string mercadoPagoPreapprovalId)
        {
            await Ec.ActivateSubscriptionAsync(idUser, idSubscriptionPlan, mercadoPagoPreapprovalId);
        }
    }
}
