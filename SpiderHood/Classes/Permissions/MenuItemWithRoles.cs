namespace SpiderHood.Models
{
    public class MenuItemWithRoles : MenuItemDefinition
    {
        public List<RolePermissionCheck> RolePermissions { get; set; } = new();
        public string? ParentTitle { get; set; }
        public bool IsExpanded { get; set; } = true;
        public int ChildrenCount => Children?.Count ?? 0;
        public bool HasChildren => ChildrenCount > 0;
    }
}
