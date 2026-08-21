// Services/IPermissionService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IPermissionService
    {
        Task<List<MenuItem>> GetMenuForUserAsync(UserSession user);
        Task<bool> HasPermissionAsync(UserSession user, string permissionId);
        Task<bool> HasAnyPermissionAsync(UserSession user, params string[] permissionIds);
        Task<List<string>> GetUserPermissionsAsync(UserSession user);
        Task<bool> CanAccessRouteAsync(UserSession user, string route);
        Task<Dictionary<string, bool>> GetButtonPermissionsAsync(UserSession user, string module);
        Task<List<string>> GetPermissionsForRoleAsync(string role);
        Task RefreshMenu();
    }
    public class PermissionService : IPermissionService
    {
        private readonly AuthService _authService;
        private readonly IConfiguration _configuration;

        // Cacheado por rol (IdRole), no en un solo slot: PermissionService es Scoped por
        // circuito y sobrevive a los cambios de rol dentro de una misma sesión (desde
        // /select-building, el dropdown de edificio del header, o el selector de "Modo
        // Desarrollo"). Con un solo _menuCache, el menú del PRIMER rol cargado quedaba
        // pegado para el resto del circuito — cambiar de rol actualizaba el chip/header
        // pero el listado de opciones del menú nunca se refrescaba.
        private readonly Dictionary<Guid, List<MenuItem>> _menuCacheByRole = new();
        private List<string>? _userPermissionsCache;
        private BDLayout ec { get; set; }

        public PermissionService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService, IConfiguration configuration)
        {
            _authService = authService;
            _configuration = configuration;
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<MenuItem>> GetMenuForUserAsync(UserSession user)
        {
            //var user = await _authService.GetCurrentUserAsync();
            if (user == null) return new List<MenuItem>();

            var userPermissions = await GetUserPermissionsAsync(user);
            var allMenuItems = await GetMenuDefinitionsAsync(user);

            return allMenuItems;
            //return FilterMenuItems(allMenuItems, userPermissions);
        }

        public async Task<bool> HasPermissionAsync(UserSession user, string permissionId)
        {
            var permissions = await GetUserPermissionsAsync(user);
            return permissions.Contains(permissionId);
        }

        public async Task<bool> HasAnyPermissionAsync(UserSession user, params string[] permissionIds)
        {
            var permissions = await GetUserPermissionsAsync(user);
            return permissionIds.Any(p => permissions.Contains(p));
        }

        public async Task<List<string>> GetUserPermissionsAsync(UserSession user)
        {
            if (user?.Role == null) return new List<string>();

            // Obtener permisos según el rol del usuario
            _userPermissionsCache = await GetPermissionsForRoleAsync(user.Role);
            return _userPermissionsCache;
        }

        public async Task<bool> CanAccessRouteAsync(UserSession user, string route)
        {
            var menuItems = await GetMenuForUserAsync(user);
            return CanAccessRouteRecursive(menuItems, route);
        }

        public async Task<Dictionary<string, bool>> GetButtonPermissionsAsync(UserSession user, string module)
        {
            var permissions = await GetUserPermissionsAsync(user);
            var buttonPermissions = new Dictionary<string, bool>();

            // Definir permisos de botones por módulo
            var moduleButtons = GetModuleButtons(module);

            foreach (var button in moduleButtons)
            {
                buttonPermissions[button.Key] = permissions.Contains(button.Value);
            }

            return buttonPermissions;
        }

        #region Private Methods

        private List<MenuItem> FilterMenuItems(List<MenuItem> items, List<string> userPermissions)
        {
            var filtered = new List<MenuItem>();

            foreach (var item in items)
            {
                // Verificar si el usuario tiene permisos para este item
                var hasPermission = !item.RequiredPermissions.Any() ||
                                   item.RequiredPermissions.Any(p => userPermissions.Contains(p));

                if (hasPermission)
                {
                    var filteredItem = new MenuItem
                    {
                        ItemKey = item.ItemKey,
                        Title = item.Title,
                        Icon = item.Icon,
                        Url = item.Url,
                        Order = item.Order,
                        Target = item.Target,
                        Children = FilterMenuItems(item.Children, userPermissions)
                    };

                    // Solo incluir si tiene hijos visibles o es un item visible
                    if (filteredItem.Children.Any() || !string.IsNullOrEmpty(item.Url))
                    {
                        filtered.Add(filteredItem);
                    }
                }
            }

            return filtered.OrderBy(i => i.Order).ToList();
        }

        public async Task RefreshMenu() {
            _menuCacheByRole.Clear();
        }
        private async Task<List<MenuItem>> GetMenuDefinitionsAsync(UserSession user)
        {
            if (_menuCacheByRole.TryGetValue(user.IdRole, out var cached))
                return cached;

            var menu = await ec.GetFullMenuAsync(user.IdRole);
            _menuCacheByRole[user.IdRole] = menu;

            return menu;
        }

        public async Task<List<string>> GetPermissionsForRoleAsync(string role)
        {
            var permitions =  await ec.GetPermissionsForRoleAsync(role);

            return permitions.Select(i => i.PermissionKey.ToString()).ToList()!;


            // Definir permisos por rol
            /*var rolePermissions = new Dictionary<string, List<string>>
            {
                ["Administrador"] = new List<string>
                {
                    "view_dashboard",
                    "manage_buildings", "view_buildings", "view_unitgroups", "view_owners", "assign_units",
                    "manage_budget", "manage_water_readings", "view_budgets", "emit_receipts", "upload_statements",
                    "view_reconciliation", "reconcile_expenses", "reconcile_installments", "view_reconciliation_history",
                    "manage_incidents", "view_all_incidents", "create_announcements", "view_announcements", "view_meetings",
                    "view_reports", "view_income_expenses", "view_delinquency", "view_consumption_report",
                    "view_budget_execution", "export_receipts",
                    "access_settings", "view_profile", "manage_security", "manage_users", "manage_categories",
                    "manage_parameters", "manage_periods", "view_about"
                },
                ["Junta"] = new List<string>
                {
                    "view_dashboard",
                    "board_member", "approve_budget", "authorize_expenses", "schedule_meetings", "manage_minutes",
                    "view_financial_reports", "view_reports", "view_income_expenses", "view_delinquency",
                    "view_profile", "view_about"
                },
                ["Residente"] = new List<string>
                {
                    "view_dashboard",
                    "resident_portal", "view_my_receipts", "view_budget_resident", "view_announcements",
                    "report_incidents", "view_my_consumption", "view_profile", "view_about"
                }
            };*/

            //return rolePermissions; //.ContainsKey(role) ? rolePermissions[role] : new List<string>();
        }

        private Dictionary<string, string> GetModuleButtons(string module)
        {
            return module.ToLower() switch
            {
                "buildings" => new Dictionary<string, string>
                {
                    ["btn_create"] = "create_building",
                    ["btn_edit"] = "edit_building",
                    ["btn_delete"] = "delete_building",
                    ["btn_export"] = "export_buildings"
                },
                "owners" => new Dictionary<string, string>
                {
                    ["btn_create"] = "create_owner",
                    ["btn_edit"] = "edit_owner",
                    ["btn_delete"] = "delete_owner",
                    ["btn_send_email"] = "send_email_owner"
                },
                "incidents" => new Dictionary<string, string>
                {
                    ["btn_create"] = "create_incident",
                    ["btn_edit"] = "edit_incident",
                    ["btn_resolve"] = "resolve_incident",
                    ["btn_assign"] = "assign_incident"
                },
                _ => new Dictionary<string, string>()
            };
        }

        private bool CanAccessRouteRecursive(List<MenuItem> items, string route)
        {
            foreach (var item in items)
            {
                if (item.Url?.Equals(route, StringComparison.OrdinalIgnoreCase) == true)
                    return true;

                if (item.Children.Any() && CanAccessRouteRecursive(item.Children, route))
                    return true;
            }
            return false;
        }

        #endregion
    }
}