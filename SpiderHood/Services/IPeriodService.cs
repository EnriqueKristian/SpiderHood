using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface IPeriodService
    {
        Task<IEnumerable<Models.Period>> GetPeriodsByBuildingAsync(Guid IdBuilding);
        Task<Models.Period> GetPeriodByIdAsync(Guid id);
        Task<Models.Period> GetCurrentPeriodAsync(Guid buildingId);
        Task<bool> CreatePeriodAsync(Models.Period period);
        Task<bool> UpdatePeriodAsync(Models.Period period);
        Task<bool> DeletePeriodAsync(Guid id);
        Task<bool> SetAsCurrentPeriodAsync(Guid periodId, Guid buildingId);
        Task<bool> ValidatePeriodDatesAsync(Guid buildingId, DateTime startDate, DateTime endDate, Guid? excludeId = null);
    }

    public class PeriodService : IPeriodService
    {
        private BDLayout ec { get; set; }
        private readonly AuthService _authService;

        public PeriodService(IDbContextFactory<SpiderHoodContext> contextFactory, AuthService authService)
        {
            ec = new BDLayout(contextFactory);
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        private async Task<string> GetPerformedByAsync()
        {
            var user = await _authService.GetCurrentUserAsync();
            return user?.Email ?? "system";
        }

        public async Task<IEnumerable<Models.Period>> GetPeriodsByBuildingAsync(Guid IdBuilding)
        {
            return await ec.GetPeriodsByBuildingAsync(IdBuilding);
        }

        public async Task<Models.Period> GetPeriodByIdAsync(Guid id)
        {
            /*return await _context.Periods
                .FirstOrDefaultAsync(p => p.IdPeriod == id);*/
            return new Models.Period();
        }

        public async Task<Models.Period> GetCurrentPeriodAsync(Guid buildingId)
        {
            /*return await _context.Periods
                .FirstOrDefaultAsync(p => p.IdBuilding == buildingId && p.IsCurrentPeriod && p.Status == 1);*/
            return new Models.Period();
        }

        public async Task<bool> CreatePeriodAsync(Models.Period period)
        {
            try
            {
                // Validar que no haya superposición de fechas
                var hasOverlap = await ec.CheckPeriodOverlapAsync(period);

                if (hasOverlap)
                {
                    throw new Exception("El periodo se superpone con otro periodo existente");
                }

                //_context.Periods.Add(period);
                await ec.AddNewRecordAsync(period);
                await ec.StampAuditAsync(AuditableEntity.Period, period.IdPeriod, await GetPerformedByAsync(), isCreate: true);

                // Si este periodo es el actual, desmarcar los demás
                if (period.IsCurrentPeriod)
                {
                    await UnsetOtherCurrentPeriods(period.IdBuilding, period.IdPeriod);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> UpdatePeriodAsync(Models.Period period)
        {
            try
            {
                // Validar que no haya superposición de fechas con otro periodo (el propio
                // IdPeriod que se le pasa a CHK_Period_CheckOverlap lo excluye de sí mismo).
                var hasOverlap = await ec.CheckPeriodOverlapAsync(period);

                if (hasOverlap)
                {
                    throw new Exception("El periodo se superpone con otro periodo existente");
                }

                await ec.UpdateRecordAsync(period);
                await ec.StampAuditAsync(AuditableEntity.Period, period.IdPeriod, await GetPerformedByAsync(), isCreate: false);

                // Si este periodo pasa a ser el actual, desmarcar los demás
                if (period.IsCurrentPeriod)
                {
                    await UnsetOtherCurrentPeriods(period.IdBuilding, period.IdPeriod);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> DeletePeriodAsync(Guid id)
        {
            try
            {
                await ec.DeleteRecordAsync(new Models.Period { IdPeriod = id });
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SetAsCurrentPeriodAsync(Guid periodId, Guid buildingId)
        {
            try
            {
                await UnsetOtherCurrentPeriods(buildingId, periodId);
                await ec.SetPeriodAsCurrentAsync(periodId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ValidatePeriodDatesAsync(Guid buildingId, DateTime startDate, DateTime endDate, Guid? excludeId = null)
        {
            /*var query = _context.Periods
                .Where(p => p.IdBuilding == buildingId &&
                           ((p.StartDate <= endDate && p.EndDate >= startDate) ||
                            (startDate <= p.EndDate && endDate >= p.StartDate)));

            if (excludeId.HasValue)
            {
                query = query.Where(p => p.IdPeriod != excludeId.Value);
            }

            return !await query.AnyAsync();*/
            return false;
        }

        private async Task UnsetOtherCurrentPeriods(Guid IdBuilding, Guid IdcurrentPeriod)
        {
            await ec.UnsetOtherCurrentPeriodsAsync(IdBuilding, IdcurrentPeriod);
        }
    }
}