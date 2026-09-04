using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Suscripción SaaS del Administrador -- ver Docs/Design-Subscripcion-Administrador.md.
    public interface ISubscriptionService
    {
        Task<Subscription?> GetSubscriptionByUserAsync(Guid idUser);

        // Alta "Piloto" (AuthService.RegisterNewAdministratorAsync): todo
        // Administrador nuevo arranca en el plan Trial, sin límite de edificios
        // ni vencimiento real todavía.
        Task CreateTrialSubscriptionAsync(Guid idUser);

        // Chequeo antes de crear un edificio (BuildingService.CreateBuildingAsync).
        // Fail-open: si la cuenta no tiene ninguna Subscription todavía, o su plan
        // no tiene MaxBuildings, no se restringe nada.
        Task<OperationResult> EnsureCanCreateBuildingAsync(Guid idUser, string role);
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

        // Si el plan Trial no existe todavía (seed del script no corrido), no
        // rompe el registro -- el usuario sólo queda sin ninguna fila en
        // Subscription, y EnsureCanCreateBuildingAsync no restringe a quien no
        // tiene ninguna.
        public async Task CreateTrialSubscriptionAsync(Guid idUser)
        {
            var plans = await Ec.GetAllSubscriptionPlansAsync();
            var trial = plans.FirstOrDefault(p => p.Name == "Trial");
            if (trial == null)
                return;

            await Ec.AddNewRecordAsync(new Subscription
            {
                IdSubscription = Guid.NewGuid(),
                IdUser = idUser,
                IdSubscriptionPlan = trial.IdSubscriptionPlan,
                Status = "Trial",
                StartDate = DateTime.UtcNow
            });
        }

        // Sólo el rol "Administrador" administra edificios propios -- SysAdmin
        // (edificios Template, soporte) nunca se restringe acá.
        public async Task<OperationResult> EnsureCanCreateBuildingAsync(Guid idUser, string role)
        {
            if (role != "Administrador")
                return OperationResult.Success();

            var subscription = await GetSubscriptionByUserAsync(idUser);
            if (subscription?.MaxBuildings is not { } max)
                return OperationResult.Success();

            var associations = await Ec.GetUserBuildingAssociationAsync(idUser);
            var currentCount = associations
                .Where(a => a.Role == "Administrador" && a.IsApproved)
                .Select(a => a.IdBuilding)
                .Distinct()
                .Count();

            if (currentCount >= max)
            {
                return OperationResult.Failure(
                    $"Tu plan {subscription.PlanName} permite administrar hasta {max} edificio(s). " +
                    "Actualizá tu plan para crear más.");
            }

            return OperationResult.Success();
        }
    }
}
