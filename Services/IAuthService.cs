using DynamicDbApi.Models;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 认证服务接口
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// 验证用户凭据并生成JWT令牌
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>JWT令牌</returns>
        Task<string?> AuthenticateAsync(string username, string password);

        /// <summary>
        /// 验证JWT令牌
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <returns>是否有效</returns>
        bool ValidateToken(string token);

        /// <summary>
        /// 从JWT令牌中获取用户ID
        /// </summary>
        /// <param name="token">JWT令牌</param>
        /// <returns>用户ID</returns>
        string? GetUserIdFromToken(string token);
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
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string? Message { get; set; }

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
        /// 角色列表
        /// </summary>
        public List<string>? Roles { get; set; }
    }
}