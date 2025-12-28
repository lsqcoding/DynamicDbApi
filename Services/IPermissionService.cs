namespace DynamicDbApi.Services
{
    /// <summary>
    /// 权限服务接口
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 检查用户是否有权限执行指定操作
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="databaseId">数据库连接ID</param>
        /// <param name="tableName">表名</param>
        /// <param name="operation">操作类型（select, insert, update, delete）</param>
        /// <returns>是否有权限</returns>
        Task<bool> CheckPermissionAsync(string userId, string databaseId, string tableName, string operation);

        /// <summary>
        /// 获取用户角色
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>角色列表</returns>
        Task<List<string>> GetUserRolesAsync(string userId);

        /// <summary>
        /// 获取角色权限
        /// </summary>
        /// <param name="role">角色名称</param>
        /// <returns>权限列表</returns>
        Task<List<RolePermission>> GetRolePermissionsAsync(string role);
        
        /// <summary>
        /// 刷新权限缓存
        /// </summary>
        void RefreshPermissions();
    }

    /// <summary>
    /// 角色权限
    /// </summary>
    public class RolePermission
    {
        /// <summary>
        /// 数据库连接ID
        /// </summary>
        public string DatabaseId { get; set; } = string.Empty;

        /// <summary>
        /// 表权限列表
        /// </summary>
        public List<TablePermission> Tables { get; set; } = new List<TablePermission>();
    }

    /// <summary>
    /// 表权限
    /// </summary>
    public class TablePermission
    {
        /// <summary>
        /// 表名
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 允许的操作列表
        /// </summary>
        public List<string> AllowedOperations { get; set; } = new List<string>();
    }
}