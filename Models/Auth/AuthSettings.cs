namespace DynamicDbApi.Models.Auth
{
    public class AuthSettings
    {
        public string DefaultAuthentication { get; set; } = "Jwt";
        public List<string> AllowedAuthentications { get; set; } = new List<string> { "Jwt", "OAuth2", "WindowsAD" };
    }
}
