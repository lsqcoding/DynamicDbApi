using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 任务数据服务，用于设置和管理任务的具体参数
    /// </summary>
    public class JobDataService
    {
        private readonly IScheduler _scheduler;
        private readonly ILogger<JobDataService> _logger;
        
        public JobDataService(IScheduler scheduler, ILogger<JobDataService> logger)
        {
            _scheduler = scheduler;
            _logger = logger;
        }
        
        /// <summary>
        /// 设置任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="jobData">任务数据对象</param>
        public async Task<bool> SetJobDataAsync(int taskId, object jobData)
        {
            try
            {
                var jobKey = new JobKey($"job-{taskId}", "dynamic-jobs");
                var jobDetail = await _scheduler.GetJobDetail(jobKey);
                
                if (jobDetail != null)
                {
                    var jsonData = JsonConvert.SerializeObject(jobData);
                    await _scheduler.PauseJob(jobKey);
                    
                    // 更新作业数据
                    jobDetail.JobDataMap["JobData"] = jsonData;
                    
                    // 重新调度作业
                    await _scheduler.RescheduleJob(
                        new TriggerKey($"trigger-{taskId}", "dynamic-triggers"),
                        await _scheduler.GetTrigger(new TriggerKey($"trigger-{taskId}", "dynamic-triggers")));
                    
                    await _scheduler.ResumeJob(jobKey);
                    
                    _logger.LogInformation($"任务 {taskId} 的JobData已更新");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"设置任务 {taskId} 的JobData失败");
                return false;
            }
        }
        
        /// <summary>
        /// 获取任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>任务数据对象</returns>
        public async Task<object> GetJobDataAsync(int taskId)
        {
            try
            {
                var jobKey = new JobKey($"job-{taskId}", "dynamic-jobs");
                var jobDetail = await _scheduler.GetJobDetail(jobKey);
                
                if (jobDetail != null && jobDetail.JobDataMap.ContainsKey("JobData"))
                {
                    var jsonData = jobDetail.JobDataMap.GetString("JobData");
                    return JsonConvert.DeserializeObject(jsonData);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"获取任务 {taskId} 的JobData失败");
                return null;
            }
        }
    }
}