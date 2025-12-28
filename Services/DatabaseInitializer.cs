using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DynamicDbApi.Services;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void Initialize(bool forceRecreate = false)
    {
        string dbPath = _connectionString.Split('=')[1].Split(';')[0];
        
        if (forceRecreate && File.Exists(dbPath))
        {
            _logger.LogInformation("Recreating SQLite database...");
            try
            {
                File.Delete(dbPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete existing database file");
                throw;
            }
        }
        
        if (!File.Exists(dbPath))
        {
            _logger.LogInformation("Creating SQLite database...");
            
            // 确保目录存在
            var directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            ExecuteSqlScript(connection, GetInitScript());
            _logger.LogInformation("Database initialized successfully");
        }
    }

    private void ExecuteSqlScript(SqliteConnection connection, string script)
    {
        // 移除脚本中的事务控制语句
        var cleanedScript = script.Replace("BEGIN TRANSACTION;", "")
                                 .Replace("COMMIT;", "")
                                 .Replace("ROLLBACK;", "");
        
        var commands = cleanedScript.Split(';')
            .Where(cmd => !string.IsNullOrWhiteSpace(cmd.Trim()))
            .ToList();

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var cmd in commands)
            {
                using var command = connection.CreateCommand();
                command.CommandText = cmd;
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private string GetInitScript()
    {
        return File.ReadAllText("Data/init_db.sql");
    }
}