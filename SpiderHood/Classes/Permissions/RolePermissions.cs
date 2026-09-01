namespace SpiderHood.Models
{
    public class RolePermissions
    {
        public Guid IdRole { get; set; } = Guid.Empty;
        public Guid IdPermission { get; set; } = Guid.Empty;
        public string RoleName { get; set; } = string.Empty;
        public string PermissionKey { get; set; } = string.Empty;
    }
}
