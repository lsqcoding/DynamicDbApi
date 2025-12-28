namespace DynamicDbApi.Models.Auth
{
    public class WindowsADSettings
    {
        public string Domain { get; set; } = string.Empty;
        public string LdapPath { get; set; } = string.Empty;
        public string GroupSearchBase { get; set; } = string.Empty;
        public bool AllowFormsAuth { get; set; } = true;
        // 添加以下字段用于域管理账号认证
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }
}
