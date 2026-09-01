namespace SpiderHood.Models
{
    public class InvitationModel
    {
        public Guid IdInvitation { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid IdBuilding { get; set; }
        public string BuildingName { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string InvitedBy { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int ApartmentNumber { get; set; }
        public bool RequiresApproval { get; set; }
        public string AdminMessage { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected, Expired
    }
}
