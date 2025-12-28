using System.Net;
using DynamicDbApi.Data;
using DynamicDbApi.Models;

namespace DynamicDbApi.Infrastructure
{
    /// <summary>
    /// IP白名单中间件
    /// </summary>
    public class IpWhitelistMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<IpWhitelistMiddleware> _logger;
        private readonly IConfiguration _configuration;
        private readonly List<string> _whitelistedIps = new();
        private readonly List<IPNetwork> _whitelistedNetworks = new();

        public IpWhitelistMiddleware(
            RequestDelegate next,
            ILogger<IpWhitelistMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// 初始化IP白名单
        /// </summary>
        private void InitializeWhitelist()
        {
            // 从配置文件获取IP白名单
            var ipWhitelist = _configuration.GetSection("IpWhitelist").Get<List<string>>();
            if (ipWhitelist != null && ipWhitelist.Count > 0)
            {
                foreach (var ip in ipWhitelist)
                {
                    AddIpToWhitelist(ip);
                }
            }
            else
            {
                // 如果没有配置白名单，则允许所有IP访问
                _logger.LogWarning("未在配置文件中找到IP白名单配置");
            }
        }

        /// <summary>
        /// 添加IP到白名单
        /// </summary>
        private void AddIpToWhitelist(string ip)
        {
            if (ip.Contains("/"))
            {
                // CIDR格式的网段
                try
                {
                    var network = IPNetwork.Parse(ip);
                    _whitelistedNetworks.Add(network);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"解析CIDR格式的IP网段失败: {ip}");
                }
            }
            else
            {
                // 单个IP地址
                _whitelistedIps.Add(ip);
            }
        }

        /// <summary>
        /// 中间件处理方法
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // 初始化IP白名单（仅在首次请求时执行）
            if (_whitelistedIps.Count == 0 && _whitelistedNetworks.Count == 0)
            {
                InitializeWhitelist();
            }

            // 如果白名单为空，则允许所有IP访问
            if (_whitelistedIps.Count == 0 && _whitelistedNetworks.Count == 0)
            {
                await _next(context);
                return;
            }

            // 获取客户端IP地址
            var clientIp = context.Connection.RemoteIpAddress;
            if (clientIp == null)
            {
                _logger.LogWarning("无法获取客户端IP地址");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("访问被拒绝");
                return;
            }

            // 检查IP是否在白名单中
            var isWhitelisted = IsIpWhitelisted(clientIp);
            if (!isWhitelisted)
            {
                _logger.LogWarning($"IP地址 {clientIp} 不在白名单中，访问被拒绝");
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("访问被拒绝");
                return;
            }

            await _next(context);
        }

        /// <summary>
        /// 检查IP是否在白名单中
        /// </summary>
        private bool IsIpWhitelisted(IPAddress clientIp)
        {
            // 检查单个IP地址
            if (_whitelistedIps.Contains(clientIp.ToString()))
            {
                return true;
            }

            // 检查IP网段
            foreach (var network in _whitelistedNetworks)
            {
                if (network.Contains(clientIp))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// IP网段
    /// </summary>
    public class IPNetwork
    {
        public IPAddress NetworkAddress { get; private set; }
        public IPAddress SubnetMask { get; private set; }
        public int Cidr { get; private set; }

        private IPNetwork(IPAddress networkAddress, IPAddress subnetMask, int cidr)
        {
            NetworkAddress = networkAddress;
            SubnetMask = subnetMask;
            Cidr = cidr;
        }

        /// <summary>
        /// 解析CIDR格式的IP网段
        /// </summary>
        public static IPNetwork Parse(string cidrNotation)
        {
            var parts = cidrNotation.Split('/');
            if (parts.Length != 2)
            {
                throw new ArgumentException("CIDR格式不正确", nameof(cidrNotation));
            }

            var ipAddress = IPAddress.Parse(parts[0]);
            var cidr = int.Parse(parts[1]);

            if (cidr < 0 || cidr > 32)
            {
                throw new ArgumentException("CIDR值必须在0-32之间", nameof(cidrNotation));
            }

            var subnetMask = CalculateSubnetMask(cidr);
            return new IPNetwork(ipAddress, subnetMask, cidr);
        }

        /// <summary>
        /// 计算子网掩码
        /// </summary>
        private static IPAddress CalculateSubnetMask(int cidr)
        {
            var mask = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (cidr >= 8)
                {
                    mask[i] = 255;
                    cidr -= 8;
                }
                else if (cidr > 0)
                {
                    mask[i] = (byte)(256 - (1 << (8 - cidr)));
                    cidr = 0;
                }
                else
                {
                    mask[i] = 0;
                }
            }
            return new IPAddress(mask);
        }

        /// <summary>
        /// 检查IP地址是否在网段内
        /// </summary>
        public bool Contains(IPAddress ipAddress)
        {
            if (ipAddress.AddressFamily != NetworkAddress.AddressFamily)
            {
                return false;
            }

            var ipBytes = ipAddress.GetAddressBytes();
            var networkBytes = NetworkAddress.GetAddressBytes();
            var maskBytes = SubnetMask.GetAddressBytes();

            for (int i = 0; i < ipBytes.Length; i++)
            {
                if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// IP白名单中间件扩展
    /// </summary>
    public static class IpWhitelistMiddlewareExtensions
    {
        public static IApplicationBuilder UseIpWhitelist(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<IpWhitelistMiddleware>();
        }
    }
}