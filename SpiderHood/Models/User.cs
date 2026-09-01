using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class UserModel
    {
        public Guid IdUser { get; set; }
        public string UserName => Email;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool EmailConfirmed { get; set; }
        public List<Building> Buildings { get; set; } = [];
        public string Token { get; set; } = string.Empty;
    }

    public class LoginModel
    {
        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public class RegisterModel
    {
        [Required(ErrorMessage = "El nombre es requerido")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es requerido")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty.ToString();

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Las contraseñas no coinciden")]
        public string ConfirmPassword { get; set; } = string.Empty.ToString();

        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Selecciona un edificio")]
        public Guid BuildingId { get; set; }
    }

    public class UserSession
    {
        public Guid IdUser { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
        public List<UserBuilding> Buildings { get; set; } = [];
        public Guid CurrentBuildingId { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionExpiry { get; set; }
        public bool IsAuthenticated => SessionExpiry > DateTime.UtcNow;
        public bool RememberMe { get; internal set; }
        public string Role { get; set; } = string.Empty; //=> Roles[0];

        // "Ver como" (SysAdmin únicamente): simula navegar como Role/CurrentBuildingId de
        // otro rol+edificio, sin crear ni tocar ninguna cuenta real -- para soporte, sin
        // tener que mantener un usuario de prueba por cada rol de cada edificio. Es de
        // solo lectura: mientras IsViewingAs esté activo, PermissionService.HasPermissionAsync
        // devuelve siempre false (ver ese método), así que cualquier acción que dependa de
        // un permiso queda bloqueada. Los campos Real* guardan la identidad SysAdmin real
        // para poder restaurarla al salir (AuthService.StopViewAsAsync).
        public bool IsViewingAs { get; set; }
        public string? RealRole { get; set; }
        public Guid? RealCurrentBuildingId { get; set; }
        public List<UserBuilding>? RealBuildings { get; set; }

        public Guid IdRole =>
            Role switch
            {
                "Administrador" => Guid.Parse("46198F07-F865-49A6-8057-571B867C5D1B"),
                "Residente" => Guid.Parse("E507B520-E2F5-4A47-99FB-D71B5515A575"),
                "Junta" => Guid.Parse("46461AA2-5A7B-4083-88CF-D9FD4704DF80"),
                "SysAdmin" => Guid.Parse("E6A7FC24-75C2-44CE-88BF-7FC5B2A0EED4"),
                _ => Guid.Empty
            };
    }

    public class UserBuilding
    {
        public Models.Building? Building { get; set; }
        public string Role { get; set; } = string.Empty; // Admin, Junta, Residente
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // Unidad (GroupUnit) que le corresponde a este usuario para este edificio+rol —
        // sólo tiene sentido para Residente/Propietario. Null si nunca se vinculó (o no
        // aplica, p.ej. Administrador/Junta). Es lo que permite filtrar "mis cuotas" de
        // Installment (que se identifican por IdGroupUnit, no por usuario) — ver
        // Mis Recibos/Mis Deudas y Profile > Finanzas.
        public Guid? IdGroupUnit { get; set; }
    }

    public class UserBuildingAssociation
    {
        public Guid IdUser { get; set; }
        public Guid IdBuilding { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime? RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }
        public string Status { get; set; } = "Pending"; // Active, Pending, Inactive
        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool RequiresApproval { get; set; }
        public Guid IdRole { get; set; }
        public Guid? IdGroupUnit { get; set; }

    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserSession UserSession { get; set; } = new();
        public string Token { get; set; } = string.Empty;
    }


    // Extensión útil para validar que el edificio pertenece al usuario
    public static class BuildingExtensions
    {
        public static bool BelongsToUser(this Models.UserBuilding building, Models.UserSession user)
        {
            return user?.Buildings?.Any(b => b.Building!.IdBuilding == building.Building!.IdBuilding) ?? false;
        }

        public static Models.UserBuilding? GetValidDefaultBuilding(
            this IEnumerable<Models.UserBuilding> buildings,
            Guid? preferredBuildingId)
        {
            if (!preferredBuildingId.HasValue)
                return buildings.FirstOrDefault();

            return buildings.FirstOrDefault(b => b.Building!.IdBuilding == preferredBuildingId.Value)
                   ?? buildings.FirstOrDefault();
        }
    }

    public class EmailConfirmationModel
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public bool? RequiresApproval { get; set; }
    }

    public class EmailConfirmationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool RequiresApproval { get; set; }
        public bool EmailConfirmed { get; set; }
    }

    public class ResendConfirmationModel
    {
        public string Email { get; set; } = string.Empty;
    }
}