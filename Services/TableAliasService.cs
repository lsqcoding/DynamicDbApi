using System.Collections.Concurrent;
using DynamicDbApi.Infrastructure;
using SqlSugar;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 表别名服务，用于管理数据库表的别名映射
    /// </summary>
    public class TableAliasService : ITableAliasService
    {
        private readonly IDatabaseConnectionManager _connectionManager;
        private readonly ILogger<TableAliasService> _logger;
        private readonly bool _isEnabled;
        
        // 使用嵌套的ConcurrentDictionary存储databaseId到别名映射
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _tableAliases = new();
        
        // 使用object作为锁，确保线程安全
        private readonly object _lockObject = new();

        public TableAliasService(
            IDatabaseConnectionManager connectionManager,
            ILogger<TableAliasService> logger,
            IConfiguration configuration)
        {
            _connectionManager = connectionManager;
            _logger = logger;
            
            // 读取表别名功能启用配置
            _isEnabled = configuration.GetSection("TableAliases").GetValue<bool>("Enabled", true);
            
            _logger.LogInformation("Table alias feature is {0}", _isEnabled ? "enabled" : "disabled");
            
            // 初始化时加载别名配置
            if (_isEnabled)
            {
                LoadAliasesFromDatabase();
            }
        }

        /// <summary>
        /// 从数据库加载表别名配置
        /// </summary>
        private void LoadAliasesFromDatabase()
        {
            try
            {
                // 获取默认数据库连接
                var defaultDb = _connectionManager.GetDefaultDbClient();
                
                // 查询所有表别名配置，使用dynamic类型代替TableAlias类
                var tableAliasConfigs = defaultDb.Queryable<dynamic>().AS("TableAliases").ToList();
                
                // 清空现有别名配置
                _tableAliases.Clear();
                
                // 按databaseId分组并构建别名映射
                foreach (var config in tableAliasConfigs)
                {
                    // 获取配置值
                    string databaseId = config.DatabaseId?.ToString()?.ToLower() ?? "default";
                    string realTableName = config.RealTableName?.ToString()?.ToLower() ?? string.Empty;
                    string alias = config.Alias?.ToString()?.ToLower() ?? string.Empty;
                    
                    if (!string.IsNullOrWhiteSpace(realTableName) && !string.IsNullOrWhiteSpace(alias))
                    {
                        // 确保databaseId存在对应的别名映射
                        if (!_tableAliases.TryGetValue(databaseId, out var dbAliases))
                        {
                            dbAliases = new ConcurrentDictionary<string, string>();
                            _tableAliases.TryAdd(databaseId, dbAliases);
                        }
                        
                        // 添加别名映射（真实表名 -> 别名）
                        dbAliases.TryAdd(realTableName, alias);
                    }
                }
                
                _logger.LogInformation("Table alias configuration loaded successfully, loaded {0} alias configurations", tableAliasConfigs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load table alias configuration");
            }
        }

        /// <summary>
        /// 刷新表别名配置
        /// </summary>
        public void RefreshAliases()
        {
            // 如果表别名功能未启用，直接返回
            if (!_isEnabled)
            {
                return;
            }
            
            // 使用锁确保线程安全
            lock (_lockObject)
            {
                LoadAliasesFromDatabase();
            }
        }

        /// <summary>
        /// 根据别名获取真实表名
        /// </summary>
        /// <param name="databaseId">数据库ID</param>
        /// <param name="tableAlias">表别名</param>
        /// <returns>真实表名，如果不存在则返回原别名</returns>
        public string GetRealTableName(string databaseId, string tableAlias)
        {
            // 如果表别名功能未启用，直接返回原别名
            if (!_isEnabled)
            {
                return tableAlias;
            }
            
            // 如果表别名不存在，直接返回原别名
            if (string.IsNullOrWhiteSpace(databaseId) || string.IsNullOrWhiteSpace(tableAlias))
            {
                return tableAlias;
            }
            
            // 转换为小写进行比较
            var lowerDatabaseId = databaseId.ToLower();
            var lowerTableAlias = tableAlias.ToLower();
            
            // 查找对应的别名映射
            if (_tableAliases.TryGetValue(lowerDatabaseId, out var dbAliases))
            {
                // 查找真实表名
                foreach (var kvp in dbAliases)
                {
                    if (kvp.Value.Equals(lowerTableAlias, StringComparison.OrdinalIgnoreCase))
                    {
                        return kvp.Key;
                    }
                }
            }
            
            // 没有找到对应的别名，返回原表名
            return tableAlias;
        }

        /// <summary>
        /// 根据真实表名获取别名
        /// </summary>
        /// <param name="databaseId">数据库ID</param>
        /// <param name="realTableName">真实表名</param>
        /// <returns>表别名，如果不存在则返回原真实表名</returns>
        public string GetAlias(string databaseId, string realTableName)
        {
            // 如果表别名功能未启用，直接返回原真实表名
            if (!_isEnabled)
            {
                return realTableName;
            }
            
            // 如果真实表名不存在，直接返回原表名
            if (string.IsNullOrWhiteSpace(databaseId) || string.IsNullOrWhiteSpace(realTableName))
            {
                return realTableName;
            }
            
            // 转换为小写进行比较
            var lowerDatabaseId = databaseId.ToLower();
            var lowerRealTableName = realTableName.ToLower();
            
            // 查找对应的别名映射
            if (_tableAliases.TryGetValue(lowerDatabaseId, out var dbAliases))
            {
                // 查找别名
                if (dbAliases.TryGetValue(lowerRealTableName, out var alias))
                {
                    return alias;
                }
            }
            
            // 没有找到对应的别名，返回原真实表名
            return realTableName;
        }
    }
}