using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class PermissionDefinition
    {
        public Guid PermissionId { get; set; } = Guid.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        [NotMapped]
        public bool IsSelected { get; set; }
    }
}
