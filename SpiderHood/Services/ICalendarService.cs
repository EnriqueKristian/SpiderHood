using Microsoft.EntityFrameworkCore;
using SpiderHood.Data;
using SpiderHood.Models;

namespace SpiderHood.Services
{
    public interface ICalendarService
    {
        Task<List<CalendarItem>> GetByBuildingAsync(Guid idBuilding, DateTime? from = null, DateTime? to = null);
        Task<CalendarItem> GetByIdAsync(Guid idCalendarItem);
        Task<CalendarItem> CreateAsync(CalendarItem item);
        Task UpdateAsync(CalendarItem item);
        Task ChangeStatusAsync(Guid idCalendarItem, CalendarItemStatus status, string modifiedBy);
        Task DeleteAsync(Guid idCalendarItem, bool deleteSeries, string performedBy);
    }

    public class CalendarService : ICalendarService
    {
        // Tope de ocurrencias generadas por serie recurrente -- sin esto, un
        // "Mensual" sin RecurrenceEndDate generaría filas para siempre.
        private const int MaxRecurrenceOccurrences = 60;
        private static readonly TimeSpan MaxRecurrenceSpan = TimeSpan.FromDays(365 * 3);

        private readonly BDLayout ec;
        private readonly IEmailService _emailService;
        private readonly ILogger<CalendarService> _logger;

        public CalendarService(
            IDbContextFactory<SpiderHoodContext> contextFactory,
            IEmailService emailService,
            ILogger<CalendarService> logger)
        {
            ec = new BDLayout(contextFactory);
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<CalendarItem>> GetByBuildingAsync(Guid idBuilding, DateTime? from = null, DateTime? to = null)
            => await ec.GetCalendarItemsByBuildingAsync(idBuilding, from, to);

        public async Task<CalendarItem> GetByIdAsync(Guid idCalendarItem)
            => await ec.GetCalendarItemByIdAsync(idCalendarItem);

        public async Task<CalendarItem> CreateAsync(CalendarItem item)
        {
            if (item.IdCalendarItem == Guid.Empty)
                item.IdCalendarItem = Guid.NewGuid();

            if (item.Recurrence == RecurrenceType.None)
            {
                item.IdRecurrenceGroup = null;
                item.IsRecurrenceMaster = false;
                await ec.AddNewRecordAsync(item);
                await NotifyBuildingAsync(item);
                return item;
            }

            var occurrences = BuildRecurrenceOccurrences(item, Guid.NewGuid());
            foreach (var occurrence in occurrences)
                await ec.AddNewRecordAsync(occurrence);

            // Un solo email por serie (no uno por ocurrencia) -- avisa que se
            // programó, no repite el aviso por cada fecha generada.
            await NotifyBuildingAsync(item);

            return occurrences[0];
        }

        public async Task UpdateAsync(CalendarItem item)
        {
            await ec.UpdateCalendarItemAsync(item);
        }

        public async Task ChangeStatusAsync(Guid idCalendarItem, CalendarItemStatus status, string modifiedBy)
        {
            await ec.UpdateCalendarItemStatusAsync(idCalendarItem, status, modifiedBy);
        }

        public async Task DeleteAsync(Guid idCalendarItem, bool deleteSeries, string performedBy)
        {
            await ec.DeleteCalendarItemAsync(idCalendarItem, deleteSeries);
        }

        private static List<CalendarItem> BuildRecurrenceOccurrences(CalendarItem template, Guid groupId)
        {
            var occurrences = new List<CalendarItem>();
            var duration = template.EndDate.HasValue ? template.EndDate.Value - template.StartDate : (TimeSpan?)null;
            var cutoff = template.RecurrenceEndDate ?? template.StartDate.Add(MaxRecurrenceSpan);
            var currentStart = template.StartDate;
            var isFirst = true;

            while (currentStart <= cutoff && occurrences.Count < MaxRecurrenceOccurrences)
            {
                occurrences.Add(new CalendarItem
                {
                    IdCalendarItem = isFirst ? template.IdCalendarItem : Guid.NewGuid(),
                    IdBuilding = template.IdBuilding,
                    Title = template.Title,
                    Description = template.Description,
                    Type = template.Type,
                    Category = template.Category,
                    StartDate = currentStart,
                    EndDate = duration.HasValue ? currentStart + duration.Value : null,
                    Location = template.Location,
                    Responsible = template.Responsible,
                    Cost = template.Cost,
                    Status = CalendarItemStatus.Scheduled,
                    Recurrence = template.Recurrence,
                    RecurrenceInterval = template.RecurrenceInterval,
                    RecurrenceEndDate = template.RecurrenceEndDate,
                    IdRecurrenceGroup = groupId,
                    IsRecurrenceMaster = isFirst,
                    CreatedBy = template.CreatedBy
                });

                currentStart = Advance(currentStart, template.Recurrence, template.RecurrenceInterval);
                isFirst = false;
            }

            return occurrences;
        }

        private static DateTime Advance(DateTime date, RecurrenceType recurrence, int interval)
        {
            interval = Math.Max(1, interval);
            return recurrence switch
            {
                RecurrenceType.Daily => date.AddDays(interval),
                RecurrenceType.Weekly => date.AddDays(7 * interval),
                RecurrenceType.Monthly => date.AddMonths(interval),
                RecurrenceType.Yearly => date.AddYears(interval),
                _ => throw new InvalidOperationException($"Recurrence type {recurrence} does not generate occurrences")
            };
        }

        // Best effort: un fallo notificando por correo no puede tumbar la
        // creación real del item -- se loguea y sigue (mismo criterio que
        // IncidentService).
        private async Task NotifyBuildingAsync(CalendarItem item)
        {
            try
            {
                var roles = await ec.GetAllUserBuildingRolesAsync();
                var residents = roles
                    .Where(r => r.IdBuilding == item.IdBuilding && r.Role == "Residente")
                    .Select(r => r.UserEmail)
                    .Where(email => !string.IsNullOrWhiteSpace(email))
                    .Distinct();

                var kind = item.Type == CalendarItemType.Maintenance ? "mantenimiento" : "evento";
                var subject = $"Nuevo {kind} programado: {item.Title}";
                var body = $"<p>Se programó un nuevo {kind}: <strong>{item.Title}</strong></p>" +
                           $"<p>Fecha: {item.StartDate:dd/MM/yyyy HH:mm}</p>" +
                           (string.IsNullOrWhiteSpace(item.Description) ? "" : $"<p>{item.Description}</p>");

                foreach (var email in residents)
                {
                    await _emailService.SendEmailAsync(email, subject, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error notificando el item de calendario {IdCalendarItem}", item.IdCalendarItem);
            }
        }
    }
}
