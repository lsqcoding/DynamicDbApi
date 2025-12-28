using SqlSugar;

namespace DynamicDbApi.Models
{
    [SugarTable("Users")]
    public class User
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        [SugarColumn(IsIgnore = true)] // 不再使用此字段，通过UserRoles关联表获取角色
        public string? Role { get; set; }
        public string? Email { get; set; }

        /// <summary>
        /// 导航属性：用户关联的角色
        /// </summary>
        [Navigate(typeof(UserRole), nameof(UserRole.UserId), nameof(UserRole.RoleId))]
        public List<Role> Roles { get; set; } = new List<Role>();
    }
}