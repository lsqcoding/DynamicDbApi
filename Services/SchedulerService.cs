using DynamicDbApi.Models;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using SqlSugar;
using System;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public class SchedulerService : ISchedulerService
    {
        private readonly IScheduler _scheduler;
        private readonly ISqlSugarClient _db;
        private readonly ILogger<SchedulerService> _logger;

        public SchedulerService(IScheduler scheduler, ISqlSugarClient db, ILogger<SchedulerService> logger)
        {
            _scheduler = scheduler;
            _db = db;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            await _scheduler.Start();
            _logger.LogInformation("Scheduler started");
        }

        public async Task StopAsync()
        {
            await _scheduler.Shutdown();
            _logger.LogInformation("Scheduler stopped");
        }

        public async Task ScheduleJobAsync(ScheduledTask task)
        {
            // 输入验证
            if (task == null)
                throw new ArgumentNullException(nameof(task));
                
            if (string.IsNullOrWhiteSpace(task.Name))
                throw new ArgumentException("Task name cannot be null or empty", nameof(task));
                
            if (string.IsNullOrWhiteSpace(task.TriggerType))
                throw new ArgumentException("TriggerType cannot be null or empty", nameof(task));
                
            if (string.IsNullOrWhiteSpace(task.TriggerExpression))
                throw new ArgumentException("TriggerExpression cannot be null or empty", nameof(task));

            // 验证任务名称唯一性
            var existingJob = await _scheduler.GetJobDetail(new JobKey(task.Name, "DynamicJobs"));
            if (existingJob != null)
                throw new InvalidOperationException($"Job with name '{task.Name}' already exists");

            try
            {
                var job = JobBuilder.Create<DynamicJob>()
                    .WithIdentity(task.Name, "DynamicJobs")
                    .UsingJobData("TaskId", task.Id)
                    // 添加JobData支持API调用配置
                    .UsingJobData("JobData", string.Empty)
                    .Build();

                ITrigger trigger;
                if (task.TriggerType.ToLower() == "cron")
                {
                    // 验证Cron表达式
                    if (!IsValidCronExpression(task.TriggerExpression))
                        throw new ArgumentException("Invalid cron expression", nameof(task.TriggerExpression));
                        
                    trigger = TriggerBuilder.Create()
                        .WithIdentity($"{task.Name}_Trigger", "DynamicTriggers")
                        .WithCronSchedule(task.TriggerExpression)
                        .Build();
                }
                else if (task.TriggerType.ToLower() == "simple")
                {
                    // 验证时间间隔
                    if (!TimeSpan.TryParse(task.TriggerExpression, out var interval))
                        throw new ArgumentException("Invalid time interval format", nameof(task.TriggerExpression));
                        
                    if (interval.TotalSeconds < 1)
                        throw new ArgumentException("Interval must be at least 1 second", nameof(task.TriggerExpression));

                    trigger = TriggerBuilder.Create()
                        .WithIdentity($"{task.Name}_Trigger", "DynamicTriggers")
                        .StartAt(task.StartTime ?? DateTime.Now)
                        .WithSimpleSchedule(x => x
                            .WithInterval(interval)
                            .RepeatForever())
                        .Build();
                }
                else
                {
                    throw new ArgumentException("Invalid trigger type. Must be 'Cron' or 'Simple'", nameof(task.TriggerType));
                }

                await _scheduler.ScheduleJob(job, trigger);
                _logger.LogInformation($"Job {task.Name} scheduled successfully with {task.TriggerType} trigger");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to schedule job {task.Name}");
                throw new InvalidOperationException($"Failed to schedule job: {ex.Message}", ex);
            }
        }

        private bool IsValidCronExpression(string cronExpression)
        {
            try
            {
                new CronExpression(cronExpression);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task PauseJobAsync(string jobName)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));
                
            await _scheduler.PauseJob(new JobKey(jobName, "DynamicJobs"));
            _logger.LogInformation($"Job {jobName} paused");
        }

        public async Task ResumeJobAsync(string jobName)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));
                
            await _scheduler.ResumeJob(new JobKey(jobName, "DynamicJobs"));
            _logger.LogInformation($"Job {jobName} resumed");
        }

        public async Task DeleteJobAsync(string jobName)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));
                
            await _scheduler.DeleteJob(new JobKey(jobName, "DynamicJobs"));
            _logger.LogInformation($"Job {jobName} deleted");
        }

        public async Task TriggerJobAsync(string jobName)
        {
            if (string.IsNullOrWhiteSpace(jobName))
                throw new ArgumentNullException(nameof(jobName));
                
            await _scheduler.TriggerJob(new JobKey(jobName, "DynamicJobs"));
            _logger.LogInformation($"Job {jobName} triggered manually");
        }
    }

    public class DynamicJob : IJob
    {
        private readonly ISqlSugarClient _db;
        private readonly ILogger<DynamicJob> _logger;
        private readonly IEnumerable<IJobExecutor> _jobExecutors;

        public DynamicJob(ISqlSugarClient db, ILogger<DynamicJob> logger, IEnumerable<IJobExecutor> jobExecutors)
        {
            _db = db;
            _logger = logger;
            _jobExecutors = jobExecutors;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var taskId = context.JobDetail.JobDataMap.GetInt("TaskId");
            var task = await _db.Queryable<ScheduledTask>().FirstAsync(t => t.Id == taskId);
            
            _logger.LogInformation($"执行任务 {task.Name}，类型: {task.JobType}");
            
            try
            {
                // 查找对应的任务执行器
                var executor = _jobExecutors.FirstOrDefault(e => e.JobType == task.JobType);
                
                if (executor != null)
                {
                    // 执行具体的任务逻辑
                    task.LastRunStatus = await executor.ExecuteAsync(task, context);
                    _logger.LogInformation($"任务 {task.Name} 执行成功，状态: {task.LastRunStatus}");
                }
                else
                {
                    _logger.LogWarning($"未找到任务类型 {task.JobType} 的执行器");
                    task.LastRunStatus = $"Failed: Unknown Job Type '{task.JobType}'";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"任务 {task.Name} 执行失败");
                task.LastRunStatus = $"Failed: {ex.Message}";
            }
            finally
            {
                // 更新任务状态
                task.LastRunTime = DateTime.UtcNow;
                task.NextRunTime = context.NextFireTimeUtc?.LocalDateTime;
                await _db.Updateable(task).ExecuteCommandAsync();
            }
            
            _logger.LogInformation($"任务 {task.Name} 执行完成");
        }
    }
}