using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<AuthController> _logger;
        private readonly ISqlSugarClient _db;

        public AuthController(
            IAuthService authService,
            IPermissionService permissionService,
            ILogger<AuthController> logger,
            ISqlSugarClient db)
        {
            _authService = authService;
            _permissionService = permissionService;
            _logger = logger;
            _db = db;
        }

        /// <summary>
        /// 用户登录
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                _logger.LogInformation($"登录尝试: 用户名={request.Username}");
                
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    _logger.LogWarning("登录失败: 用户名或密码为空");
                    return BadRequest(new LoginResponse
                    {
                        Success = false,
                        Message = "用户名和密码不能为空"
                    });
                }

                var token = await _authService.AuthenticateAsync(request.Username, request.Password);
                if (token == null)
                {
                    _logger.LogWarning($"登录失败: 用户名={request.Username}, 原因=用户名或密码错误");
                    return Unauthorized(new LoginResponse
                    {
                        Success = false,
                        Message = "用户名或密码错误"
                    });
                }

                var roles = await _permissionService.GetUserRolesAsync(request.Username);
                
                _logger.LogInformation($"登录成功: 用户名={request.Username}, 角色={string.Join(",", roles)}");
                return Ok(new LoginResponse
                {
                    Success = true,
                    Message = "登录成功",
                    Token = token,
                    UserId = request.Username,
                    Username = request.Username,
                    Roles = roles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"登录时发生错误: 用户名={request.Username}, 异常类型={ex.GetType().Name}, 异常消息={ex.Message}, 堆栈跟踪={ex.StackTrace}");
                return StatusCode(500, new LoginResponse
                {
                    Success = false,
                    Message = "登录失败，请稍后重试"
                });
            }
        }

        /// <summary>
        /// 验证令牌
        /// </summary>
        [HttpPost("validate-token")]
        public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Token))
                {
                    return BadRequest(new { Success = false, Message = "令牌不能为空" });
                }

                var isValid = _authService.ValidateToken(request.Token);
                if (!isValid)
                {
                    return Unauthorized(new { Success = false, Message = "令牌无效或已过期" });
                }

                var userId = _authService.GetUserIdFromToken(request.Token);
                return Ok(new { Success = true, Message = "令牌有效", UserId = userId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证令牌时发生错误");
                return StatusCode(500, new { Success = false, Message = "验证令牌失败，请稍后重试" });
            }
        }

        /// <summary>
        /// 创建用户 (仅限开发环境)
        /// </summary>
        [HttpPost("create-user")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                {
                    return BadRequest(new { Success = false, Message = "用户名和密码不能为空" });
                }

                var existingUser = await _db.Queryable<User>()
                    .Where(u => u.Username == request.Username)
                    .FirstAsync();

                if (existingUser != null)
                {
                    return BadRequest(new { Success = false, Message = "用户名已存在" });
                }

                var newUser = new User
                {
                    Username = request.Username,
                    Password = request.Password,
                    Email = request.Email
                    // 不再设置Role属性，改为通过UserRoles表关联
                };

                // 插入用户并获取ID
                var userId = await _db.Insertable(newUser).ExecuteReturnIdentityAsync();

                // 设置用户角色
                var rolesToAssign = request.Roles ?? new List<string> { "User" };
                
                foreach (var roleName in rolesToAssign)
                {
                    // 查找角色
                    var role = await _db.Queryable<Role>()
                        .Where(r => r.Name == roleName)
                        .FirstAsync();
                    
                    if (role != null)
                    {
                        // 创建用户角色关联
                        var userRole = new UserRole
                        {
                            UserId = userId,
                            RoleId = role.Id
                        };
                        
                        await _db.Insertable(userRole).ExecuteCommandAsync();
                    }
                }

                return Ok(new { Success = true, Message = "用户创建成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建用户时发生错误");
                return StatusCode(500, new { Success = false, Message = "创建用户失败" });
            }
        }
    }

    public class CreateUserRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<string>? Roles { get; set; }
        public string? Email { get; set; }
    }

    /// <summary>
    /// 验证令牌请求
    /// </summary>
    public class ValidateTokenRequest
    {
        /// <summary>
        /// JWT令牌
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// 登录请求
    /// </summary>
    public class LoginRequest
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string Username { get; set; } = string.Empty;
        
        /// <summary>
        /// 密码
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// 登录响应
    /// </summary>
    public class LoginResponse
    {
        /// <summary>
        /// 是否登录成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; } = string.Empty;
        
        /// <summary>
        /// JWT令牌
        /// </summary>
        public string? Token { get; set; }
        
        /// <summary>
        /// 用户ID
        /// </summary>
        public string? UserId { get; set; }
        
        /// <summary>
        /// 用户名
        /// </summary>
        public string? Username { get; set; }
        
        /// <summary>
        /// 用户角色
        /// </summary>
        public List<string>? Roles { get; set; }
    }
}