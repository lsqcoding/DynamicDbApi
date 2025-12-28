using DynamicDbApi.Models;
using SqlSugar;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using DynamicDbApi.Services;
using System.Diagnostics;

namespace DynamicDbApi.Infrastructure
{
    /// <summary>
    /// 数据库连接管理器实现(SqlSugar版)
    /// </summary>
    public class DatabaseConnectionManager : IDatabaseConnectionManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILoggingService _loggingService;
        private readonly ConcurrentDictionary<string, SqlSugarClient> _dbClients = new();
        private readonly List<DatabaseConnectionConfig> _connectionConfigs = new();

        public DatabaseConnectionManager(IConfiguration configuration, ILoggingService loggingService)
        {
            _configuration = configuration;
            _loggingService = loggingService;
            InitializeConnections();
        }



        /// <summary>
        /// 初始化数据库连接
        /// </summary>
        private void InitializeConnections()
        {
            var connections = _configuration.GetSection("DatabaseConnections").Get<List<DatabaseConnectionConfig>>();
            if (connections != null)
            {
                foreach (var connection in connections)
                {
                    // 只添加启用的数据库连接
                    if (connection.Enabled)
                    {
                        _connectionConfigs.Add(connection);
                    }
                }
            }
            
            // 如果没有启用的连接，则启用默认连接
            if (_connectionConfigs.Count == 0)
            {
                var defaultConnection = connections?.FirstOrDefault(c => c.IsDefault);
                if (defaultConnection != null)
                {
                    _connectionConfigs.Add(defaultConnection);
                }
            }
            
            // 如果仍然没有连接，创建一个默认的SQLite连接
            if (_connectionConfigs.Count == 0)
            {
                var defaultConnection = new DatabaseConnectionConfig
                {
                    Id = "default",
                    Name = "默认数据库",
                    Type = (int)DbTypeEnum.SQLite, // 使用枚举值1表示SQLite
                    ConnectionString = "Data Source=Data/appdb.db",
                    Enabled = true,
                    IsDefault = true
                };
                _connectionConfigs.Add(defaultConnection);
            }
        }

        /// <summary>
        /// 获取指定ID的数据库客户端
        /// </summary>
        public SqlSugarClient GetDbClient(string connectionId)
        {
            if (_dbClients.TryGetValue(connectionId, out var existingClient))
            {
                return existingClient;
            }

            var config = GetConnectionConfig(connectionId) ?? 
                       throw new ArgumentException($"无效的连接ID: {connectionId}");

            var connectionConfig = new ConnectionConfig()
            {
                ConnectionString = config.ConnectionString,
                DbType = GetDbType(config.Type),
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
                // 连接池配置通过连接字符串设置
            };

            // 根据数据库类型设置连接池参数
            if (config.Type == (int)DbTypeEnum.SQLite)
            {
                // SQLite连接池配置较简单，不需要显式设置Max Pool Size等参数
                if (!connectionConfig.ConnectionString.Contains("Pooling="))
                {
                    connectionConfig.ConnectionString += ";Pooling=True";
                }
            }
            else if (config.Type == (int)DbTypeEnum.MySQL || config.Type == (int)DbTypeEnum.PostgreSQL || config.Type == (int)DbTypeEnum.SQLServer)
            {
                // 对于这些数据库，连接池参数通常在连接字符串中配置
                if (!connectionConfig.ConnectionString.Contains("Max Pool Size"))
                {
                    connectionConfig.ConnectionString += ";Max Pool Size=100";
                }
                if (!connectionConfig.ConnectionString.Contains("Min Pool Size"))
                {
                    connectionConfig.ConnectionString += ";Min Pool Size=10";
                }
                if (!connectionConfig.ConnectionString.Contains("Connection Timeout"))
                {
                    connectionConfig.ConnectionString += ";Connection Timeout=15";
                }
            }

            // 配置读写分离
            if (config.EnableReadWriteSeparation && !string.IsNullOrEmpty(config.ReadConnectionString))
            {
                connectionConfig.SlaveConnectionConfigs = new List<SlaveConnectionConfig>()
                {
                    new SlaveConnectionConfig()
                    {
                        HitRate = 100, // 100%使用该从库
                        ConnectionString = config.ReadConnectionString
                    }
                };
            }

            var db = new SqlSugarClient(connectionConfig);

            // 配置SQL AOP日志
            ConfigureSqlAop(db, connectionId);

            _dbClients[connectionId] = db;
            return db;
        }

        /// <summary>
        /// 获取默认数据库客户端
        /// </summary>
        public SqlSugarClient GetDefaultDbClient()
        {
            var defaultConfig = _connectionConfigs.FirstOrDefault(c => c.IsDefault);
            if (defaultConfig == null)
            {
                defaultConfig = _connectionConfigs.FirstOrDefault();
                if (defaultConfig == null)
                {
                    throw new InvalidOperationException("未配置任何数据库连接");
                }
            }

            return GetDbClient(defaultConfig.Id);
        }

        /// <summary>
        /// 获取所有数据库连接配置
        /// </summary>
        public List<DatabaseConnectionConfig> GetAllConnections()
        {
            return _connectionConfigs.ToList();
        }

        /// <summary>
        /// 获取指定ID的数据库连接配置
        /// </summary>
        public DatabaseConnectionConfig? GetConnectionConfig(string connectionId)
        {
            return _connectionConfigs.FirstOrDefault(c => c.Id == connectionId);
        }

        /// <summary>
        /// 添加或更新数据库连接配置
        /// </summary>
        public bool AddOrUpdateConnection(DatabaseConnectionConfig config)
        {
            try
            {
                var existingConfig = _connectionConfigs.FirstOrDefault(c => c.Id == config.Id);
                if (existingConfig != null)
                {
                    _connectionConfigs.Remove(existingConfig);
                    _dbClients.TryRemove(config.Id, out _);
                }

                _connectionConfigs.Add(config);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 删除数据库连接配置
        /// </summary>
        public bool RemoveConnection(string connectionId)
        {
            var config = _connectionConfigs.FirstOrDefault(c => c.Id == connectionId);
            if (config != null)
            {
                _connectionConfigs.Remove(config);
                _dbClients.TryRemove(connectionId, out _);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        public (bool Success, string Message) TestConnection(DatabaseConnectionConfig config)
        {
            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig()
                {
                    ConnectionString = config.ConnectionString,
                    DbType = GetDbType(config.Type),
                    IsAutoCloseConnection = true
                });

                // 配置SQL AOP日志
                ConfigureSqlAop(db, config.Id);

                db.Ado.Open();
                db.Ado.Close();
                return (true, "连接成功");
            }
            catch (Exception ex)
            {
                return (false, $"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将配置类型转换为SqlSugar的DbType
        /// </summary>
        /// <summary>
        /// 配置SQL AOP日志
        /// </summary>
        private void ConfigureSqlAop(SqlSugarClient db, string connectionId)
        {
            // 执行SQL前事件
            db.Aop.OnLogExecuting = (sql, parameters) =>
            {
                _loggingService.LogSql($"[SQL执行前] [连接: {connectionId}] SQL: {sql}");
                if (parameters != null && parameters.Length > 0)
                {
                    var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterName} = {p.Value}"));
                    _loggingService.LogSql($"[SQL执行前] [连接: {connectionId}] 参数: {paramStr}");
                }
            };

            // 执行SQL后事件
            db.Aop.OnLogExecuted = (sql, parameters) =>
            {
                _loggingService.LogSql($"[SQL执行后] [连接: {connectionId}] SQL: {sql}");
            };

            // SQL执行错误事件
            db.Aop.OnError = (ex) =>
            {
                _loggingService.LogSqlError($"[SQL执行错误] [连接: {connectionId}] 错误: {ex.Message}", ex);
            };
        }

        /// <summary>
        /// 将配置类型转换为SqlSugar的DbType
        /// </summary>
        private DbType GetDbType(int dbType)
        {
            return dbType switch
            {
                (int)DbTypeEnum.SQLite => DbType.Sqlite,
                (int)DbTypeEnum.SQLServer => DbType.SqlServer,
                (int)DbTypeEnum.MySQL => DbType.MySql,
                (int)DbTypeEnum.PostgreSQL => DbType.PostgreSQL,
                (int)DbTypeEnum.Oracle => DbType.Oracle,
                _ => throw new ArgumentException($"不支持的数据库类型: {dbType}")
            };
        }
    }
}