namespace SpiderHood.Models
{
    // Solo aplica a CalendarItem.Type == CalendarItemType.Maintenance -- para
    // Type == Event queda null.
    public enum MaintenanceCategory
    {
        Elevator,
        Pumps,
        Cleaning,
        Gardening,
        Security,
        Electrical,
        Plumbing,
        General
    }
}
