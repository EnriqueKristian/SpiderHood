namespace SpiderHood.Models
{
    // Natural (una persona administra a título propio) vs Empresa (una
    // inmobiliaria/administradora formal) -- mismo concepto y mismos valores que
    // OwnerType (Unit.cs), pero es un enum propio: Account y Owner son entidades
    // distintas y no hay ninguna razón de negocio para que compartan literalmente
    // el mismo tipo (uno es quién paga la Subscription, el otro quién es dueño de
    // una unidad) más allá de la coincidencia de nombres de los 2 valores.
    public enum AccountType { Natural = 1, Empresa = 2 }

    // Cuenta de facturación (Docs/Design-Account-Facturacion.md) -- a quién se le
    // cobra la Subscription y de qué "pool" de edificios sale el MaxBuildings del
    // plan. NO reemplaza a UserBuildingAssociation (acceso real persona-edificio);
    // una persona se ASOCIA a una Account (ver AccountUser), no al revés.
    public class Account
    {
        public Guid IdAccount { get; set; }
        // Nombre completo (Natural) o razón social de la empresa (Empresa) --
        // un solo campo para los dos casos, mismo criterio que ya usa
        // ModalOwner.razor para Persona Natural/Jurídica (reutiliza Names en vez
        // de duplicar en dos propiedades distintas).
        public string? RazonSocial { get; set; }
        public string? RucDni { get; set; }
        public string? Telefono { get; set; }
        public DateTime CreatedAt { get; set; }

        // --- Campos agregados en
        // Database/Scripts/2026-09-22_136_Account_NaturalEmpresa.sql
        // (Docs/Pendientes-Negocio-Consolidado.md #30, punto c).
        public AccountType AccountType { get; set; } = AccountType.Natural;

        // Sólo tiene sentido cuando AccountType == Empresa -- nullable a
        // propósito, no se valida server-side si falta (fail-open, mismo
        // criterio que el resto de los campos "sólo Persona Jurídica" de Owner).
        public string? LegalRepresentative { get; set; }

        // Dirección fiscal para facturación -- aplica a los dos tipos (una
        // persona Natural con RUC también factura desde una dirección).
        public string? FiscalAddress { get; set; }

        // --- Horario de Atención (Database/Scripts/2026-09-22_141_Account_OfficeHours.sql,
        // Docs/Pendientes-Negocio-Consolidado.md #33) -- usado como fallback del
        // Building.OfficeHours de un edificio que no carga el suyo propio (ver
        // IBuildingService.GetEffectiveContactAsync), mismo patrón que el logo.
        public string? OfficeHours { get; set; }

        // --- Logo de la empresa administradora (Database/Scripts/
        // 2026-09-22_138_Account_Logo.sql, Docs/Pendientes-Negocio-Consolidado.md
        // #30, punto d) -- usado en la emisión de recibos (InstallmentExportService)
        // y disponible para otros PDFs/reportes. LogoPath es la ruta RELATIVA que
        // devuelve IFileStorageService.SaveAsync, nunca una URL pública.
        public string? LogoPath { get; set; }
        public string? LogoContentType { get; set; }
    }

    // Fila de GET_AccountUsersByAccount -- denormalizada (trae nombre/email del
    // usuario del join) para pintar la lista de "Colaboradores" en Settings.razor
    // sin un round-trip aparte por cada fila.
    public class AccountUserView
    {
        public Guid IdAccountUser { get; set; }
        public Guid IdAccount { get; set; }
        public Guid IdUser { get; set; }
        public string Role { get; set; } = string.Empty; // Owner | Colaborador
        public DateTime CreatedAt { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    // Invitación pendiente de un colaborador -- separada de InvitationModel
    // (que es específico de invitar Residentes a un edificio, con
    // IdBuilding/ApartmentNumber que no aplican acá).
    public class AccountInvitation
    {
        public Guid IdAccountInvitation { get; set; }
        public Guid IdAccount { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending | Accepted | Cancelled
        public Guid InvitedByIdUser { get; set; }
        public DateTime CreatedAt { get; set; }
        // Sólo viene de GET_AccountInvitationByCode -- para mostrar "Te invitaron a
        // [RazonSocial]" en /accept-invitation sin un segundo round-trip.
        public string? RazonSocial { get; set; }
    }
}
