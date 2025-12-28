using DynamicDbApi.Data;
using DynamicDbApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.IO;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DbInitController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DbInitController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("export-schema")]
        public IActionResult ExportDatabaseSchema()
        {
            var sb = new StringBuilder();
            
            // 添加初始化数据
            var initSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "init_db.sql");
            if (System.IO.File.Exists(initSqlPath))
            {
                sb.AppendLine(System.IO.File.ReadAllText(initSqlPath));
            }
            
            return Ok(sb.ToString());
        }

        [HttpPost("init-database")]
        public async Task<IActionResult> InitializeDatabase()
        {
            try
            {
                var initSqlPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "init_db.sql");
                if (!System.IO.File.Exists(initSqlPath))
                {
                    return BadRequest("Initialization script not found");
                }

                var initSql = await System.IO.File.ReadAllTextAsync(initSqlPath);
                
                // 分割SQL脚本为单独的语句
                var statements = new List<string>();
                var currentStatement = new StringBuilder();
                
                using (var reader = new StringReader(initSql))
                {
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        
                        currentStatement.AppendLine(line);
                        
                        if (line.EndsWith(";"))
                        {
                            statements.Add(currentStatement.ToString());
                            currentStatement.Clear();
                        }
                    }
                    
                    if (currentStatement.Length > 0)
                    {
                        statements.Add(currentStatement.ToString());
                    }
                }

                // 使用SqlSugar事务执行SQL
                _context.Db.Ado.BeginTran();
                try
                {
                    foreach (var sql in statements)
                    {
                        if (!string.IsNullOrWhiteSpace(sql))
                        {
                            _context.Db.Ado.ExecuteCommand(sql);
                        }
                    }
                    _context.Db.Ado.CommitTran();
                    return Ok("Database initialized successfully");
                }
                catch
                {
                    _context.Db.Ado.RollbackTran();
                    throw;
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Initialization failed: {ex.Message}");
            }
        }

        [HttpGet("check-tables")]
        public IActionResult CheckTables()
        {
            try
            {
                var result = new Dictionary<string, object>();
                
                // 检查数据库连接
                var canConnect = _context.Db.Ado.IsValidConnection();
                result["DatabaseConnection"] = canConnect;
                
                if (canConnect)
                {
                    // 检查表是否存在
                    var tableNames = new List<string> 
                    { 
                        "FileTypePolicies", 
                        "MailServers", 
                        "CorsSettings", 
                        "StoredFiles", 
                        "FileShareLinks" 
                    };
                    
                    foreach (var tableName in tableNames)
                    {
                        try
                        {
                            // 尝试执行查询来检查表是否存在
                            try
                            {
                                _context.Db.Ado.ExecuteCommand($"SELECT 1 FROM {tableName} WHERE 1=0");
                                result[tableName + "_Exists"] = true;
                                
                                // 检查是否有数据
                                var count = _context.Db.Ado.GetScalar($"SELECT COUNT(*) FROM {tableName}");
                                var hasData = Convert.ToInt32(count) > 0;
                                result[tableName + "_HasData"] = hasData;
                            }
                            catch
                            {
                                result[tableName + "_Exists"] = false;
                            }
                        }
                        catch (Exception ex)
                        {
                            result[tableName + "_Error"] = ex.Message;
                        }
                    }
                }
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new 
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                });
            }
        }
    }
}