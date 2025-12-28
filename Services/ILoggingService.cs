namespace DynamicDbApi.Services
{
    public interface ILoggingService
    {
        void LogInformation(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
        void LogDatabaseOperation(string connectionId, string operation, string tableName, string? userId = null);
        void LogSql(string message, Exception? ex = null);
        void LogSqlError(string message, Exception ex);
    }
}