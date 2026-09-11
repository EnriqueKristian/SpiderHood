using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    // Docs/Pendientes-Negocio-Consolidado.md #21 -- diseño cerrado 2026-09-11.
    // CRUD simple; persistencia propia (tabla AreaComun), no se mete en el
    // Clone()/UPD gigante de BuildingConfiguration (item #24).
    public interface IAreaComunService
    {
        Task<List<AreaComun>> GetAreaComunesAsync(Guid idBuilding);

        Task<AreaComun> CrearAsync(AreaComun areaComun);

        Task ActualizarAsync(AreaComun areaComun);
    }

    public class AreaComunService : IAreaComunService
    {
        private BDLayout ec { get; set; }

        public AreaComunService(IDbContextFactory<SpiderHoodContext> contextFactory)
        {
            ec = new BDLayout(contextFactory);
        }

        public async Task<List<AreaComun>> GetAreaComunesAsync(Guid idBuilding)
            => await ec.GetAreaComunesByBuildingAsync(idBuilding);

        public async Task<AreaComun> CrearAsync(AreaComun areaComun)
        {
            areaComun.IdAreaComun = Guid.NewGuid();
            areaComun.CreatedOn = DateTime.Now;
            return await ec.AddNewRecordAsync(areaComun);
        }

        public async Task ActualizarAsync(AreaComun areaComun)
            => await ec.UpdateAreaComunAsync(areaComun);
    }
}
