// Services/IPermissionAdminService.cs
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IPermissionAdminService
    {
        Task<List<Role>> GetAllRolesAsync();
        Task<Role?> GetRoleByIdAsync(Guid id);
        Task<Role> CreateRoleAsync(Role role);
        Task UpdateRoleAsync(Role role);
        Task DeleteRoleAsync(Guid id);
        Task<List<PermissionGroup>> GetAllPermissionsAsync();
        Task AssignPermissionsToRoleAsync(Guid roleId, List<Guid> permissionIds, List<string> permissionkeys);
        Task<List<RoleAssignment>> GetUserRoleAssignmentsAsync();
        Task AssignRoleToUserAsync(Guid userId, Guid roleId);
        Task<List<string>> GetUserPermissionsAsync(Guid userId);
    }

    public class PermissionAdminService : IPermissionAdminService
    {
        private readonly IConfiguration _configuration;
        private readonly AuthService _authService;
        private readonly IPermissionService _permissionService;
        private List<Role> _roles = new(); // En producción, esto vendría de BD
        private List<PermissionDefinition> _allPermissions = new();
        public SpiderHoodContext _context = default!;
        private BDLayout ec { get; set; }


        public PermissionAdminService(SpiderHoodContext context, IConfiguration configuration, AuthService authService, IPermissionService permissionService)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration;
            _authService = authService;
            _permissionService = permissionService;
            ec = new BDLayout(context);
            //_ = InitializeDataAsync();
        }

        private async Task InitializeDataAsync()
        {
            // Inicializar permisos del sistema
            _allPermissions = await GetAllPermissionDefinitionsAsync();

            _roles = await GetAllRolesAsync();
        }

        private async Task<List<PermissionDefinition>> GetAllPermissionDefinitionsAsync()
        {
            return await ec.GetAllPermissionsAsync();
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            List<Role> list = new();

            list = await ec.GetAllRolesAsync();

            foreach (var rol in list) { 
                rol.Permissions = await _permissionService.GetPermissionsForRoleAsync(rol.RoleName);
            }

            return list;
        }

        public async Task<Role?> GetRoleByIdAsync(Guid id)
        {
            var role = await ec.GetRoleByIdAsync(id);
            return role.FirstOrDefault();
        }

        public Task<Role> CreateRoleAsync(Role role)
        {
            role.IdRole = Guid.NewGuid();
            role.CreatedAt = DateTime.UtcNow;
            role.IsSystem = false;
            _roles.Add(role);
            return Task.FromResult(role);
        }

        public Task UpdateRoleAsync(Role role)
        {
            var existing = _roles.FirstOrDefault(r => r.IdRole == role.IdRole);
            if (existing != null)
            {
                existing.RoleName = role.RoleName;
                existing.Description = role.Description;
                existing.Permissions = role.Permissions;
            }
            return Task.CompletedTask;
        }

        public Task DeleteRoleAsync(Guid id)
        {
            var role = _roles.FirstOrDefault(r => r.IdRole == id);
            if (role != null && !role.IsSystem)
            {
                _roles.Remove(role);
            }
            return Task.CompletedTask;
        }

        public async Task<List<PermissionGroup>> GetAllPermissionsAsync()
        {

            _allPermissions = await GetAllPermissionDefinitionsAsync();

            var groups = _allPermissions
                .GroupBy(p => p.Group)
                .Select(g => new PermissionGroup
                {
                    Module = g.Key,
                    ModuleDisplayName = GetModuleDisplayName(g.Key),
                    Icon = GetModuleIcon(g.Key),
                    Permissions = g.OrderBy(p => p.Name).ToList()
                })
                .OrderBy(g => g.ModuleDisplayName)
                .ToList();

            return groups; // Task.FromResult(groups);
        }

        public Task AssignPermissionsToRoleAsync(Guid roleId, List<Guid> permissionIds, List<string> permissionkeys)
        {
            var role = _roles.FirstOrDefault(r => r.IdRole == roleId);
            if (role != null)
            {
                role.Permissions = permissionkeys;

                RolePermissions permissions = new();

                foreach (var permission in permissionIds) {
                    permissions.IdRole = roleId;
                    permissions.IdPermission = permission;

                    _ = ec.AddNewRecordAsync(permissions);
                }

            }
            //Insertar los Permission to Role
            return Task.CompletedTask;
        }

        public Task<List<RoleAssignment>> GetUserRoleAssignmentsAsync()
        {
            // En producción, esto vendría de la base de datos
            var users = new List<UserSession>
            {
                new() { IdUser = Guid.NewGuid(), Email = "admin@example.com", UserName = "Admin User", Role = "Admin" },
                new() { IdUser = Guid.NewGuid(), Email = "juan@example.com", UserName = "Juan Pérez", Role = "Residente" },
                new() { IdUser = Guid.NewGuid(), Email = "maria@example.com", UserName = "Maria García", Role = "Junta" }
            };

            var assignments = users.Select(u => new RoleAssignment
            {
                IdUser = u.IdUser,
                UserEmail = u.Email,
                UserName = u.UserName,
                CurrentRole = u.Role,
                AvailableRoles = _roles.Select(r => r.RoleName).ToList()
            }).ToList();

            return Task.FromResult(assignments);
        }

        public Task AssignRoleToUserAsync(Guid userId, Guid roleId)
        {
            // En producción, actualizar en base de datos
            return Task.CompletedTask;
        }

        public Task<List<string>> GetUserPermissionsAsync(Guid userId)
        {
            // En producción, obtener de BD
            var user = new UserSession { IdUser  = userId, Role = "Admin" };
            var role = _roles.FirstOrDefault(r => r.RoleName == user.Role);
            return Task.FromResult(role?.Permissions ?? new List<string>());
        }

        private string GetModuleDisplayName(string module)
        {
            return module switch
            {
                "dashboard" => "Dashboard",
                "building" => "Gestión de Edificios",
                "budget" => "Presupuesto y Finanzas",
                "resident" => "Portal del Residente",
                "board" => "Junta Directiva",
                "incidents" => "Incidentes y Comunicados",
                "reports" => "Reportes",
                "settings" => "Configuración",
                _ => module
            };
        }

        private string GetModuleIcon(string module)
        {
            return module switch
            {
                "dashboard" => "fas fa-chart-line",
                "building" => "fas fa-building",
                "budget" => "fas fa-file-invoice-dollar",
                "resident" => "fas fa-house-user",
                "board" => "fas fa-user-tie",
                "incidents" => "fas fa-message",
                "reports" => "fas fa-chart-pie",
                "settings" => "fas fa-gear",
                _ => "fas fa-cog"
            };
        }
    }
}