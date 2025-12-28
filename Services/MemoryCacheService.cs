using System;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 基于内存的缓存服务实现
    /// </summary>
    public class MemoryCacheService : ICacheService
    {
        private readonly ObjectCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志记录器</param>
        public MemoryCacheService(ILogger<MemoryCacheService> logger)
        {
            _cache = MemoryCache.Default;
            _logger = logger;
        }

        /// <summary>
        /// 获取缓存项
        /// </summary>
        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var result = _cache.Get(key);
                return result is T ? (T)result : default;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取缓存项失败: {Key}", key);
                return default;
            }
        }

        /// <summary>
        /// 设置缓存项
        /// </summary>
        public async Task SetAsync<T>(string key, T value, TimeSpan expirationTime)
        {
            try
            {
                var policy = new CacheItemPolicy
                {
                    AbsoluteExpiration = DateTimeOffset.UtcNow.Add(expirationTime)
                };
                _cache.Set(key, value, policy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置缓存项失败: {Key}", key);
            }
        }

        /// <summary>
        /// 删除缓存项
        /// </summary>
        public async Task RemoveAsync(string key)
        {
            try
            {
                _cache.Remove(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除缓存项失败: {Key}", key);
            }
        }

        /// <summary>
        /// 检查缓存项是否存在
        /// </summary>
        public async Task<bool> ExistsAsync(string key)
        {
            try
            {
                return _cache.Contains(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查缓存项存在性失败: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// 根据前缀删除缓存项
        /// </summary>
        public async Task RemoveByPrefixAsync(string prefix)
        {
            try
            {
                foreach (var item in _cache)
                {
                    if (item.Key.ToString().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        _cache.Remove(item.Key.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "根据前缀删除缓存项失败: {Prefix}", prefix);
            }
        }
    }
}