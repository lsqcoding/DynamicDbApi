using DynamicDbApi.Models;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 动态查询服务接口
    /// </summary>
    public interface IDynamicQueryService
    {
        /// <summary>
        /// 执行动态查询
        /// </summary>
        /// <param name="request">查询请求</param>
        /// <param name="userId">用户ID</param>
        /// <returns>查询响应</returns>
        Task<DynamicQueryResponse> ExecuteQueryAsync(DynamicQueryRequest request, string userId);

        /// <summary>
        /// 获取表结构信息
        /// </summary>
        /// <param name="databaseId">数据库连接ID</param>
        /// <param name="tableName">表名，为空则获取所有表</param>
        /// <returns>表结构信息</returns>
        Task<DynamicQueryResponse> GetTableSchemaAsync(string? databaseId, string? tableName);
        
        /// <summary>
        /// 创建表
        /// </summary>
        /// <param name="request">建表请求</param>
        /// <param name="userId">用户ID</param>
        /// <returns>建表响应</returns>
        Task<DynamicQueryResponse> CreateTableAsync(CreateTableRequest request, string userId);
    }
}