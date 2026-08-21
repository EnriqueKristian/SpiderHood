// Services/IPermissionAdminService.cs
using Microsoft.EntityFrameworkCore;
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
        private List<PermissionDefinition> _allPermissions = new();
        private BDLayout ec { get; set; }


        public PermissionAdminService(IDbContextFactory<SpiderHoodContext> contextFactory, IConfiguration configuration, AuthService authService, IPermissionService permissionService)
        {
            _configuration = configuration;
            _authService = authService;
            _permissionService = permissionService;
            ec = new BDLayout(contextFactory);
        }

        private async Task<List<PermissionDefinition>> GetAllPermissionDefinitionsAsync()
        {
            return await ec.GetAllPermissionsAsync();
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            List<Role> list = new();

            list = await ec.GetAllRolesAsync();

            foreach (var rol in list)
            {
                rol.Permissions = await _permissionService.GetPermissionsForRoleAsync(rol.RoleName);
            }

            return list;
        }

        public async Task<Role?> GetRoleByIdAsync(Guid id)
        {
            var role = await ec.GetRoleByIdAsync(id);
            return role.FirstOrDefault();
        }

        public async Task<Role> CreateRoleAsync(Role role)
        {
            role.IdRole = Guid.NewGuid();
            role.CreatedAt = DateTime.UtcNow;
            role.IsSystem = false;
            await ec.AddNewRecordAsync(role);
            return role;
        }

        public async Task UpdateRoleAsync(Role role)
        {
            await ec.UpdateRecordAsync(role);
        }

        public async Task DeleteRoleAsync(Guid id)
        {
            var role = await ec.GetRoleByIdAsync(id);
            var existing = role.FirstOrDefault();
            if (existing == null)
                return;

            if (existing.IsSystem)
                throw new InvalidOperationException("No se puede eliminar un rol de sistema.");

            await ec.DeleteRecordAsync(existing);
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

        public async Task AssignPermissionsToRoleAsync(Guid roleId, List<Guid> permissionIds, List<string> permissionkeys)
        {
            var role = await ec.GetRoleByIdAsync(roleId);
            if (role.FirstOrDefault() == null)
                throw new InvalidOperationException("El rol especificado no existe.");

            await ec.DeleteRolePermissionsByRoleAsync(roleId);

            foreach (var permissionId in permissionIds)
            {
                await ec.AddNewRecordAsync(new RolePermissions
                {
                    IdRole = roleId,
                    IdPermission = permissionId
                });
            }
        }

        public async Task<List<RoleAssignment>> GetUserRoleAssignmentsAsync()
        {
            var assignments = await ec.GetAllUsersWithRolesAsync();
            var roles = await ec.GetAllRolesAsync();

            foreach (var assignment in assignments)
            {
                assignment.AvailableRoles = roles;
            }

            return assignments;
        }

        public async Task AssignRoleToUserAsync(Guid userId, Guid roleId)
        {
            await ec.DeleteUserRoleByUserAsync(userId);
            await ec.AddUserRoleAsync(userId, roleId);
        }

        public async Task<List<string>> GetUserPermissionsAsync(Guid userId)
        {
            var role = await ec.GetRoleByUserIdAsync(userId);
            if (role == null)
                return new List<string>();

            return await _permissionService.GetPermissionsForRoleAsync(role.RoleName);
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