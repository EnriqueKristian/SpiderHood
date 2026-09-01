namespace SpiderHood.Models
{
    public class PermissionSelection
    {
        public Guid IdPermission { get; set; } = Guid.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string DisplayGroupName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public string ParentPermissionKey { get; set; } = string.Empty;
    }
}
