using SqlSugar;

namespace DynamicDbApi.Models
{
    /// <summary>
    /// 用户角色关联表
    /// </summary>
    [SugarTable("UserRoles")]
    public class UserRole
    {
        /// <summary>
        /// 主键
        /// </summary>
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        [SugarColumn(IsNullable = false, ColumnName = "UserId")]
        public int UserId { get; set; }

        /// <summary>
        /// 角色ID
        /// </summary>
        [SugarColumn(IsNullable = false, ColumnName = "RoleId")]
        public int RoleId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP")]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 导航属性：用户
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(UserId))]
        public User User { get; set; }

        /// <summary>
        /// 导航属性：角色
        /// </summary>
        [Navigate(NavigateType.OneToOne, nameof(RoleId))]
        public Role Role { get; set; }
    }
}