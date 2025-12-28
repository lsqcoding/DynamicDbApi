using DynamicDbApi.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using SqlSugar;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public class MailService : IMailService
    {
        private readonly ISqlSugarClient _db;
        private readonly ILogger<MailService> _logger;

        public MailService(ISqlSugarClient db, ILogger<MailService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task SendEmailAsync(string serverId, string to, string subject, string body, bool isHtml = false)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentNullException(nameof(serverId));
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentNullException(nameof(to));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentNullException(nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentNullException(nameof(body));
            
            var server = await _db.Queryable<MailServer>()
                .Where(s => s.Name == serverId || (serverId == "default" && s.IsDefault))
                .FirstAsync();

            if (server == null)
            {
                throw new Exception($"Mail server {serverId} not found");
            }

            await SendEmailAsync(server, server.DefaultFrom, to, subject, body, isHtml);
        }

        public async Task SendEmailAsync(string serverId, string from, string to, string subject, string body, bool isHtml = false)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                throw new ArgumentNullException(nameof(serverId));
            if (string.IsNullOrWhiteSpace(from))
                throw new ArgumentNullException(nameof(from));
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentNullException(nameof(to));
            if (string.IsNullOrWhiteSpace(subject))
                throw new ArgumentNullException(nameof(subject));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentNullException(nameof(body));
            
            var server = await _db.Queryable<MailServer>()
                .Where(s => s.Name == serverId || (serverId == "default" && s.IsDefault))
                .FirstAsync();

            if (server == null)
            {
                throw new Exception($"Mail server {serverId} not found");
            }

            await SendEmailAsync(server, from, to, subject, body, isHtml);
        }

        private async Task SendEmailAsync(MailServer server, string? from, string to, string subject, string body, bool isHtml)
        {
            from ??= server.DefaultFrom;
            
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(server.DisplayName ?? "DynamicDB API", from!));
                email.To.Add(MailboxAddress.Parse(to));
                email.Subject = subject;
                email.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(server.Host, server.Port, server.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                await smtp.AuthenticateAsync(server.UserName, server.Password);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                _logger.LogInformation($"Email sent to {to} using server {server.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {to}");
                throw;
            }
        }

        public async Task<(bool Success, string Message)> TestSmtpConnectionAsync(string serverId)
        {
            try
            {
                var server = await _db.Queryable<MailServer>()
                    .Where(s => s.Name == serverId || (serverId == "default" && s.IsDefault))
                    .FirstAsync();

                if (server == null)
                {
                    return (false, $"邮件服务器 {serverId} 未找到");
                }

                return await TestSmtpConnectionAsync(server.Host!, server.Port, server.UserName!, server.Password!, server.EnableSsl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"测试SMTP连接失败: {serverId}");
                return (false, $"测试SMTP连接失败: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> TestSmtpConnectionAsync(string host, int port, string username, string password, bool enableSsl)
        {
            try
            {
                using var smtp = new SmtpClient();
                
                // 设置超时时间（10秒）
                smtp.Timeout = 10000;
                
                // 验证输入参数
                if (string.IsNullOrWhiteSpace(host))
                    return (false, "SMTP服务器地址不能为空");
                if (port <= 0 || port > 65535)
                    return (false, "SMTP端口号无效");
                if (string.IsNullOrWhiteSpace(username))
                    return (false, "SMTP用户名不能为空");
                if (string.IsNullOrWhiteSpace(password))
                    return (false, "SMTP密码不能为空");
                
                await smtp.ConnectAsync(host, port, enableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
                
                // 测试认证
                await smtp.AuthenticateAsync(username, password);
                
                // 测试连接是否有效
                if (!smtp.IsConnected)
                {
                    return (false, "SMTP连接失败");
                }
                
                // 获取服务器能力信息
                var capabilities = smtp.Capabilities;
                var serverInfo = $"服务器支持: {string.Join(", ", capabilities)}";
                
                // 断开连接
                await smtp.DisconnectAsync(true);
                
                _logger.LogInformation($"SMTP连接测试成功: {host}:{port}");
                return (true, $"SMTP连接测试成功. {serverInfo}");
            }
            catch (AuthenticationException authEx)
            {
                _logger.LogError(authEx, $"SMTP认证失败: {host}:{port}");
                return (false, $"SMTP认证失败: {authEx.Message}");
            }
            catch (SmtpCommandException smtpEx)
            {
                _logger.LogError(smtpEx, $"SMTP命令错误: {host}:{port}");
                return (false, $"SMTP命令错误: {smtpEx.Message}");
            }
            catch (SocketException socketEx)
            {
                _logger.LogError(socketEx, $"网络连接失败: {host}:{port}");
                return (false, $"网络连接失败: {socketEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"SMTP连接测试失败: {host}:{port}");
                return (false, $"SMTP连接测试失败: {ex.Message}");
            }
        }
    }
}