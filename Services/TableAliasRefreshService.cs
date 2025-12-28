using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DynamicDbApi.Services;

/// <summary>
/// 表别名自动刷新服务，定期从数据库加载最新的表别名配置
/// </summary>
public class TableAliasRefreshService : BackgroundService
{
    private readonly ITableAliasService _tableAliasService;
    private readonly ILogger<TableAliasRefreshService> _logger;
    private readonly bool _isAutoRefreshEnabled;
    private readonly int _refreshIntervalSeconds;
    
    public TableAliasRefreshService(
        ITableAliasService tableAliasService,
        ILogger<TableAliasRefreshService> logger,
        IConfiguration configuration)
    {
        _tableAliasService = tableAliasService;
        _logger = logger;
        
        // 读取自动刷新配置
        var tableAliasConfig = configuration.GetSection("TableAliases");
        var autoRefreshConfig = tableAliasConfig.GetSection("AutoRefresh");
        _isAutoRefreshEnabled = autoRefreshConfig.GetValue<bool>("Enabled", false);
        _refreshIntervalSeconds = autoRefreshConfig.GetValue<int>("RefreshIntervalSeconds", 30);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 合并服务启动和功能状态日志
        if (!_isAutoRefreshEnabled)
        {
            _logger.LogInformation("Table alias auto-refresh feature is disabled");
            return;
        }
        
        _logger.LogInformation("Table alias auto-refresh feature is enabled, refresh interval: {0} seconds", _refreshIntervalSeconds);
        
        // Initial loading
        _tableAliasService.RefreshAliases();
        
        // Periodic refresh
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_refreshIntervalSeconds), stoppingToken);
                _logger.LogDebug("Starting to refresh table alias configuration...");
                _tableAliasService.RefreshAliases();
                _logger.LogDebug("Table alias configuration refresh completed");
            }
            catch (TaskCanceledException)
            {
                // Normal exception when service stops
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while refreshing table alias configuration");
            }
        }
        
        _logger.LogInformation("Table alias auto-refresh service has stopped");
    }
}