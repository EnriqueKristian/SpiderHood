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

        // Pantalla de mantenimiento del Account (Docs/Pendientes-Negocio-Consolidado.md
        // #30, puntos a y c) -- hasta ahora RazonSocial/RucDni/Telefono sólo se
        // cargaban una vez al registrarse, sin forma de corregirlos después, y no
        // existía la distinción Natural/Empresa. accountType nace en Natural en
        // CreateAccountAsync (registro) -- se corrige a Empresa, si corresponde,
        // desde acá.
        Task<Account> UpdateAccountAsync(Guid idAccount, string? razonSocial, string? rucDni, string? telefono,
            AccountType accountType, string? legalRepresentative, string? fiscalAddress, string? officeHours);

        // Logo de la empresa administradora (Docs/Pendientes-Negocio-Consolidado.md
        // #30, punto d) -- reusa IFileStorageService, mismo mecanismo que fotos de
        // Incidencias/Recibos PDF. Reemplaza el logo anterior si ya había uno
        // (no se acumulan versiones -- sólo hay un logo vigente por Account).
        Task UploadLogoAsync(Guid idAccount, string fileName, string contentType, byte[] content);

        // Null si el Account no tiene logo cargado o si ya no está en storage
        // (mismo criterio fail-open que IFileStorageService.ReadAsync).
        Task<(byte[] Content, string ContentType)?> GetLogoAsync(Guid idAccount);

        // Para BuildingService.GetReceiptBrandingAsync -- RazonSocial (para la
        // franja "Administrado por..." del recibo) + logo (para cuando el Building
        // no tiene uno propio) en una sola consulta. Todo null si la Account no
        // existe o no tiene esos datos -- nunca tira excepción por esto.
        Task<(string? RazonSocial, byte[]? LogoBytes, string? LogoContentType)> GetBrandingAsync(Guid idAccount);

        // Para IBuildingService.GetEffectiveContactAsync (Docs/Pendientes-Negocio-Consolidado.md
        // #33) -- datos de contacto de la administradora para usar como fallback cuando un
        // Edificio no carga los suyos propios. Todo null si la Account no existe.
        Task<(string? RazonSocial, string? Telefono, string? OfficeHours)> GetContactInfoAsync(Guid idAccount);

        Task<List<AccountUserView>> GetCollaboratorsAsync(Guid idAccount);

        Task<List<AccountInvitation>> GetPendingInvitationsAsync(Guid idAccount);

        // Sólo genera la invitación (código random) -- el envío del email es
        // best-effort, no bloquea: si el SMTP no está configurado en este entorno,
        // el link igual queda visible en Settings.razor para copiar a mano.
        Task<OperationResult> InviteCollaboratorAsync(Guid idAccount, string email, Guid invitedByIdUser);

        Task<AccountInvitation?> GetInvitationByCodeAsync(string code);

        // Agrega al usuario (ya sea uno recién creado en /accept-invitation, o uno
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
        // Whitelist deliberada, mismo criterio que IIncidentService -- sólo
        // raster (no SVG): el logo se embebe en el PDF del recibo vía
        // QuestPDF's Image(byte[]) (InstallmentExportService.ComposeHeader), que
        // no decodifica SVG -- aceptar SVG acá rompería silenciosamente el PDF
        // apenas alguien subiera uno.
        private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };
        // 2 MB alcanza de sobra para un logo (no es una foto) -- lo mantiene
        // liviano para no inflar cada PDF de recibo que lo incluya.
        private const int MaxLogoSizeBytes = 2 * 1024 * 1024;

        private BDLayout Ec { get; }
        private readonly IEmailService _emailService;
        private readonly IFileStorageService _fileStorage;
        private readonly string _baseUrl;

        public AccountService(IDbContextFactory<SpiderHoodContext> contextFactory, IEmailService emailService,
            IFileStorageService fileStorage, IConfiguration configuration)
        {
            Ec = new BDLayout(contextFactory);
            _emailService = emailService;
            _fileStorage = fileStorage;
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

        public async Task<Account> UpdateAccountAsync(Guid idAccount, string? razonSocial, string? rucDni, string? telefono,
            AccountType accountType, string? legalRepresentative, string? fiscalAddress, string? officeHours)
        {
            var account = new Account
            {
                IdAccount = idAccount,
                RazonSocial = razonSocial,
                RucDni = rucDni,
                Telefono = telefono,
                AccountType = accountType,
                // Sólo tiene sentido para Empresa -- se limpia si se cambia a
                // Natural, para no dejar un representante legal "fantasma" colgado
                // de una cuenta que ya no es empresa.
                LegalRepresentative = accountType == AccountType.Empresa ? legalRepresentative : null,
                FiscalAddress = fiscalAddress,
                OfficeHours = officeHours,
            };
            await Ec.UpdateRecordAsync(account);
            return account;
        }

        public async Task<(string? RazonSocial, string? Telefono, string? OfficeHours)> GetContactInfoAsync(Guid idAccount)
        {
            var account = await Ec.GetAccountByIdAsync(idAccount);
            return (account?.RazonSocial, account?.Telefono, account?.OfficeHours);
        }

        public async Task UploadLogoAsync(Guid idAccount, string fileName, string contentType, byte[] content)
        {
            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension) || !AllowedLogoExtensions.Contains(extension))
            {
                throw new ArgumentException(
                    $"Tipo de archivo no permitido ({(string.IsNullOrEmpty(extension) ? "sin extensión" : extension)}). " +
                    $"Formatos aceptados: {string.Join(", ", AllowedLogoExtensions.Order())}.");
            }

            if (content.Length == 0)
                throw new ArgumentException("El archivo está vacío.");

            if (content.Length > MaxLogoSizeBytes)
            {
                throw new ArgumentException(
                    $"El archivo pesa {content.Length / 1024} KB -- el máximo permitido es {MaxLogoSizeBytes / 1024} KB.");
            }

            // Nombre fijo ("logo" + extensión) en vez del nombre original -- sólo
            // hay un logo vigente por Account, así que subir uno nuevo debe
            // reemplazar el archivo anterior, no acumular versiones sueltas en
            // storage con nombres random.
            var relativePath = await _fileStorage.SaveAsync(
                ["accounts", idAccount.ToString("N"), "logo"],
                $"logo{extension}",
                content);

            await Ec.UpdateAccountLogoAsync(idAccount, relativePath, contentType);
        }

        public async Task<(byte[] Content, string ContentType)?> GetLogoAsync(Guid idAccount)
        {
            var account = await Ec.GetAccountByIdAsync(idAccount);
            if (account?.LogoPath == null)
                return null;

            var bytes = await _fileStorage.ReadAsync(account.LogoPath);
            if (bytes == null)
                return null;

            return (bytes, account.LogoContentType ?? "application/octet-stream");
        }

        public async Task<(string? RazonSocial, byte[]? LogoBytes, string? LogoContentType)> GetBrandingAsync(Guid idAccount)
        {
            var account = await Ec.GetAccountByIdAsync(idAccount);
            if (account == null)
                return (null, null, null);

            var logoBytes = account.LogoPath == null ? null : await _fileStorage.ReadAsync(account.LogoPath);
            return (account.RazonSocial, logoBytes, account.LogoContentType);
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
                var link = $"{_baseUrl}/accept-invitation?code={invitation.Code}";
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
