using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class RoleAssignment
    {
        public Guid IdUser { get; set; } = Guid.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
        [NotMapped]
        public List<Role> AvailableRoles { get; set; } = new();
    }
}
