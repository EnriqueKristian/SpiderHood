// Services/IMenuAdminService.cs
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IMenuAdminService
    {
        Task<List<MenuItemDefinition>> GetAllMenuItemsAsync();
        Task<List<MenuItemDefinition>> GetRootMenuItemsAsync();
        Task<MenuItemDefinition?> GetMenuItemByIdAsync(Guid id);
        Task<MenuItemDefinition> CreateMenuItemAsync(MenuItemDefinition item);
        Task UpdateMenuItemAsync(MenuItemDefinition item);
        Task DeleteMenuItemAsync(Guid id);
        Task<List<MenuItemDefinition>> GetAvailableParentsAsync(Guid? currentItemId = null);
        Task ReorderMenuAsync(List<MenuItemDefinition> items);
        Task<List<PermissionSelection>> GetAllPermissionsForMenuAsync();
        Task<bool> ValidateMenuItemUrlAsync(string url, Guid? excludeId = null);
    }

    public class MenuAdminService : IMenuAdminService
    {
        private readonly IConfiguration _configuration;
        private readonly IPermissionAdminService _permissionAdminService;
        private List<MenuItemDefinition> _menuItems = [];
        public SpiderHoodContext _context = default!;
        private BDLayout ec { get; set; }

        public MenuAdminService(SpiderHoodContext context, IConfiguration configuration, IPermissionAdminService permissionAdminService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration;
            _permissionAdminService = permissionAdminService;
            ec = new BDLayout(context);
            InitializeMenuData();
        }

        private void InitializeMenuData()
        {

            _menuItems = ec.GetMenuItemsAsync().Result;
            var permissions = ec.GetAllMenuPermissionsAsync().Result;

            var lookup = permissions
                .GroupBy(p => p.MenuId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionId).ToList());

            foreach (var m in _menuItems)
                m.RequiredPermissions = lookup.TryGetValue(m.MenuId, out var list)
                    ? list
                    : new List<Guid>();


            /*_menuItems = new List<MenuItemDefinition>
            {
                // Home
                new MenuItemDefinition
                {
                    MenuId = Guid.Parse("6678A86B-6B78-499B-9549-D80A54808DE2"),
                    ItemKey = "home",
                    Title = "Dashboard",
                    Icon = "fa-solid fa-house",
                    Url = "Dashboard",
                    DisplayOrder = 1,
                    RequiredPermissions = new List<string> { "view_dashboard" }
                },

                // Módulo Edificio
                new MenuItemDefinition
                {
                    MenuId = Guid.Parse("CC5B325A-0368-49DC-8178-7C7A1A0D02F0"),
                    ItemKey = "building",
                    Title = "Adm. Edificio",
                    Icon = "fas fa-building",
                    Target = "menu-edificio",
                    DisplayOrder = 2,
                    RequiredPermissions = new List<string> { "manage_buildings" }
                },
               
            };*/

            // Construir relaciones padre-hijo
            BuildHierarchy();
        }
        
        private void BuildHierarchy()
        {
            foreach (var item in _menuItems)
            {
                if (!string.IsNullOrEmpty(item.ParentId.ToString()))
                {
                    item.Parent = _menuItems.FirstOrDefault(p => p.MenuId == item.ParentId);
                }
            }
        }

        public Task<List<MenuItemDefinition>> GetAllMenuItemsAsync()
        {
            return Task.FromResult(_menuItems.OrderBy(i => i.DisplayOrder).ToList());
        }

        public Task<List<MenuItemDefinition>> GetRootMenuItemsAsync()
        {
            var roots = _menuItems
                .Where(i => i.ParentId == Guid.Empty)
                .OrderBy(i => i.DisplayOrder)
                .ToList();

            foreach (var root in roots)
            {
                root.Children = _menuItems
                    .Where(i => i.ParentId == root.MenuId)
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();
            }

            return Task.FromResult(roots);
        }

        public Task<MenuItemDefinition?> GetMenuItemByIdAsync(Guid id)
        {
            var item = _menuItems.FirstOrDefault(i => i.MenuId == id);
            if (item != null)
            {
                item.Children = _menuItems
                    .Where(i => i.ParentId == item.MenuId)
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();
            }
            return Task.FromResult(item);
        }

        public Task<MenuItemDefinition> CreateMenuItemAsync(MenuItemDefinition item)
        {
            item.MenuId = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;

            // Generar Target para items con hijos
            if (string.IsNullOrEmpty(item.ParentId.ToString()))
            {
                item.Target = $"menu-{item.MenuId.ToString().ToLower()}";
            }

            _menuItems.Add(item);

            _ = ec.AddNewRecordAsync(item);

            MenuPermissions mperm = new MenuPermissions();

            mperm.MenuId = item.MenuId;
            _ = ec.DeleteRecordAsync(mperm);

            foreach (var perm in item.RequiredPermissions) {
                mperm = new();
                mperm.MenuId = item.MenuId;
                mperm.PermissionId = perm;

                _ = ec.AddNewRecordAsync(mperm);
            }

            return Task.FromResult(item);
        }

        public Task UpdateMenuItemAsync(MenuItemDefinition item)
        {
            var existing = _menuItems.FirstOrDefault(i => i.MenuId == item.MenuId);
            if (existing != null)
            {
                existing.Title = item.Title;
                existing.Icon = item.Icon;
                existing.Url = item.Url;
                existing.DisplayOrder = item.DisplayOrder;
                existing.ParentId = item.ParentId;
                existing.RequiredPermissions = item.RequiredPermissions;
                existing.IsVisible = item.IsVisible;
                existing.BadgeText = item.BadgeText;
                existing.BadgeColor = item.BadgeColor;
                existing.UpdatedAt = DateTime.UtcNow;

                // Actualizar Target si es necesario
                if (string.IsNullOrEmpty(existing.ParentId.ToString()))
                {
                    existing.Target = $"menu-{existing.MenuId.ToString().ToLower()}";
                }
                else
                {
                    existing.Target = null;
                }

                _ = ec.UpdateRecordAsync(item);

                MenuPermissions mperm = new MenuPermissions();

                mperm.MenuId = item.MenuId;
                _ = ec.DeleteRecordAsync(mperm);

                foreach (var perm in item.RequiredPermissions)
                {
                    mperm = new();
                    mperm.MenuId = item.MenuId;
                    mperm.PermissionId = perm;

                    _ = ec.AddNewRecordAsync(mperm);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteMenuItemAsync(Guid id)
        {
            // Eliminar también todos los hijos
            var children = _menuItems.Where(i => i.ParentId == id).ToList();
            foreach (var child in children)
            {
                _menuItems.Remove(child);
            }

            var item = _menuItems.FirstOrDefault(i => i.MenuId == id);
            if (item != null)
            {
                _menuItems.Remove(item);
            }

            return Task.CompletedTask;
        }

        public Task<List<MenuItemDefinition>> GetAvailableParentsAsync(Guid? currentItemId = null)
        {
            var parents = _menuItems
                .Where(i => i.ParentId == Guid.Empty && i.MenuId != currentItemId)
                .OrderBy(i => i.Title)
                .ToList();

            // Agregar opción "Raíz" (sin padre)
            parents.Insert(0, new MenuItemDefinition
            {
                MenuId = Guid.Empty,
                Title = "--- Raíz del menú ---"
            });

            return Task.FromResult(parents);
        }

        public Task ReorderMenuAsync(List<MenuItemDefinition> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = _menuItems.FirstOrDefault(x => x.MenuId == items[i].MenuId);
                if (item != null)
                {
                    item.DisplayOrder = i + 1;
                }
            }
            return Task.CompletedTask;
        }

        public async Task<List<PermissionSelection>> GetAllPermissionsForMenuAsync()
        {
            var permissions = new List<PermissionSelection>();
            var permissionGroups = await _permissionAdminService.GetAllPermissionsAsync();

            foreach (var group in permissionGroups)
            {
                foreach (var perm in group.Permissions)
                {
                    permissions.Add(new PermissionSelection
                    {
                        IdPermission = perm.PermissionId,
                        PermissionKey = perm.PermissionKey,
                        Name = perm.Name,
                        Group = perm.Group,
                        DisplayGroupName = group.ModuleDisplayName,
                        IsSelected = false
                    });
                }
            }

            return permissions.OrderBy(p => p.Group).ThenBy(p => p.Name).ToList();
        }

        public Task<bool> ValidateMenuItemUrlAsync(string url, Guid? excludeId = null)
        {
            var exists = _menuItems.Any(i =>
                i.Url?.Equals(url, StringComparison.OrdinalIgnoreCase) == true &&
                i.MenuId != excludeId);

            return Task.FromResult(!exists);
        }
    }
}