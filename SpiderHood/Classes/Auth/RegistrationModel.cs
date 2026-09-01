namespace SpiderHood.Models
{
    public class RegistrationModel
    {
        // Información personal
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public bool AcceptTerms { get; set; }

        // Información del edificio (para administrador)
        public string BuildingName { get; set; } = "";
        public string Address { get; set; } = "";
        public string City { get; set; } = "";
        public int TotalApartments { get; set; } = 1;
        public int Floors { get; set; } = 1;
        public int ConstructionYear { get; set; } = DateTime.Now.Year;
        public string BuildingType { get; set; } = "";
        public string BuildingDescription { get; set; } = "";
    }
}
