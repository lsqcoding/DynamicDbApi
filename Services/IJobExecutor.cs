using DynamicDbApi.Models;
using Quartz;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 任务执行器接口，定义不同类型任务的执行标准
    /// </summary>
    public interface IJobExecutor
    {
        /// <summary>
        /// 任务类型标识
        /// </summary>
        string JobType { get; }
        
        /// <summary>
        /// 执行任务
        /// </summary>
        /// <param name="task">定时任务信息</param>
        /// <param name="context">任务执行上下文</param>
        /// <returns>执行结果</returns>
        Task<string> ExecuteAsync(ScheduledTask task, IJobExecutionContext context);
    }
}