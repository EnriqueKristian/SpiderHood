using System.ComponentModel.DataAnnotations;

namespace SpiderHood.Models
{
    public class User
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de correo inválido")]
        public string Email { get; set; } = string.Empty;

        [Range(18, 99, ErrorMessage = "La edad debe estar entre 18 y 99")]
        public int Edad { get; set; }
    }

    /*
    public class view_unit {
        public Guid Id { get; set; }
        public string Unit_Number { get; set; } = null!;
        public Guid IdBuilding { get; set; }
        public Guid IdUnitType { get; set; }
        public decimal Area { get; set; }
        public string Type { get; set; }
        public string Building { get; set; }
    } */
}
