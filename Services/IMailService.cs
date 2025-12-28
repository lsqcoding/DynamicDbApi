using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public interface IMailService
    {
        Task SendEmailAsync(string serverId, string to, string subject, string body, bool isHtml = false);
        Task SendEmailAsync(string serverId, string from, string to, string subject, string body, bool isHtml = false);
        
        /// <summary>
        /// 测试SMTP服务器连接
        /// </summary>
        Task<(bool Success, string Message)> TestSmtpConnectionAsync(string serverId);
        
        /// <summary>
        /// 测试SMTP服务器连接（使用配置信息）
        /// </summary>
        Task<(bool Success, string Message)> TestSmtpConnectionAsync(string host, int port, string username, string password, bool enableSsl);
    }
}