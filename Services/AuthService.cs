using DynamicDbApi.Models;
using DynamicDbApi.Models.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SqlSugar;
using System.DirectoryServices.AccountManagement;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace DynamicDbApi.Services
{
    public class AuthService : IAuthService
    {
        private readonly ISqlSugarClient _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;
        private readonly AuthSettings _authSettings;
        private readonly WindowsADSettings _windowsAdSettings;

        public AuthService(
            ISqlSugarClient db,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
            
            // 加载认证设置
            _authSettings = configuration.GetSection("AuthSettings").Get<AuthSettings>() ?? 
                throw new InvalidOperationException("AuthSettings are not configured");
            _windowsAdSettings = configuration.GetSection("WindowsADSettings").Get<WindowsADSettings>() ?? 
                throw new InvalidOperationException("WindowsADSettings are not configured");
        }

        private string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public async Task<string?> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            _logger.LogDebug($"验证用户: {username}");
            _logger.LogDebug($"当前默认认证方式: {_authSettings.DefaultAuthentication}");
            
            User? user = null;
            
            // 根据配置的默认认证方式选择认证方法
            switch (_authSettings.DefaultAuthentication)
            {
                case "Jwt":
                    user = await AuthenticateWithJwt(username, password);
                    break;
                case "WindowsAD":
                    user = await AuthenticateWithWindowsAD(username, password);
                    break;
                default:
                    // 默认使用JWT认证
                    user = await AuthenticateWithJwt(username, password);
                    break;
            }

            if (user == null)
                return null;

            // 生成JWT令牌
            return GenerateJwtToken(user);
        }
        
        /// <summary>
        /// 使用JWT（本地数据库）认证用户
        /// </summary>
        private async Task<User?> AuthenticateWithJwt(string username, string password)
        {
            _logger.LogDebug($"使用JWT认证用户: 用户名={username}");
            
            var hashedPassword = HashPassword(password);
            _logger.LogDebug($"计算哈希: {hashedPassword}");

            _logger.LogInformation($"开始JWT验证用户: {username}");
            _logger.LogInformation($"使用的哈希算法: SHA-256 Hex");
            
            var user = await _db.Queryable<User>()
                .Where(u => u.Username == username)
                .FirstAsync();

            if (user != null)
            {
                _logger.LogInformation($"找到用户: {user.Username}");
                _logger.LogInformation($"数据库存储密码哈希: {user.Password}");
                _logger.LogInformation($"计算出的密码哈希: {hashedPassword}");
                _logger.LogInformation($"哈希比对结果: {user.Password == hashedPassword}");
            }
            else
            {
                _logger.LogWarning($"JWT认证失败: 未找到用户 - 用户名={username}");
                return null;
            }

            user = await _db.Queryable<User>()
                .Where(u => u.Username == username && u.Password == hashedPassword)
                .FirstAsync();

            if (user != null)
            {
                _logger.LogInformation($"JWT认证成功: 用户名={username}");
            }
            else
            {
                _logger.LogWarning($"JWT认证失败: 密码错误 - 用户名={username}");
            }
            
            return user;
        }
        
        /// <summary>
        /// 使用Windows AD认证用户
        /// </summary>
        private async Task<User?> AuthenticateWithWindowsAD(string username, string password)
        {
            _logger.LogDebug($"使用Windows AD认证用户: 用户名={username}");
            
            try
            {
                // 构建AD上下文
                PrincipalContext principalContext;

                if (!string.IsNullOrEmpty(_windowsAdSettings.AdminUsername) && !string.IsNullOrEmpty(_windowsAdSettings.AdminPassword))
                {
                    // 使用管理账号和密码创建上下文
                    principalContext = new PrincipalContext(ContextType.Domain, _windowsAdSettings.Domain, _windowsAdSettings.AdminUsername, _windowsAdSettings.AdminPassword);
                }
                else
                {
                    // 使用当前上下文创建AD上下文
                    principalContext = new PrincipalContext(ContextType.Domain, _windowsAdSettings.Domain);
                }

                using (principalContext)
                {
                    // 验证用户名和密码
                    bool isAuthenticated = principalContext.ValidateCredentials(username, password);
                    
                    if (!isAuthenticated)
                    {
                        _logger.LogWarning($"AD验证失败: 用户名或密码错误 - 用户名={username}");
                        return null;
                    }
                    
                    // 获取AD用户信息
                    var adUser = UserPrincipal.FindByIdentity(principalContext, username);
                    if (adUser == null)
                    {
                        _logger.LogWarning($"AD验证失败: 无法找到用户 - 用户名={username}");
                        return null;
                    }
                    
                    _logger.LogInformation($"AD验证成功: 用户名={username}");
                    _logger.LogInformation($"获取AD用户信息成功: 显示名称={adUser.DisplayName}, 邮箱={adUser.EmailAddress}");
                    
                    // 检查本地数据库中是否已存在该用户
                    var localUser = await _db.Queryable<User>()
                        .Where(u => u.Username == username)
                        .FirstAsync();
                    
                    if (localUser == null)
                    {
                        // 如果本地不存在，创建新用户记录
                        localUser = new User
                        {
                            Username = username,
                            Password = "AD_AUTH", // 使用特殊标记表示AD认证用户
                            Email = adUser.EmailAddress
                        };
                        
                        // 插入用户并获取ID
                        var userId = await _db.Insertable(localUser).ExecuteReturnIdentityAsync();
                        
                        // 为AD用户分配默认角色
                        var userRole = new UserRole
                        {
                            UserId = userId,
                            RoleId = 2 // 角色ID 2 对应 "User" 角色
                        };
                        
                        await _db.Insertable(userRole).ExecuteCommandAsync();
                        
                        _logger.LogInformation($"创建AD用户本地记录: 用户名={username}");
                    }
                    
                    return localUser;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Windows AD认证发生错误: 用户名={username}");
                
                // 如果AD认证失败，且配置允许回退到表单认证，则尝试表单认证
                if (_windowsAdSettings.AllowFormsAuth)
                {
                    _logger.LogInformation($"AD认证失败，尝试回退到表单认证: 用户名={username}");
                    return await AuthenticateWithJwt(username, password);
                }
                
                return null;
            }
        }
        
        /// <summary>
        /// 生成JWT令牌
        /// </summary>
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings").Get<JwtSettings>();
            if (jwtSettings == null)
                throw new InvalidOperationException("JWT settings are not configured");
                
            var key = Encoding.ASCII.GetBytes(jwtSettings.Key ?? throw new InvalidOperationException("JWT Key is not configured"));
            
            // 设置令牌过期时间
            var expiryTime = DateTime.UtcNow.AddMinutes(jwtSettings.ExpiryMinutes);
            
            // 获取用户的所有角色
            var userRoles = _db.Queryable<UserRole>()
                .LeftJoin<Role>((ur, r) => ur.RoleId == r.Id)
                .Where((ur, r) => ur.UserId == user.Id && r.IsActive)
                .Select((ur, r) => r.Name)
                .ToList();
            
            // 如果没有角色，设置默认角色
            if (userRoles.Count == 0)
            {
                userRoles.Add("User");
            }
            
            // 创建声明列表
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Username), // 使用用户名作为用户ID，而不是数字ID
                new Claim("username", user.Username),
                new Claim("email", user.Email ?? string.Empty)
            };
            
            // 添加所有角色声明
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expiryTime,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings.Issuer,
                Audience = jwtSettings.Audience
            };
            
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            _logger.LogInformation($"生成JWT令牌成功: 用户名={user.Username}, 过期时间={expiryTime}, 角色={string.Join(",", userRoles)}");
            
            return tokenHandler.WriteToken(token);
        }

        public bool ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured"));
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out _);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string? GetUserIdFromToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);
                var claim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name);
                return claim?.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}