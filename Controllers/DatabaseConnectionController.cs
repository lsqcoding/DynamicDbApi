using DynamicDbApi.Infrastructure;
using DynamicDbApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class DatabaseConnectionController : ControllerBase
    {
        private readonly IDatabaseConnectionManager _connectionManager;
        private readonly ILogger<DatabaseConnectionController> _logger;

        public DatabaseConnectionController(
            IDatabaseConnectionManager connectionManager,
            ILogger<DatabaseConnectionController> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        /// <summary>
        /// 获取所有数据库连接
        /// </summary>
        [HttpGet]
        public IActionResult GetAllConnections()
        {
            try
            {
                var connections = _connectionManager.GetAllConnections();
                return Ok(new { Success = true, Data = connections });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取数据库连接列表时发生错误");
                return StatusCode(500, new { Success = false, Message = $"获取数据库连接列表失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 获取指定ID的数据库连接
        /// </summary>
        [HttpGet("{id}")]
        public IActionResult GetConnection(string id)
        {
            try
            {
                var connection = _connectionManager.GetConnectionConfig(id);
                if (connection == null)
                {
                    return NotFound(new { Success = false, Message = $"未找到ID为 {id} 的数据库连接" });
                }

                return Ok(new { Success = true, Data = connection });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取数据库连接时发生错误");
                return StatusCode(500, new { Success = false, Message = $"获取数据库连接失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 添加或更新数据库连接
        /// </summary>
        [HttpPost]
        public IActionResult AddOrUpdateConnection([FromBody] DatabaseConnectionConfig config)
        {
            try
            {
                _logger.LogInformation($"添加或更新数据库连接: ID={config.Id}, 类型={config.Type}");
                
                if (string.IsNullOrEmpty(config.Id) || string.IsNullOrEmpty(config.ConnectionString))
                {
                    _logger.LogWarning("添加或更新数据库连接失败: 连接ID或连接字符串为空");
                    return BadRequest(new { Success = false, Message = "连接ID和连接字符串不能为空" });
                }

                var success = _connectionManager.AddOrUpdateConnection(config);
                if (!success)
                {
                    _logger.LogError("添加或更新数据库连接失败: 未知错误");
                    return StatusCode(500, new { Success = false, Message = "添加或更新数据库连接失败" });
                }

                _logger.LogInformation($"数据库连接添加或更新成功: ID={config.Id}");
                return Ok(new { Success = true, Message = "添加或更新数据库连接成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"添加或更新数据库连接时发生错误: ID={config.Id}");
                return StatusCode(500, new { Success = false, Message = $"添加或更新数据库连接失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 删除数据库连接
        /// </summary>
        [HttpDelete("{id}")]
        public IActionResult RemoveConnection(string id)
        {
            try
            {
                var success = _connectionManager.RemoveConnection(id);
                if (!success)
                {
                    return NotFound(new { Success = false, Message = $"未找到ID为 {id} 的数据库连接" });
                }

                return Ok(new { Success = true, Message = "删除数据库连接成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除数据库连接时发生错误");
                return StatusCode(500, new { Success = false, Message = $"删除数据库连接失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 测试数据库连接
        /// </summary>
        [HttpPost("test")]
        public IActionResult TestConnection([FromBody] DatabaseConnectionConfig config)
        {
            try
            {
                if (string.IsNullOrEmpty(config.ConnectionString))
                {
                    return BadRequest(new { Success = false, Message = "连接字符串不能为空" });
                }

                var (success, message) = _connectionManager.TestConnection(config);
                return Ok(new { Success = success, Message = message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试数据库连接时发生错误");
                return StatusCode(500, new { Success = false, Message = $"测试数据库连接失败: {ex.Message}" });
            }
        }
    }
}