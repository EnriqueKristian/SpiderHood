namespace SpiderHood.Models
{
    public class SocialLoginResult
    {
        public bool Success { get; set; }
        public string Provider { get; set; } = ""; // Google, Facebook
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Token { get; set; } = "";
    }
}
