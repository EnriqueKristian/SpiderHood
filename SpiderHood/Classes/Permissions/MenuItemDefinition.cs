using System.ComponentModel.DataAnnotations.Schema;

namespace SpiderHood.Models
{
    public class MenuItemDefinition
    {
        public Guid IdMenu { get; set; } = Guid.NewGuid();
        public string ItemKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Url { get; set; }
        public int DisplayOrder { get; set; }
        public Guid? IdParent { get; set; }
        public string ParentKey { get; set; } = string.Empty;
        public string? Target { get; set; } // Para collapse ID
        [NotMapped]
        public List<Guid> RequiredPermissions { get; set; } = new();

        public bool IsVisible { get; set; } = true;

        public string? BadgeText { get; set; }

        public string? BadgeColor { get; set; } = "danger";
        [NotMapped]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public DateTime? UpdatedAt { get; set; }

        // Propiedades de navegación
        [NotMapped]
        public List<MenuItemWithRoles> Children { get; set; } = new();
        [NotMapped]
        public MenuItemDefinition? Parent { get; set; }
    }
}
