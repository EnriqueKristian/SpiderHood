using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Cuenta de facturación + colaboradores -- Docs/Design-Account-Facturacion.md.
    public interface IAccountService
    {
        Task<Account?> GetAccountByUserAsync(Guid idUser);

        // Llamado sólo desde AuthService.RegisterNewAdministratorAsync, justo
        // después de crear el UserModel -- ese usuario queda como Owner.
        Task<Account> CreateAccountAsync(Guid ownerIdUser, string? razonSocial, string? rucDni, string? telefono);

        Task<List<AccountUserView>> GetCollaboratorsAsync(Guid idAccount);

        Task<List<AccountInvitation>> GetPendingInvitationsAsync(Guid idAccount);

        // Sólo genera la invitación (código random) -- el envío del email es
        // best-effort, no bloquea: si el SMTP no está configurado en este entorno,
        // el link igual queda visible en Settings.razor para copiar a mano.
        Task<OperationResult> InviteCollaboratorAsync(Guid idAccount, string email, Guid invitedByIdUser);

        Task<AccountInvitation?> GetInvitationByCodeAsync(string code);

        // Agrega al usuario (ya sea uno recién creado en /aceptar-invitacion, o uno
        // que ya tenía cuenta en SpiderHood) como Colaborador de la Account de la
        // invitación: crea el AccountUser, le replica UserBuildingAssociation de
        // cada edificio de esa cuenta (para que "vea" lo mismo que el resto, ver
        // decisión 3 del documento) y le otorga el rol global Administrador (mismo
        // mecanismo que ya usa el Owner) para que también pueda crear edificios
        // nuevos. Marca la invitación Accepted al final.
        Task<OperationResult> AcceptInvitationAsync(string code, Guid idUser);
    }

    public class AccountService : IAccountService
    {
        private BDLayout Ec { get; }
        private readonly IEmailService _emailService;
        private readonly string _baseUrl;

        public AccountService(IDbContextFactory<SpiderHoodContext> contextFactory, IEmailService emailService, IConfiguration configuration)
        {
            Ec = new BDLayout(contextFactory);
            _emailService = emailService;
            _baseUrl = (configuration["BaseUrl"] ?? "https://localhost:7175").TrimEnd('/');
        }

        public async Task<Account?> GetAccountByUserAsync(Guid idUser)
        {
            return await Ec.GetAccountByUserAsync(idUser);
        }

        public async Task<Account> CreateAccountAsync(Guid ownerIdUser, string? razonSocial, string? rucDni, string? telefono)
        {
            var account = new Account
            {
                IdAccount = Guid.NewGuid(),
                RazonSocial = razonSocial,
                RucDni = rucDni,
                Telefono = telefono,
                CreatedAt = DateTime.UtcNow,
            };
            await Ec.AddNewRecordAsync(account);
            await Ec.AddAccountUserAsync(account.IdAccount, ownerIdUser, "Owner");
            return account;
        }

        public async Task<List<AccountUserView>> GetCollaboratorsAsync(Guid idAccount)
        {
            return await Ec.GetAccountUsersByAccountAsync(idAccount);
        }

        public async Task<List<AccountInvitation>> GetPendingInvitationsAsync(Guid idAccount)
        {
            return await Ec.GetPendingInvitationsByAccountAsync(idAccount);
        }

        public async Task<OperationResult> InviteCollaboratorAsync(Guid idAccount, string email, Guid invitedByIdUser)
        {
            var normalizedEmail = email.Trim().ToLowerInvariant();

            var invitation = new AccountInvitation
            {
                IdAccountInvitation = Guid.NewGuid(),
                IdAccount = idAccount,
                Email = normalizedEmail,
                // 32 caracteres hex -- suficiente entropía para un link de invitación
                // de un solo uso, corto de leer/pegar a mano si el email falla.
                Code = Guid.NewGuid().ToString("N"),
                InvitedByIdUser = invitedByIdUser,
            };
            await Ec.AddNewRecordAsync(invitation);

            // Best-effort -- si el SMTP no está configurado en este entorno (dev/test),
            // no bloquea la invitación: el link queda visible en Settings.razor igual.
            try
            {
                var link = $"{_baseUrl}/aceptar-invitacion?code={invitation.Code}";
                await _emailService.SendEmailAsync(
                    normalizedEmail,
                    "Te invitaron a colaborar en SpiderHood",
                    $"Te invitaron a administrar edificios junto a tu equipo en SpiderHood. Aceptá la invitación acá: <a href=\"{link}\">{link}</a>");
            }
            catch (Exception)
            {
                // Ignorado a propósito -- ver comentario de arriba.
            }

            return OperationResult.Success(invitation);
        }

        public async Task<AccountInvitation?> GetInvitationByCodeAsync(string code)
        {
            return await Ec.GetAccountInvitationByCodeAsync(code);
        }

        public async Task<OperationResult> AcceptInvitationAsync(string code, Guid idUser)
        {
            var invitation = await Ec.GetAccountInvitationByCodeAsync(code);
            if (invitation == null)
                return OperationResult.Failure("El link de invitación no es válido.");
            if (invitation.Status != "Pending")
                return OperationResult.Failure("Esta invitación ya fue usada o cancelada.");

            await Ec.AddAccountUserAsync(invitation.IdAccount, idUser, "Colaborador");

            var buildings = await Ec.GetBuildingsByAccountAsync(invitation.IdAccount);
            foreach (var building in buildings)
            {
                await Ec.AcceptInvitationAsync(new UserBuildingAssociation
                {
                    IdUser = idUser,
                    IdBuilding = building.IdBuilding,
                    Role = "Administrador",
                    IsApproved = true,
                    RequestedAt = DateTime.UtcNow,
                });
            }

            var roles = await Ec.GetAllRolesAsync();
            var administrador = roles.FirstOrDefault(r => r.RoleName == "Administrador");
            if (administrador != null)
                await Ec.AddUserRoleAsync(idUser, administrador.IdRole);

            await Ec.UpdateAccountInvitationStatusAsync(invitation.IdAccountInvitation, "Accepted");

            return OperationResult.Success();
        }
    }
}
