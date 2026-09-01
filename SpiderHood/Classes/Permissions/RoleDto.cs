namespace SpiderHood.Models
{
    public class RoleDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsExpanded { get; set; } = true;
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
