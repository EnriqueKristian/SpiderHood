namespace SpiderHood.Models
{
    public class AuthResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public UserModel? User { get; set; }
        public string Token { get; set; } = "";
        public bool RequiresApproval { get; set; }
    }
}
