using SqlSugar;

namespace DynamicDbApi.Models
{
    [SugarTable("Permissions")]
    public class Permission
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        
        public int RoleId { get; set; } // 关联角色ID
        public string DatabaseId { get; set; } = null!; // 数据库ID，*表示所有数据库
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        // 导航属性
        [Navigate(NavigateType.OneToMany, nameof(TablePermission.PermissionId))]
        public List<TablePermission> TablePermissions { get; set; } = new();
    }
}
