using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Constitución de la Junta Directiva (Docs/Pendientes-Negocio-Consolidado.md #34).
    // Decisiones confirmadas por el usuario (2026-09-22): sólo el Administrador
    // registra/edita (mismo permiso edit_building que ya gatea el resto de
    // BuildingPage.razor, sin permiso nuevo); la pertenencia a Owner se valida como
    // advertencia blanda, no como bloqueo -- ver CheckOwnerWarningAsync.
    public interface IBuildingBoardService
    {
        // Null si el edificio nunca constituyó una Junta -- estado válido.
        Task<BuildingBoard?> GetActiveBoardAsync(Guid idBuilding);

        Task<List<BuildingBoardMemberView>> GetMembersAsync(Guid idBuildingBoard);

        // Usuarios con Rol "Junta" aprobado en este edificio -- son los únicos que
        // tiene sentido ofrecer para un cargo (el Rol ya lo asigna el Administrador
        // desde /Settings/UserRoles; constituir la Junta no asigna el Rol, sólo el
        // cargo dentro de ella).
        Task<List<UserBuildingRoleAssignment>> GetEligibleUsersAsync(Guid idBuilding);

        // Cierra cualquier Junta previamente activa del edificio (ver INS_BuildingBoard)
        // y crea una nueva, vacía -- los miembros se agregan aparte con AddMemberAsync.
        Task<BuildingBoard> CreateBoardAsync(Guid idBuilding, DateTime fechaInicio, Guid performedBy);

        Task<BuildingBoardMember> AddMemberAsync(Guid idBuildingBoard, Guid idUser, BoardMemberRole cargo, string? otroDescripcion);

        Task RemoveMemberAsync(Guid idBuildingBoardMember);

        // Advertencia blanda (Docs/Pendientes-Negocio-Consolidado.md #34) -- no hay
        // vínculo User-Owner explícito en el modelo de datos todavía, así que esto
        // compara por Email entre el User y los Owners activos del edificio (dato
        // que sí existe hoy en ambos lados). Un false NO bloquea nada -- sólo dispara
        // el aviso en la UI ("no se encontró un propietario con este email").
        Task<bool> CheckOwnerWarningAsync(Guid idBuilding, Guid idUser);
    }

    public class BuildingBoardService : IBuildingBoardService
    {
        private BDLayout Ec { get; }

        public BuildingBoardService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            Ec = new BDLayout(contextFactory);
        }

        public async Task<BuildingBoard?> GetActiveBoardAsync(Guid idBuilding)
        {
            return await Ec.GetActiveBuildingBoardAsync(idBuilding);
        }

        public async Task<List<BuildingBoardMemberView>> GetMembersAsync(Guid idBuildingBoard)
        {
            return await Ec.GetBuildingBoardMembersAsync(idBuildingBoard);
        }

        public async Task<List<UserBuildingRoleAssignment>> GetEligibleUsersAsync(Guid idBuilding)
        {
            var roles = await Ec.GetAllUserBuildingRolesAsync();
            return roles
                .Where(r => r.IdBuilding == idBuilding && r.Role == "Junta" && r.IsApproved)
                .OrderBy(r => r.UserName)
                .ToList();
        }

        public async Task<BuildingBoard> CreateBoardAsync(Guid idBuilding, DateTime fechaInicio, Guid performedBy)
        {
            var board = new BuildingBoard
            {
                IdBuildingBoard = Guid.NewGuid(),
                IdBuilding = idBuilding,
                FechaInicio = fechaInicio,
                IsActive = true,
                CreatedBy = performedBy,
                CreatedOn = DateTime.UtcNow,
            };
            return await Ec.CreateBuildingBoardAsync(board);
        }

        public async Task<BuildingBoardMember> AddMemberAsync(Guid idBuildingBoard, Guid idUser, BoardMemberRole cargo, string? otroDescripcion)
        {
            var member = new BuildingBoardMember
            {
                IdBuildingBoardMember = Guid.NewGuid(),
                IdBuildingBoard = idBuildingBoard,
                IdUser = idUser,
                Cargo = cargo,
                // Sólo tiene sentido con Otro -- se limpia para cualquier otro cargo, mismo
                // criterio que Account.LegalRepresentative con AccountType.
                OtroDescripcion = cargo == BoardMemberRole.Otro ? otroDescripcion : null,
            };
            return await Ec.AddBuildingBoardMemberAsync(member);
        }

        public async Task RemoveMemberAsync(Guid idBuildingBoardMember)
        {
            await Ec.DeleteBuildingBoardMemberAsync(idBuildingBoardMember);
        }

        public async Task<bool> CheckOwnerWarningAsync(Guid idBuilding, Guid idUser)
        {
            // Advertencia blanda -- cualquier problema resolviendo el email (usuario no
            // encontrado, lo que no debería pasar dado que idUser sale de
            // GetEligibleUsersAsync, pero GetUserByIdAsync tira si no lo encuentra)
            // se trata como "no se pudo confirmar propietario", nunca bloquea nada.
            UserModel user;
            try
            {
                user = await Ec.GetUserByIdAsync(idUser);
            }
            catch (EntityNotFoundException)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(user.Email))
                return false;

            var owners = await Ec.GetOwnersByBuildingAsync(idBuilding);
            return owners.Any(o => o.IsActive && string.Equals(o.Email, user.Email, StringComparison.OrdinalIgnoreCase));
        }
    }
}
