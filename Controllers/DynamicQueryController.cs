using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DynamicQueryController : ControllerBase
    {
        private readonly IDynamicQueryService _dynamicQueryService;
        private readonly IAuthService _authService;
        private readonly IQueryAnalysisService _queryAnalysisService;
        private readonly ITableAliasService _tableAliasService;
        private readonly ILogger<DynamicQueryController> _logger;

        public DynamicQueryController(
            IDynamicQueryService dynamicQueryService,
            IAuthService authService,
            IQueryAnalysisService queryAnalysisService,
            ITableAliasService tableAliasService,
            ILogger<DynamicQueryController> logger)
        {
            _dynamicQueryService = dynamicQueryService;
            _authService = authService;
            _queryAnalysisService = queryAnalysisService;
            _tableAliasService = tableAliasService;
            _logger = logger;
        }

        /// <summary>
        /// 执行动态查询
        /// </summary>
        [HttpPost("query")]
        [EnableRateLimiting("query")]
        public async Task<IActionResult> ExecuteQuery([FromBody] DynamicQueryRequest request)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"未授权的访问尝试: {User.Identity?.Name}");
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                _logger.LogInformation($"用户 {userId} 正在执行查询: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                
                // 执行查询
                var response = await _dynamicQueryService.ExecuteQueryAsync(request, userId);
                
                _logger.LogInformation($"用户 {userId} 查询成功: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"执行动态查询时发生错误: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                return StatusCode(500, DynamicQueryResponse.Fail($"执行查询失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取表结构信息
        /// </summary>
        [HttpGet("schema")]
        [EnableRateLimiting("default")]
        public async Task<IActionResult> GetTableSchema([FromQuery] string? databaseId = null, [FromQuery] string? tableName = null)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                // 获取表结构
                var response = await _dynamicQueryService.GetTableSchemaAsync(databaseId, tableName);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取表结构信息时发生错误");
                return StatusCode(500, DynamicQueryResponse.Fail($"获取表结构信息失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 动态创建表
        /// </summary>
        [HttpPost("createTable")]
        [EnableRateLimiting("default")]
        public async Task<IActionResult> CreateTable([FromBody] CreateTableRequest request)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"未授权的建表访问尝试: {User.Identity?.Name}");
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                _logger.LogInformation($"用户 {userId} 正在创建表: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                
                // 创建表
                var response = await _dynamicQueryService.CreateTableAsync(request, userId);
                
                _logger.LogInformation($"用户 {userId} 表创建请求处理完成: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建表时发生错误: 连接ID={request.DatabaseId}, 表名={request.TableName}");
                return StatusCode(500, DynamicQueryResponse.Fail($"创建表失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 获取表的索引建议
        /// </summary>
        [HttpGet("index-suggestions")]
        [EnableRateLimiting("default")]
        public async Task<IActionResult> GetIndexSuggestions([FromQuery] string tableName)
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                if (string.IsNullOrEmpty(tableName))
                {
                    return BadRequest(DynamicQueryResponse.Fail("表名不能为空"));
                }

                _logger.LogInformation($"用户 {userId} 正在获取表索引建议: 表名={tableName}");
                
                // 获取索引建议
                var suggestions = await _queryAnalysisService.GetIndexSuggestionsAsync(tableName);
                
                return Ok(DynamicQueryResponse.Ok(suggestions, "获取索引建议成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取索引建议时发生错误: 表名={tableName}");
                return StatusCode(500, DynamicQueryResponse.Fail($"获取索引建议失败: {ex.Message}"));
            }
        }

        /// <summary>
        /// 刷新表别名配置
        /// </summary>
        [HttpPost("refresh-aliases")]
        [EnableRateLimiting("default")]
        public IActionResult RefreshTableAliases()
        {
            try
            {
                // 获取当前用户ID
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning($"未授权的刷新表别名访问尝试: {User.Identity?.Name}");
                    return Unauthorized(DynamicQueryResponse.Fail("未授权的访问"));
                }

                _logger.LogInformation($"用户 {userId} 正在刷新表别名配置");
                
                // 刷新表别名
                _tableAliasService.RefreshAliases();
                
                _logger.LogInformation($"用户 {userId} 表别名配置刷新成功");
                return Ok(DynamicQueryResponse.Ok(null, "表别名配置刷新成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新表别名配置时发生错误");
                return StatusCode(500, DynamicQueryResponse.Fail($"刷新表别名配置失败: {ex.Message}"));
            }
        }
    }
}