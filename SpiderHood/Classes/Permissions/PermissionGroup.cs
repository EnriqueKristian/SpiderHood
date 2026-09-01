namespace SpiderHood.Models
{
    public class PermissionGroup
    {
        public string Module { get; set; } = string.Empty;
        public string ModuleDisplayName { get; set; } = string.Empty;
        public List<PermissionDefinition> Permissions { get; set; } = new();
        public string Icon { get; set; } = "fas fa-cog";
    }
}
