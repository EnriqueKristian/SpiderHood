// Services/IMenuAdminService.cs
using DocumentFormat.OpenXml.InkML;
using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IMenuAdminService
    {
        Task<List<MenuItemWithRoles>> GetAllMenuItemsAsync();
        Task<List<MenuItemWithRoles>> GetRootMenuItemsAsync();
        Task<MenuItemWithRoles?> GetMenuItemByIdAsync(Guid id);
        Task<MenuItemWithRoles> CreateMenuItemAsync(MenuItemWithRoles item);
        Task UpdateMenuItemAsync(MenuItemWithRoles item);
        Task DeleteMenuItemAsync(Guid id);
        Task<List<MenuItemWithRoles>> GetAvailableParentsAsync(Guid? currentItemId = null);
        Task ReorderMenuAsync(List<MenuItemWithRoles> items);
        Task<List<PermissionSelection>> GetAllPermissionsForMenuAsync();
        Task<bool> ValidateMenuItemUrlAsync(string url, Guid? excludeId = null);

        Task<List<RoleDto>> GetAllRolesAsync();
        Task<List<MenuItemWithRoles>> GetRootMenuItemsWithRolesAsync();
        
        Task UpdateMenuItemPermissionsAsync(Guid menuItemId, List<Guid> roleIds, List<bool> action);
    }

    public class MenuAdminService : IMenuAdminService
    {
        private readonly IConfiguration _configuration;
        private readonly IPermissionAdminService _permissionAdminService;
        private List<MenuItemWithRoles> _menuItems = [];
        private BDLayout ec { get; set; }

        public MenuAdminService(IDbContextFactory<SpiderHoodContext> contextFactory, IConfiguration configuration, IPermissionAdminService permissionAdminService)
        {
            _configuration = configuration;
            _permissionAdminService = permissionAdminService;
            ec = new BDLayout(contextFactory);
            InitializeMenuData();
        }

        private void InitializeMenuData()
        {

            _menuItems = ec.GetMenuItemsAsync().Result;
            var permissions = ec.GetAllMenuPermissionsAsync().Result;

            var lookup = permissions
                .GroupBy(p => p.IdMenu)
                .ToDictionary(g => g.Key, g => g.Select(x => x.IdRole).ToList());

            foreach (var m in _menuItems)
                m.RequiredPermissions = lookup.TryGetValue(m.IdMenu, out var list)
                    ? list
                    : new List<Guid>();

            // Construir relaciones padre-hijo
            BuildHierarchy();
        }
        
        private void BuildHierarchy()
        {
            foreach (var item in _menuItems)
            {
                if (!string.IsNullOrEmpty(item.IdParent.ToString()))
                {
                    item.Parent = _menuItems.FirstOrDefault(p => p.IdMenu == item.IdParent);
                }
            }
        }

        public Task<List<MenuItemWithRoles>> GetAllMenuItemsAsync()
        {
            return Task.FromResult(_menuItems.OrderBy(i => i.DisplayOrder).ToList());
        }

        public Task<List<MenuItemWithRoles>> GetRootMenuItemsAsync()
        {
            var roots = _menuItems
                .Where(i => i.IdParent == Guid.Empty)
                .OrderBy(i => i.DisplayOrder)
                .ToList();

            foreach (var root in roots)
            {
                root.Children = _menuItems
                    .Where(i => i.IdParent == root.IdMenu)
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();
            }

            return Task.FromResult(roots);
        }

        public Task<MenuItemWithRoles?> GetMenuItemByIdAsync(Guid id)
        {
            var item = _menuItems.FirstOrDefault(i => i.IdMenu == id);
            if (item != null)
            {
                item.Children = _menuItems
                    .Where(i => i.IdParent == item.IdMenu)
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();
            }
            return Task.FromResult(item);
        }

        public Task<MenuItemWithRoles> CreateMenuItemAsync(MenuItemWithRoles item)
        {
            item.IdMenu = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;

            // Generar Target para items con hijos
            if (string.IsNullOrEmpty(item.IdParent.ToString()))
            {
                item.Target = $"menu-{item.IdMenu.ToString().ToLower()}";
            }

            _menuItems.Add(item);

            _ = ec.AddNewRecordAsync(item);

            MenuPermissions mperm = new MenuPermissions();

            mperm.IdMenu = item.IdMenu;
            _ = ec.DeleteRecordAsync(mperm);

            foreach (var perm in item.RequiredPermissions) {
                mperm = new();
                mperm.IdMenu = item.IdMenu;
                mperm.IdRole = perm;

                _ = ec.AddNewRecordAsync(mperm);
            }

            return Task.FromResult(item);
        }

        public Task UpdateMenuItemAsync(MenuItemWithRoles item)
        {
            var existing = _menuItems.FirstOrDefault(i => i.IdMenu == item.IdMenu);
            if (existing != null)
            {
                existing.Title = item.Title;
                existing.Icon = item.Icon;
                existing.Url = item.Url;
                existing.DisplayOrder = item.DisplayOrder;
                existing.IdParent = item.IdParent;
                existing.RequiredPermissions = item.RequiredPermissions;
                existing.IsVisible = item.IsVisible;
                existing.BadgeText = item.BadgeText;
                existing.BadgeColor = item.BadgeColor;
                existing.UpdatedAt = DateTime.UtcNow;

                // Actualizar Target si es necesario
                if (string.IsNullOrEmpty(existing.IdParent.ToString()))
                {
                    existing.Target = $"menu-{existing.IdMenu.ToString().ToLower()}";
                }
                else
                {
                    existing.Target = null;
                }

                _ = ec.UpdateRecordAsync(item);

                MenuPermissions mperm = new MenuPermissions();

                mperm.IdMenu = item.IdMenu;
                _ = ec.DeleteRecordAsync(mperm);

                foreach (var perm in item.RequiredPermissions)
                {
                    mperm = new();
                    mperm.IdMenu = item.IdMenu;
                    mperm.IdRole = perm;

                    _ = ec.AddNewRecordAsync(mperm);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteMenuItemAsync(Guid id)
        {
            // Eliminar también todos los hijos
            var children = _menuItems.Where(i => i.IdParent == id).ToList();
            foreach (var child in children)
            {
                _menuItems.Remove(child);
            }

            var item = _menuItems.FirstOrDefault(i => i.IdMenu == id);
            if (item != null)
            {
                _menuItems.Remove(item);
            }

            return Task.CompletedTask;
        }

        public Task<List<MenuItemWithRoles>> GetAvailableParentsAsync(Guid? currentItemId = null)
        {
            var parents = _menuItems
                .Where(i => i.IdParent == Guid.Empty && i.IdMenu != currentItemId)
                .OrderBy(i => i.Title)
                .ToList();

            // Agregar opción "Raíz" (sin padre)
            parents.Insert(0, new MenuItemWithRoles
            {
                IdMenu = Guid.Empty,
                Title = "--- Raíz del menú ---"
            });

            return Task.FromResult(parents);
        }

        public Task ReorderMenuAsync(List<MenuItemWithRoles> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = _menuItems.FirstOrDefault(x => x.IdMenu == items[i].IdMenu);
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
                i.IdMenu != excludeId);

            return Task.FromResult(!exists);
        }




        // ============ NUEVOS MÉTODOS PARA ROLES Y PERMISOS ============

        public async Task<List<RoleDto>> GetAllRolesAsync()
        {
            try
            {
                // Usando BDLayout para obtener roles
                var roles = await ec.GetAllRolesAsync();

                return roles.Select(r => new RoleDto
                {
                    Id = r.IdRole,
                    Name = r.RoleName ?? string.Empty,
                    Description = r.Description ?? "Rol de usuario",
                    CreatedAt = r.CreatedAt,
                    //UserCount = r.UserCount, // Asumiendo que tienes esta propiedad
                    IsExpanded = true
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener roles: {ex.Message}");

                // Datos de ejemplo si no hay roles en BD
                return new List<RoleDto>
                    {
                        new RoleDto { Id = Guid.NewGuid(), Name = "Administrador", Description = "Acceso total", UserCount = 5 },
                        new RoleDto { Id = Guid.NewGuid(), Name = "Supervisor", Description = "Supervisión general", UserCount = 12 },
                        new RoleDto { Id = Guid.NewGuid(), Name = "Operador", Description = "Operaciones básicas", UserCount = 25 },
                        new RoleDto { Id = Guid.NewGuid(), Name = "Consultor", Description = "Solo lectura", UserCount = 8 }
                    };
            }
        }

        public async Task<List<MenuItemWithRoles>> GetRootMenuItemsWithRolesAsync()
        {
            try
            {
                // Obtener todos los roles
                var roles = await GetAllRolesAsync();

                // Obtener items raíz
                var rootItems = _menuItems
                    .Where(i => i.IdParent == null || i.IdParent == Guid.Empty)
                    .OrderBy(i => i.DisplayOrder)
                    .ToList();

                var result = new List<MenuItemWithRoles>();

                foreach (var item in rootItems)
                {
                    result.Add(await MapToMenuItemWithRoles(item, roles));
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener items raíz con roles: {ex.Message}");
                return new List<MenuItemWithRoles>();
            }
        }

        public async Task UpdateMenuItemPermissionsAsync(Guid menuItemId, List<Guid> roleIds, List<bool> action)
        {
            try
            {
                var menuItem = _menuItems.FirstOrDefault(m => m.IdMenu == menuItemId);

                if (menuItem == null)
                    throw new KeyNotFoundException($"MenuItem {menuItemId} no encontrado");

                // Actualizar RequiredPermissions en memoria
                menuItem.RequiredPermissions ??= new List<Guid>();

                for (int i = 0; i < roleIds.Count; i++)
                {
                    var roleId = roleIds[i];
                    var hasPermission = action[i];

                    if (hasPermission)
                    {
                        // Agregar permiso si no existe
                        if (!menuItem.RequiredPermissions.Contains(roleId))
                            menuItem.RequiredPermissions.Add(roleId);
                    }
                    else
                    {
                        // Remover permiso si existe
                        if (menuItem.RequiredPermissions.Contains(roleId))
                            menuItem.RequiredPermissions.Remove(roleId);
                    }
                }

                menuItem.UpdatedAt = DateTime.UtcNow;

                // Actualizar en Base de Datos
                for (int i = 0; i < roleIds.Count; i++)
                {
                    var roleId = roleIds[i];
                    var hasPermission = action[i];

                    var menuPerm = new MenuPermissions
                    {
                        IdMenu = menuItemId,
                        IdRole = roleId
                    };

                    if (hasPermission)
                    {
                        // Intentar agregar el permiso (manejar si ya existe)
                        try
                        {
                            await ec.AddNewRecordAsync(menuPerm);
                        }
                        catch (Exception ex)
                        {
                            // Posiblemente el permiso ya existe, ignorar el error
                            Console.WriteLine($"Permiso ya existe o error al agregar: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Eliminar el permiso
                        try
                        {
                            await ec.DeleteRecordAsync(menuPerm);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error al eliminar permiso: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine($"Permisos actualizados para menu item {menuItemId}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar permisos para {menuItemId}: {ex.Message}");
                throw;
            }
        }

        // Método adicional útil: Obtener permisos por rol
        public async Task<List<Guid>> GetMenuPermissionsByRoleAsync(Guid roleId)
        {
            try
            {
                // Usar BDLayout para obtener permisos por rol
                var permissions = await ec.GetAllMenuPermissionsAsync();// await ec.GetMenuPermissionsByRoleAsync(roleId);

                return permissions.Select(p => p.IdMenu).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al obtener permisos por rol: {ex.Message}");

                // Datos de ejemplo
                if (roleId.ToString().Contains("admin"))
                    return _menuItems.Select(m => m.IdMenu).ToList();

                return new List<Guid>();
            }
        }

        // Método adicional: Asignar permiso a rol
        public async Task AssignRoleToMenuItemAsync(Guid roleId, Guid menuItemId)
        {
            try
            {
                var menuItem = _menuItems.FirstOrDefault(m => m.IdMenu == menuItemId);

                if (menuItem == null)
                    throw new KeyNotFoundException($"MenuItem {menuItemId} no encontrado");

                var permissions = menuItem.RequiredPermissions ?? new List<Guid>();

                if (!permissions.Contains(roleId))
                {
                    permissions.Add(roleId);
                    menuItem.RequiredPermissions = permissions;
                    menuItem.UpdatedAt = DateTime.UtcNow;

                    // Guardar en BD usando BDLayout
                    var menuPerm = new MenuPermissions();
                    menuPerm.IdMenu = menuItemId;
                    menuPerm.IdRole = roleId;
                    await ec.AddNewRecordAsync(menuPerm);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al asignar permiso: {ex.Message}");
                throw;
            }
        }

        // Método adicional: Remover permiso de rol
        public async Task RemoveRoleFromMenuItemAsync(Guid roleId, Guid menuItemId)
        {
            try
            {
                var menuItem = _menuItems.FirstOrDefault(m => m.IdMenu == menuItemId);

                if (menuItem == null)
                    throw new KeyNotFoundException($"MenuItem {menuItemId} no encontrado");

                var permissions = menuItem.RequiredPermissions ?? new List<Guid>();

                if (permissions.Contains(roleId))
                {
                    permissions.Remove(roleId);
                    menuItem.RequiredPermissions = permissions;
                    menuItem.UpdatedAt = DateTime.UtcNow;

                    // Eliminar de BD usando BDLayout
                    var menuPerm = new MenuPermissions();
                    menuPerm.IdMenu = menuItemId;
                    menuPerm.IdRole = roleId;
                    await ec.DeleteRecordAsync(menuPerm);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al remover permiso: {ex.Message}");
                throw;
            }
        }

        // ============ MÉTODOS PRIVADOS DE APOYO ============

        private async Task<MenuItemWithRoles> MapToMenuItemWithRoles(MenuItemDefinition item, List<RoleDto> roles)
        {
            var itemWithRoles = new MenuItemWithRoles
            {
                IdMenu = item.IdMenu,
                ItemKey = item.ItemKey,
                Title = item.Title,
                Icon = item.Icon,
                Url = item.Url,
                DisplayOrder = item.DisplayOrder,
                IdParent = item.IdParent,
                ParentKey = item.ParentKey,
                Target = item.Target,
                RequiredPermissions = item.RequiredPermissions ?? new List<Guid>(),
                IsVisible = item.IsVisible,
                BadgeText = item.BadgeText,
                BadgeColor = item.BadgeColor,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                RolePermissions = roles.Select(role => new RolePermissionCheck
                {
                    IdRole = role.Id,
                    RoleName = role.Name,
                    CanView = item.RequiredPermissions?.Contains(role.Id) ?? false,
                    IsExpanded = true
                }).ToList()
            };

            // Obtener título del padre si existe
            if (item.IdParent.HasValue && item.IdParent != Guid.Empty)
            {
                var parent = _menuItems.FirstOrDefault(p => p.IdMenu == item.IdParent);
                itemWithRoles.ParentTitle = parent?.Title ?? "Desconocido";
            }

            // Mapear hijos recursivamente
            var children = _menuItems
                .Where(c => c.IdParent == item.IdMenu)
                .OrderBy(c => c.DisplayOrder)
                .ToList();

            foreach (var child in children)
            {
                itemWithRoles.Children.Add(await MapToMenuItemWithRoles(child, roles));
            }

            return itemWithRoles;
        }


    }
}