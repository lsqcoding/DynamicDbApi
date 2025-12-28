namespace DynamicDbApi.Models.Auth
{
    public class OAuth2Settings
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = "/signin-oidc";
        public List<string> Scopes { get; set; } = new List<string> { "openid", "profile", "email" };
    }
}
