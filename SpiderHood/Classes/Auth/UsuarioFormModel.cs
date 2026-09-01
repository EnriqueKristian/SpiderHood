using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{
    public class UsuarioFormModel
    {
        public Guid IdUser { get; set; }
        public bool IsEdit { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;

        public Guid? IdRole { get; set; }

        public bool IsActive { get; set; } = true;

        // Solo se usa al crear un usuario nuevo; en edición queda vacío.
        public string Password { get; set; } = string.Empty;
    }
}
