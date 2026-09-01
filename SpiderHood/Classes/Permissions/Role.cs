using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class Role
    {
        public Guid IdRole { get; set; } = Guid.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystem { get; set; } // Para roles que no se pueden eliminar
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public List<string> Permissions { get; set; } = new();
    }
}
