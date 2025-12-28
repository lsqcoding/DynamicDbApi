using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using SqlSugar;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MailController : ControllerBase
    {
        private readonly IMailService _mailService;
        private readonly ISqlSugarClient _db;
        private readonly ILogger<MailController> _logger;

        public MailController(
            IMailService mailService,
            ISqlSugarClient db,
            ILogger<MailController> logger)
        {
            _mailService = mailService;
            _db = db;
            _logger = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail([FromBody] MailRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                if (string.IsNullOrEmpty(request.From))
                {
                    await _mailService.SendEmailAsync(request.ServerId, request.To, request.Subject, request.Body, request.IsHtml);
                }
                else
                {
                    await _mailService.SendEmailAsync(request.ServerId, request.From, request.To, request.Subject, request.Body, request.IsHtml);
                }

                return Ok(new { success = true, message = "邮件发送成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送邮件失败");
                return StatusCode(500, new { success = false, message = $"发送邮件失败: {ex.Message}" });
            }
        }

        [HttpGet("servers")]
        public async Task<IActionResult> GetMailServers()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                var servers = await _db.Queryable<MailServer>()
                    .Where(s => s.Enabled)
                    .ToListAsync();

                return Ok(new { success = true, data = servers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取邮件服务器列表失败");
                return StatusCode(500, new { success = false, message = $"获取邮件服务器列表失败: {ex.Message}" });
            }
        }

        [HttpPost("test-connection")]
        public async Task<IActionResult> TestSmtpConnection([FromBody] TestSmtpConnectionRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                if (string.IsNullOrEmpty(request.ServerId) && 
                    (string.IsNullOrEmpty(request.Host) || request.Port <= 0 || 
                     string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password)))
                {
                    return BadRequest(new { success = false, message = "请提供有效的服务器配置信息" });
                }

                var (success, message) = string.IsNullOrEmpty(request.ServerId) 
                    ? await _mailService.TestSmtpConnectionAsync(request.Host!, request.Port, request.Username!, request.Password!, request.EnableSsl)
                    : await _mailService.TestSmtpConnectionAsync(request.ServerId);

                return Ok(new { success, message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "测试SMTP连接失败");
                return StatusCode(500, new { success = false, message = $"测试SMTP连接失败: {ex.Message}" });
            }
        }

        [HttpPost("servers")]
        public async Task<IActionResult> CreateMailServer([FromBody] CreateMailServerRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                // 验证请求数据
                if (string.IsNullOrWhiteSpace(request.Name))
                    return BadRequest(new { success = false, message = "服务器名称不能为空" });
                if (string.IsNullOrWhiteSpace(request.Host))
                    return BadRequest(new { success = false, message = "服务器地址不能为空" });
                if (request.Port <= 0 || request.Port > 65535)
                    return BadRequest(new { success = false, message = "端口号无效" });
                if (string.IsNullOrWhiteSpace(request.Username))
                    return BadRequest(new { success = false, message = "用户名不能为空" });
                if (string.IsNullOrWhiteSpace(request.Password))
                    return BadRequest(new { success = false, message = "密码不能为空" });

                // 检查名称是否已存在
                var existingServer = await _db.Queryable<MailServer>()
                    .Where(s => s.Name == request.Name)
                    .FirstAsync();
                
                if (existingServer != null)
                {
                    return BadRequest(new { success = false, message = $"服务器名称 '{request.Name}' 已存在" });
                }

                // 如果设置为默认服务器，取消其他服务器的默认状态
                if (request.IsDefault)
                {
                    await _db.Updateable<MailServer>()
                        .SetColumns(s => new MailServer { IsDefault = false })
                        .Where(s => s.IsDefault)
                        .ExecuteCommandAsync();
                }

                var server = new MailServer
                {
                    Name = request.Name,
                    Host = request.Host,
                    Port = request.Port,
                    UserName = request.Username,
                    Password = request.Password,
                    EnableSsl = request.EnableSsl,
                    DefaultFrom = request.DefaultFrom,
                    DisplayName = request.DisplayName,
                    IsDefault = request.IsDefault,
                    Enabled = request.Enabled,
                    CreatedAt = DateTime.UtcNow
                };

                await _db.Insertable(server).ExecuteCommandAsync();

                _logger.LogInformation($"邮件服务器 '{request.Name}' 创建成功");
                return Ok(new { success = true, message = "邮件服务器创建成功", data = server });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建邮件服务器失败");
                return StatusCode(500, new { success = false, message = $"创建邮件服务器失败: {ex.Message}" });
            }
        }

        [HttpPut("servers/{id}")]
        public async Task<IActionResult> UpdateMailServer(int id, [FromBody] UpdateMailServerRequest request)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                var server = await _db.Queryable<MailServer>()
                    .Where(s => s.Id == id)
                    .FirstAsync();

                if (server == null)
                {
                    return NotFound(new { success = false, message = $"邮件服务器 ID {id} 未找到" });
                }

                // 如果修改了名称，检查是否与其他服务器冲突
                if (!string.IsNullOrWhiteSpace(request.Name) && server.Name != request.Name)
                {
                    var existingServer = await _db.Queryable<MailServer>()
                        .Where(s => s.Name == request.Name && s.Id != id)
                        .FirstAsync();
                    
                    if (existingServer != null)
                    {
                        return BadRequest(new { success = false, message = $"服务器名称 '{request.Name}' 已存在" });
                    }
                    server.Name = request.Name;
                }

                // 如果设置为默认服务器，取消其他服务器的默认状态
                if (request.IsDefault.HasValue && request.IsDefault.Value)
                {
                    await _db.Updateable<MailServer>()
                        .SetColumns(s => new MailServer { IsDefault = false })
                        .Where(s => s.IsDefault)
                        .ExecuteCommandAsync();
                    server.IsDefault = true;
                }

                if (!string.IsNullOrWhiteSpace(request.Host)) server.Host = request.Host;
                if (request.Port.HasValue) server.Port = request.Port.Value;
                if (!string.IsNullOrWhiteSpace(request.Username)) server.UserName = request.Username;
                if (!string.IsNullOrWhiteSpace(request.Password)) server.Password = request.Password;
                if (request.EnableSsl.HasValue) server.EnableSsl = request.EnableSsl.Value;
                if (!string.IsNullOrWhiteSpace(request.DefaultFrom)) server.DefaultFrom = request.DefaultFrom;
                if (!string.IsNullOrWhiteSpace(request.DisplayName)) server.DisplayName = request.DisplayName;
                if (request.Enabled.HasValue) server.Enabled = request.Enabled.Value;

                await _db.Updateable(server).ExecuteCommandAsync();

                _logger.LogInformation($"邮件服务器 '{server.Name}' 更新成功");
                return Ok(new { success = true, message = "邮件服务器更新成功", data = server });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新邮件服务器失败: {id}");
                return StatusCode(500, new { success = false, message = $"更新邮件服务器失败: {ex.Message}" });
            }
        }

        [HttpDelete("servers/{id}")]
        public async Task<IActionResult> DeleteMailServer(int id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                var server = await _db.Queryable<MailServer>()
                    .Where(s => s.Id == id)
                    .FirstAsync();

                if (server == null)
                {
                    return NotFound(new { success = false, message = $"邮件服务器 ID {id} 未找到" });
                }

                // 检查是否是默认服务器
                if (server.IsDefault)
                {
                    return BadRequest(new { success = false, message = "不能删除默认邮件服务器" });
                }

                await _db.Deleteable<MailServer>().Where(s => s.Id == id).ExecuteCommandAsync();

                _logger.LogInformation($"邮件服务器 '{server.Name}' 删除成功");
                return Ok(new { success = true, message = "邮件服务器删除成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"删除邮件服务器失败: {id}");
                return StatusCode(500, new { success = false, message = $"删除邮件服务器失败: {ex.Message}" });
            }
        }
    }

    public class MailRequest
    {
        public string ServerId { get; set; } = "default";
        public string? From { get; set; }
        public required string To { get; set; }
        public required string Subject { get; set; }
        public required string Body { get; set; }
        public bool IsHtml { get; set; } = false;
    }

    public class TestSmtpConnectionRequest
    {
        public string? ServerId { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; } = 587;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; } = true;
    }

    public class CreateMailServerRequest
    {
        public required string Name { get; set; }
        public required string Host { get; set; }
        public int Port { get; set; } = 587;
        public required string Username { get; set; }
        public required string Password { get; set; }
        public bool EnableSsl { get; set; } = true;
        public string? DefaultFrom { get; set; }
        public string? DisplayName { get; set; }
        public bool IsDefault { get; set; } = false;
        public bool Enabled { get; set; } = true;
    }

    public class UpdateMailServerRequest
    {
        public string? Name { get; set; }
        public string? Host { get; set; }
        public int? Port { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool? EnableSsl { get; set; }
        public string? DefaultFrom { get; set; }
        public string? DisplayName { get; set; }
        public bool? IsDefault { get; set; }
        public bool? Enabled { get; set; }
    }
}