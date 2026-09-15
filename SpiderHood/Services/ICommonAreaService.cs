using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #21 -- diseño cerrado 2026-09-11.
    // CRUD simple; persistencia propia (tabla CommonArea), no se mete en el
    // Clone()/UPD gigante de BuildingConfiguration (item #24).
    public interface ICommonAreaService
    {
        Task<List<CommonArea>> GetCommonAreasAsync(Guid idBuilding);

        Task<CommonArea> CrearAsync(CommonArea areaComun);

        Task ActualizarAsync(CommonArea areaComun);
    }

    public class CommonAreaService : ICommonAreaService
    {
        private BDLayout ec { get; set; }

        public CommonAreaService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<CommonArea>> GetCommonAreasAsync(Guid idBuilding)
            => await ec.GetCommonAreasByBuildingAsync(idBuilding);

        public async Task<CommonArea> CrearAsync(CommonArea areaComun)
        {
            areaComun.IdCommonArea = Guid.NewGuid();
            areaComun.CreatedOn = DateTime.Now;
            return await ec.AddNewRecordAsync(areaComun);
        }

        public async Task ActualizarAsync(CommonArea areaComun)
            => await ec.UpdateCommonAreaAsync(areaComun);
    }
}
