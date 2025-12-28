using DynamicDbApi.Data;
using Microsoft.Extensions.Logging;
using SqlSugar;
using DynamicDbApi.Models;
using System.IO;
using System.Collections.Generic;

namespace DynamicDbApi.Infrastructure;

public class DatabaseInitializer
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(string connectionString, ILogger<DatabaseInitializer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public void Initialize()
    {
        try
        {
            _logger.LogInformation("Initializing SQLite database...");
            
            // 使用SqlSugar初始化数据库
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = _connectionString,
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
            
            // 执行初始化SQL脚本
            var initSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "init_db.sql");
            if (System.IO.File.Exists(initSqlPath))
            {
                var initSql = System.IO.File.ReadAllText(initSqlPath);
                db.Ado.ExecuteCommand(initSql);
            }

            _logger.LogInformation("SQLite database initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing SQLite database");
            throw;
        }
    }
}