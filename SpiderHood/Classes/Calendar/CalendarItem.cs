using Microsoft.EntityFrameworkCore;

namespace SpiderHood.Models
{
    // Item de Calendario -- Evento o Mantenimiento, un solo modelo distinguido
    // por Type. Category/Cost/Responsible solo tienen sentido para Maintenance,
    // pero quedan en la misma entidad (igual criterio que Incident, que también
    // mezcla campos que no todos los Type usan).
    //
    // Recurrencia: al crear un item con Recurrence != None, el service genera
    // varias filas (una por ocurrencia) compartiendo IdRecurrenceGroup -- no hay
    // expansión "virtual" en tiempo de lectura. Editar/borrar operan sobre una
    // fila puntual, salvo el borrado de serie (ver ICalendarService.DeleteAsync)
    // que borra esta ocurrencia y las futuras del mismo grupo.
    public class CalendarItem
    {
        public Guid IdCalendarItem { get; set; }
        public Guid IdBuilding { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public CalendarItemType Type { get; set; } = CalendarItemType.Maintenance;
        public MaintenanceCategory? Category { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Location { get; set; } = string.Empty;
        public string Responsible { get; set; } = string.Empty;
        [Precision(18, 2)]
        public decimal? Cost { get; set; }
        public CalendarItemStatus Status { get; set; } = CalendarItemStatus.Scheduled;

        public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
        public int RecurrenceInterval { get; set; } = 1;
        public DateTime? RecurrenceEndDate { get; set; }
        public Guid? IdRecurrenceGroup { get; set; }
        public bool IsRecurrenceMaster { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedOn { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
}
