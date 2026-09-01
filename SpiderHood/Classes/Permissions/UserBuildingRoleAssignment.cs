namespace SpiderHood.Models
{
    // Una fila por (usuario, edificio, rol) en UserBuildingAssociation — la tabla real
    // que AuthService.LoginAsync lee para armar el menú y los permisos de sesión. Un
    // usuario puede tener varias filas (un rol por edificio, o varios roles sobre el
    // mismo edificio), a diferencia de RoleAssignment (que asume un solo rol global por
    // usuario, sobre la tabla UserRole — desconectada de la sesión real).
    public class UserBuildingRoleAssignment
    {
        public Guid IdUser { get; set; } = Guid.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; } = Guid.Empty;
        public string BuildingName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid IdRole { get; set; } = Guid.Empty;
        public Guid? IdGroupUnit { get; set; }

        // Estado de la solicitud de acceso -- ver GET_AllUserBuildingRoles. Alimenta la
        // bandeja de "Solicitudes pendientes" en /Settings/UserRoles (IsApproved == false).
        public bool IsApproved { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? RequestedAt { get; set; }
    }
}
