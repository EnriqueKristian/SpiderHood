namespace SpiderHood.Models
{
    public class NewCalculation
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public string BuildingName { get; set; } = "";
        public int TotalApartments { get; set; } = 30;
        public string Template { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
