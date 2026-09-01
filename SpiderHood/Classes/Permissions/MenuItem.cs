namespace SpiderHood.Models
{
    public class MenuItem
    {
        public Guid IdMenu { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ItemKey { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Url { get; set; }
        public int Order { get; set; }
        public Guid? IdParent { get; set; }
        public List<string> RequiredPermissions { get; set; } = new();
        public List<MenuItem> Children { get; set; } = new();
        public bool IsVisible { get; set; } = true;
        public string? Target { get; set; } // Para menús colapsables
    }
}
