using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ApiTestController : ControllerBase
    {
        private readonly IDynamicQueryService _dynamicQueryService;
        private readonly ILogger<ApiTestController> _logger;

        public ApiTestController(
            IDynamicQueryService dynamicQueryService,
            ILogger<ApiTestController> logger)
        {
            _dynamicQueryService = dynamicQueryService;
            _logger = logger;
        }

        /// <summary>
        /// 测试动态查询
        /// </summary>
        [HttpPost("test-query")]
        public async Task<IActionResult> TestQuery([FromBody] DynamicQueryRequest request)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                // 记录测试请求
                _logger.LogInformation($"用户 {userId} 执行测试查询: {request.Operation} {request.TableName}");

                // 执行查询
                var response = await _dynamicQueryService.ExecuteQueryAsync(request, userId);
                
                // 记录测试结果
                _logger.LogInformation($"测试查询结果: {(response.Success ? "成功" : "失败")} - {response.Message}");
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行测试查询时发生错误");
                return StatusCode(500, DynamicQueryResponse.Fail($"执行测试查询失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取数据库列表
        /// </summary>
        [HttpGet("databases")]
        public IActionResult GetDatabases()
        {
            try
            {
                // 这里应该从数据库连接管理器获取数据库列表
                // 简化实现，返回配置中的数据库连接
                var databases = HttpContext.RequestServices
                    .GetRequiredService<Infrastructure.IDatabaseConnectionManager>()
                    .GetAllConnections()
                    .Select(c => new { Id = c.Id, Name = c.Name, Type = c.Type })
                    .ToList();

                return Ok(new { Success = true, Data = databases });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取数据库列表时发生错误");
                return StatusCode(500, new { Success = false, Message = $"获取数据库列表失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 获取表列表
        /// </summary>
        [HttpGet("tables")]
        public async Task<IActionResult> GetTables([FromQuery] string? databaseId = null)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Success = false, Message = "未授权的访问" });
                }

                // 获取表列表
                var response = await _dynamicQueryService.GetTableSchemaAsync(databaseId, null);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取表列表时发生错误");
                return StatusCode(500, new { Success = false, Message = $"获取表列表失败: {ex.Message}" });
            }
        }

        /// <summary>
        /// 获取表结构
        /// </summary>
        [HttpGet("table-schema")]
        public async Task<IActionResult> GetTableSchema([FromQuery] string? databaseId = null, [FromQuery] string? tableName = null)
        {
            try
            {
                if (string.IsNullOrEmpty(tableName))
                {
                    return BadRequest(new { Success = false, Message = "表名不能为空" });
                }

                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { Success = false, Message = "未授权的访问" });
                }

                // 获取表结构
                var response = await _dynamicQueryService.GetTableSchemaAsync(databaseId, tableName);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取表结构时发生错误");
                return StatusCode(500, new { Success = false, Message = $"获取表结构失败: {ex.Message}" });
            }
        }
    }
}