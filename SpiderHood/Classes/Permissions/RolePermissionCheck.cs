namespace SpiderHood.Models
{
    public class RolePermissionCheck
    {
        public Guid IdRole { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public bool CanView { get; set; }
        public bool IsExpanded { get; set; }
    }
}
