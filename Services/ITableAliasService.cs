using System;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 表别名服务接口，用于管理数据库表的别名映射
    /// </summary>
    public interface ITableAliasService
    {
        /// <summary>
        /// 根据别名获取真实表名
        /// </summary>
        /// <param name="databaseId">数据库ID</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns>真实表名，如果不存在则返回原别名</returns>
        string GetRealTableName(string databaseId, string tableAlias);
        
        /// <summary>
        /// 根据真实表名获取别名
        /// </summary>
        /// <param name="databaseId">数据库ID</param>
        /// <param name="realTableName">真实表名</param>
        /// <returns>表别名，如果不存在则返回原真实表名</returns>
        string GetAlias(string databaseId, string realTableName);
        
        /// <summary>
        /// 刷新表别名配置（重新从数据库加载）
        /// </summary>
        void RefreshAliases();
    }
}