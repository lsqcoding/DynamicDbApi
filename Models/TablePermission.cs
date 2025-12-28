using SqlSugar;

namespace DynamicDbApi.Models
{
    [SugarTable("TablePermissions")]
    public class TablePermission
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }
        
        public int PermissionId { get; set; } // 关联权限ID
        public string Name { get; set; } = null!; // 表名，*表示所有表
        public string AllowedOperations { get; set; } = null!; // 允许的操作，用逗号分隔，如 "select,insert,update,delete" 或 "*"
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        // 导航属性
        [Navigate(NavigateType.ManyToOne, nameof(PermissionId))]
        public Permission Permission { get; set; } = null!;
    }
}
