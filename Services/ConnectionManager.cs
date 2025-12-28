using SqlSugar;
using System.Collections.Concurrent;
using DynamicDbApi.Infrastructure;
using DynamicDbApi.Services;

namespace DynamicDbApi.Services
{
    public class ConnectionManager : IConnectionManager
    {
        private readonly IDatabaseConnectionManager _databaseConnectionManager;
        private readonly ILoggingService _loggingService;

        public ConnectionManager(IDatabaseConnectionManager databaseConnectionManager, ILoggingService loggingService)
        {
            _databaseConnectionManager = databaseConnectionManager;
            _loggingService = loggingService;
        }

        public ISqlSugarClient GetConnection(string connectionId)
        {
            // 使用统一的数据库连接管理器获取连接
            return _databaseConnectionManager.GetDbClient(connectionId);
        }

        /// <summary>
        /// 配置SQL AOP日志
        /// </summary>
        private void ConfigureSqlAop(ISqlSugarClient db, string connectionId)
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
    }
}