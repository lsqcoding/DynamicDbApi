using DynamicDbApi.Models;
using SqlSugar;

namespace DynamicDbApi.Infrastructure
{
    /// <summary>
    /// 数据库连接管理器接口(SqlSugar版)
    /// </summary>
    public interface IDatabaseConnectionManager
    {
        /// <summary>
        /// 获取指定ID的数据库客户端
        /// </summary>
        SqlSugarClient GetDbClient(string connectionId);

        /// <summary>
        /// 获取默认数据库客户端
        /// </summary>
        SqlSugarClient GetDefaultDbClient();

        /// <summary>
        /// 获取所有数据库连接配置
        /// </summary>
        List<DatabaseConnectionConfig> GetAllConnections();

        /// <summary>
        /// 获取指定ID的数据库连接配置
        /// </summary>
        DatabaseConnectionConfig? GetConnectionConfig(string connectionId);

        /// <summary>
        /// 添加或更新数据库连接配置
        /// </summary>
        bool AddOrUpdateConnection(DatabaseConnectionConfig config);

        /// <summary>
        /// 删除数据库连接配置
        /// </summary>
        bool RemoveConnection(string connectionId);

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        (bool Success, string Message) TestConnection(DatabaseConnectionConfig config);
    }
}