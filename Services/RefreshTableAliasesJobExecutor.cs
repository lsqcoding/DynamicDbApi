using DynamicDbApi.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Threading.Tasks;

namespace DynamicDbApi.Services;

/// <summary>
/// 刷新表别名任务执行器，用于定期从数据库加载最新的表别名配置
/// </summary>
public class RefreshTableAliasesJobExecutor : IJobExecutor
{
    public string JobType => "RefreshTableAliasesJob";
    
    private readonly ITableAliasService _tableAliasService;
    private readonly ILogger<RefreshTableAliasesJobExecutor> _logger;
    
    public RefreshTableAliasesJobExecutor(
        ITableAliasService tableAliasService,
        ILogger<RefreshTableAliasesJobExecutor> logger)
    {
        _tableAliasService = tableAliasService;
        _logger = logger;
    }
    
    public async Task<string> ExecuteAsync(ScheduledTask task, IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("开始刷新表别名配置...");
            
            // 调用TableAliasService的RefreshAliases方法刷新别名
            _tableAliasService.RefreshAliases();
            
            _logger.LogInformation("表别名配置刷新成功");
            return "表别名配置刷新成功";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新表别名配置失败");
            return $"刷新失败: {ex.Message}";
        }
    }
}
