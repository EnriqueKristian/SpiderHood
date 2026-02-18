using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{
    public class User
    {
        public Guid IdUser { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
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
    }

    public class UserSession
    {
        public Guid IdUser { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = [];
        public List<UserBuilding> Buildings { get; set; } = [];
        public Guid CurrentBuildingId { get; set; }
        public DateTime SessionStart { get; set; }
        public DateTime SessionExpiry { get; set; }
        public bool IsAuthenticated => SessionExpiry > DateTime.UtcNow;

        public bool RememberMe { get; internal set; }
    }

    public class UserBuilding
    {
        public Models.Building? Building { get; set; } 
        public string Role { get; set; } = string.Empty; // Admin, Junta, Residente
        public bool IsApproved { get; set; }
        public DateTime? ApprovedAt { get; set; }
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
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public UserSession UserSession { get; set; }= new();
        public string Token { get; set; }= string.Empty;
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
}