namespace SpiderHood.Models
{
    public class ToastMessage
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public ToastType Type { get; set; }
        public int Duration { get; set; } = 3000;
    }
}
