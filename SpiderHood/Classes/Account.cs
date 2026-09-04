namespace SpiderHood.Models
{
    // Cuenta de facturación (Docs/Design-Account-Facturacion.md) -- a quién se le
    // cobra la Subscription y de qué "pool" de edificios sale el MaxBuildings del
    // plan. NO reemplaza a UserBuildingAssociation (acceso real persona-edificio);
    // una persona se ASOCIA a una Account (ver AccountUser), no al revés.
    public class Account
    {
        public Guid IdAccount { get; set; }
        public string? RazonSocial { get; set; }
        public string? RucDni { get; set; }
        public string? Telefono { get; set; }
        public DateTime CreatedAt { get; set; }
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
        // [RazonSocial]" en /aceptar-invitacion sin un segundo round-trip.
        public string? RazonSocial { get; set; }
    }
}
