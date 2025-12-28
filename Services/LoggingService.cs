using Serilog;
using Serilog.Context;

namespace DynamicDbApi.Services
{
    public class LoggingService : ILoggingService
    {
        private readonly Serilog.ILogger _logger;

        public LoggingService()
        {
            _logger = Log.Logger;
        }

        public void LogInformation(string message)
        {
            _logger.Information(message);
        }

        public void LogWarning(string message)
        {
            _logger.Warning(message);
        }

        public void LogError(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                _logger.Error(ex, message);
            }
            else
            {
                _logger.Error(message);
            }
        }

        public void LogDatabaseOperation(string connectionId, string operation, string tableName, string? userId = null)
        {
            var message = $"Database operation: {operation} on table {tableName} in connection {connectionId}";
            if (!string.IsNullOrEmpty(userId))
            {
                message += $" by user {userId}";
            }
            _logger.Information(message);
        }

        /// <summary>
        /// 记录SQL日志（输出到单独的SQL日志文件）
        /// </summary>
        public void LogSql(string message, Exception? ex = null)
        {
            using (LogContext.PushProperty("SqlLog", true))
            {
                if (ex != null)
                {
                    _logger.Information(ex, message);
                }
                else
                {
                    _logger.Information(message);
                }
            }
        }

        /// <summary>
        /// 记录SQL错误日志（输出到单独的SQL日志文件）
        /// </summary>
        public void LogSqlError(string message, Exception ex)
        {
            using (LogContext.PushProperty("SqlLog", true))
            {
                _logger.Error(ex, message);
            }
        }
    }
}