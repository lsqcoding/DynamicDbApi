using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;
using System.Security.Claims;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CustomQueryController : ControllerBase
    {
        private readonly IConnectionManager _connectionManager;
        private readonly ILogger<CustomQueryController> _logger;

        public CustomQueryController(
            IConnectionManager connectionManager,
            ILogger<CustomQueryController> logger)
        {
            _connectionManager = connectionManager;
            _logger = logger;
        }

        [HttpPost("query")]
        public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new ApiResponse(false, "未授权的访问"));
                }

                var db = _connectionManager.GetConnection(request.Db);
                var query = db.Queryable<dynamic>().AS(request.Table);

                if (request.Columns?.Length > 0)
                {
                    query = query.Select(string.Join(",", request.Columns));
                }

                var result = await query.Take(10).ToListAsync();

                return Ok(new ApiResponse(true)
                {
                    Data = result,
                    Db = request.Db
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询执行失败");
                return StatusCode(500, new ApiResponse(false, $"查询失败: {ex.Message}"));
            }
        }
    }

    public class QueryRequest
    {
        public string Db { get; set; } = "default";
        public string Table { get; set; } = string.Empty;
        public string[] Columns { get; set; } = Array.Empty<string>();
    }

    public class ApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public object Data { get; set; } = null;
        public string Db { get; set; } = string.Empty;

        public ApiResponse(bool success, string message = null)
        {
            Success = success;
            Message = message;
            Db = string.Empty;
        }
    }
}