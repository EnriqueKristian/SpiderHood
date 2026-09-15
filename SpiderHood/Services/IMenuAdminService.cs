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

        // Un item raíz puede tener IdParent en NULL (todos los INS_MenuItem del
        // repo lo pasan así por default, ej. Database/Scripts/2026-09-02_05_Incidents.sql
        // y los del módulo Personal y Planillas) o en Guid.Empty (lo que graba
        // MenuItemForm.razor cuando el usuario elige "--- Raíz del menú ---" del
        // combo, ver GetAvailableParentsAsync). Antes de este fix, cada método de
        // este archivo comparaba sólo contra UNA de las dos convenciones -- según
        // cuál, un item raíz sembrado por script (IdParent NULL) directamente no
        // aparecía en "Administrar Menú" (ni como raíz ni como opción de padre
        // para crearle hijos), mientras que uno creado desde la propia UI
        // (IdParent Guid.Empty) sí. Centralizado acá para que las dos
        // convenciones se traten siempre igual.
        private static bool IsRootMenuItem(Guid? idParent) => idParent == null || idParent == Guid.Empty;

        // FK_MenuItems_Parent (IdParent -> MenuItems.IdMenu, self-referencing) sólo
        // deja pasar NULL para "sin padre" -- Guid.Empty no matchea ninguna fila y
        // SIEMPRE revienta con "The UPDATE/INSERT statement conflicted with the
        // FOREIGN KEY... column 'IdMenu'" (SQL Error 547), que termina el circuito
        // de Blazor sin ningún mensaje útil para el usuario. GetAvailableParentsAsync
        // arriba ofrece Guid.Empty como el value del combo "--- Raíz del menú ---"
        // (ver MenuItemForm.razor), así que cualquier alta/edición que se deje ese
        // combo en su default llega acá con IdParent = Guid.Empty. Se normaliza a
        // null justo antes de persistir -- IsRootMenuItem ya trata ambos por igual,
        // así que esto no cambia ninguna lógica en memoria.
        private static Guid? NormalizeIdParent(Guid? idParent) => idParent == Guid.Empty ? null : idParent;

        private void BuildHierarchy()
        {
            foreach (var item in _menuItems)
            {
                if (!IsRootMenuItem(item.IdParent))
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
                .Where(i => IsRootMenuItem(i.IdParent))
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

        // Antes era un método "sync" que envolvía cada llamada a BDLayout en `_ =
        // ec.XxxAsync(...)` (fire-and-forget) en vez de awaitearla -- el método
        // devolvía el item ya "guardado" al caller (y la UI mostraba éxito) sin
        // esperar a que ninguna de esas escrituras hubiera terminado siquiera, ni
        // observar si alguna fallaba (una excepción en una Task no observada se
        // pierde en silencio). Eso es lo que hacía que "a veces" el guardado no
        // se reflejara en SQL Server -- una simple condición de carrera, no algo
        // determinístico, por eso era intermitente.
        public async Task<MenuItemWithRoles> CreateMenuItemAsync(MenuItemWithRoles item)
        {
            item.IdMenu = Guid.NewGuid();
            item.CreatedAt = DateTime.UtcNow;
            item.IdParent = NormalizeIdParent(item.IdParent);

            // Generar Target para items raíz (con hijos) -- ver IsRootMenuItem.
            if (IsRootMenuItem(item.IdParent))
            {
                item.Target = $"menu-{item.IdMenu.ToString().ToLower()}";
            }

            _menuItems.Add(item);

            await ec.AddNewRecordAsync(item);

            // Antes de insertar los permisos nuevos, se limpia cualquier fila vieja
            // para este IdMenu (no debería haber ninguna en un alta, pero por las
            // dudas si se reintentó tras un error parcial) -- ver
            // DEL_MenuItemPermissionsByMenu.
            await ec.DeleteMenuItemPermissionsByMenuAsync(item.IdMenu);

            foreach (var perm in item.RequiredPermissions)
            {
                await ec.AddNewRecordAsync(new MenuPermissions { IdMenu = item.IdMenu, IdRole = perm });
            }

            return item;
        }

        // Mismo fix que CreateMenuItemAsync arriba. El bug más serio acá era el
        // borrado de permisos "viejos" antes de re-insertar los nuevos: se armaba
        // un MenuPermissions con sólo IdMenu seteado (IdRole quedaba en
        // Guid.Empty, el default de C#) y DEL_MenuItemPermission borra por
        // (IdMenu, IdRole) EXACTO -- esa fila nunca existía, así que el borrado
        // nunca borraba nada de verdad. Resultado: destildar un rol y guardar
        // nunca le sacaba el acceso a ese rol (sólo se podían AGREGAR roles
        // nuevos, nunca quitar uno existente), sin ningún error visible.
        public async Task UpdateMenuItemAsync(MenuItemWithRoles item)
        {
            var existing = _menuItems.FirstOrDefault(i => i.IdMenu == item.IdMenu);
            if (existing == null)
                return;

            item.IdParent = NormalizeIdParent(item.IdParent);

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

            // Actualizar Target si es necesario -- ver IsRootMenuItem.
            existing.Target = IsRootMenuItem(existing.IdParent)
                ? $"menu-{existing.IdMenu.ToString().ToLower()}"
                : null;

            await ec.UpdateRecordAsync(item);

            await ec.DeleteMenuItemPermissionsByMenuAsync(item.IdMenu);

            foreach (var perm in item.RequiredPermissions)
            {
                await ec.AddNewRecordAsync(new MenuPermissions { IdMenu = item.IdMenu, IdRole = perm });
            }
        }

        public async Task DeleteMenuItemAsync(Guid id)
        {
            // Antes esto sólo sacaba el item de la lista en memoria (_menuItems) --
            // nunca llegaba a borrar nada en SQL Server (DEL_MenuItem), así que el
            // item reaparecía en el próximo login/circuito, que vuelve a leer todo
            // desde la base en el constructor (InitializeMenuData).
            await ec.DeleteMenuItemAsync(id);

            // Eliminar también todos los hijos (mismo alcance que ya tenía DEL_MenuItem)
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
        }

        public Task<List<MenuItemWithRoles>> GetAvailableParentsAsync(Guid? currentItemId = null)
        {
            var parents = _menuItems
                .Where(i => IsRootMenuItem(i.IdParent) && i.IdMenu != currentItemId)
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

        // Bug encontrado 2026-09-15: esto sólo tocaba el DisplayOrder en memoria
        // (_menuItems) -- nunca llamaba a BDLayout, así que "Guardar orden" en
        // Administrar Menú mostraba éxito pero el reordenamiento se perdía apenas
        // terminaba el circuito (nuevo login/F5). Ahora persiste cada item vía
        // UPD_MenuItem -- que es un UPDATE de fila completa, así que hay que
        // mandar el objeto ENTERO tal como está en _menuItems (con su Title/Url/
        // Icon/etc. reales), no el objeto sparse que manda la página (que sólo
        // trae IdMenu + DisplayOrder) -- mandar ese de acá directo habría
        // vaciado esas columnas en SQL Server.
        //
        // Segundo bug encontrado el mismo día: esto usaba "i + 1" (la posición del
        // item dentro de la lista) en vez de items[i].DisplayOrder (el valor que el
        // usuario efectivamente escribió en el campo "Orden" y que MenuItems.razor
        // manda acá) -- como editar el número no cambia la posición del row en la
        // lista, el valor tecleado se descartaba en silencio y siempre quedaba
        // renumerado 1..N según el orden en pantalla. Este era exactamente el
        // síntoma reportado: "no hace caso a lo que se envía".
        public async Task ReorderMenuAsync(List<MenuItemWithRoles> items)
        {
            foreach (var incoming in items)
            {
                var item = _menuItems.FirstOrDefault(x => x.IdMenu == incoming.IdMenu);
                if (item == null)
                    continue;

                item.DisplayOrder = incoming.DisplayOrder;
                item.UpdatedAt = DateTime.UtcNow;
                item.IdParent = NormalizeIdParent(item.IdParent);
                await ec.UpdateRecordAsync(item);
            }
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

        // Antes, "agregar" un permiso era un INSERT con un try/catch que asumía
        // que cualquier excepción significaba "ya existe, ignorar" -- pero
        // dbo.MenuPermissions no tenía ninguna restricción UNIQUE (arreglado en
        // Database/Scripts/2026-09-15_112_Fix_MenuAdmin_Permissions.sql), así que
        // un INSERT repetido nunca fallaba: creaba una fila duplicada en
        // silencio. Y si el INSERT fallaba por un motivo real (timeout, conexión
        // caída), ese catch lo tragaba igual, la UI mostraba "guardado" y el
        // cambio real nunca había llegado a SQL Server. Acá se borra primero
        // (idempotente: no falla si no existía) y recién después se inserta si
        // corresponde -- sin depender de que una excepción signifique lo que uno
        // cree que significa, y sin tragarse errores reales.
        public async Task UpdateMenuItemPermissionsAsync(Guid menuItemId, List<Guid> roleIds, List<bool> action)
        {
            var menuItem = _menuItems.FirstOrDefault(m => m.IdMenu == menuItemId);
            if (menuItem == null)
                throw new KeyNotFoundException($"MenuItem {menuItemId} no encontrado");

            menuItem.RequiredPermissions ??= new List<Guid>();

            for (int i = 0; i < roleIds.Count; i++)
            {
                var roleId = roleIds[i];
                var hasPermission = action[i];

                await ec.DeleteRecordAsync(new MenuPermissions { IdMenu = menuItemId, IdRole = roleId });

                if (hasPermission)
                {
                    await ec.AddNewRecordAsync(new MenuPermissions { IdMenu = menuItemId, IdRole = roleId });

                    if (!menuItem.RequiredPermissions.Contains(roleId))
                        menuItem.RequiredPermissions.Add(roleId);
                }
                else
                {
                    menuItem.RequiredPermissions.Remove(roleId);
                }
            }

            menuItem.UpdatedAt = DateTime.UtcNow;
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