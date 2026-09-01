namespace SpiderHood.Models
{
    public class RegistrationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string BuildingName { get; set; } = "";
    }
}
