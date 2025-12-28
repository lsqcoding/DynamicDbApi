using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace DynamicDbApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JobManagementController : ControllerBase
    {
        private readonly ISchedulerService _schedulerService;
        private readonly JobDataService _jobDataService;
        private readonly ILogger<JobManagementController> _logger;
        
        public JobManagementController(
            ISchedulerService schedulerService,
            JobDataService jobDataService,
            ILogger<JobManagementController> logger)
        {
            _schedulerService = schedulerService;
            _jobDataService = jobDataService;
            _logger = logger;
        }
        
        /// <summary>
        /// 创建API调用任务
        /// </summary>
        /// <param name="request">任务创建请求</param>
        /// <returns>创建的任务ID</returns>
        [HttpPost("create-api-job")]
        public async Task<IActionResult> CreateApiJob([FromBody] CreateApiJobRequest request)
        {
            try
            {
                // 创建任务实体
                var task = new ScheduledTask
                {
                    Name = request.Name,
                    Description = request.Description,
                    JobType = "ApiJob",
                    TriggerType = request.TriggerType,
                    TriggerExpression = request.TriggerExpression,
                    IsActive = request.IsEnabled,
                    StartTime = request.StartTime,
                    EndTime = request.EndTime
                };
                
                // 如果没有提供描述但提供了API配置，则生成描述
                if (string.IsNullOrEmpty(task.Description) && request.ApiConfig != null)
                {
                    task.Description = $"API:{request.ApiConfig.ApiUrl},Method:{request.ApiConfig.Method ?? "GET"}";
                    if (request.ApiConfig.Body != null)
                    {
                        task.Description += $",Body:{Newtonsoft.Json.JsonConvert.SerializeObject(request.ApiConfig.Body)}";
                    }
                }
                
                // 创建任务
                await _schedulerService.ScheduleJobAsync(task);
                
                // 如果提供了API配置，设置任务数据
                if (request.ApiConfig != null && task.Id > 0)
                {
                    await _jobDataService.SetJobDataAsync(task.Id, request.ApiConfig);
                }
                
                return Ok(new { TaskId = task.Id, Message = "API任务创建成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建API任务失败");
                return StatusCode(500, new { Message = "创建任务失败", Error = ex.Message });
            }
        }
        
        /// <summary>
        /// 设置任务数据
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="apiConfig">API配置</param>
        /// <returns>操作结果</returns>
        [HttpPut("{taskId}/api-config")]
        public async Task<IActionResult> SetJobData(int taskId, [FromBody] ApiJobConfig apiConfig)
        {
            try
            {
                var success = await _jobDataService.SetJobDataAsync(taskId, apiConfig);
                
                if (success)
                {
                    return Ok(new { Message = "任务配置更新成功" });
                }
                else
                {
                    return NotFound(new { Message = "任务不存在或更新失败" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"更新任务 {taskId} 的API配置失败");
                return StatusCode(500, new { Message = "更新配置失败", Error = ex.Message });
            }
        }
        
        /// <summary>
        /// 立即触发任务执行
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>操作结果</returns>
        [HttpPost("{taskId}/trigger")]
        public async Task<IActionResult> TriggerJob(int taskId)
        {
            try
            {
                // 根据任务ID构建作业名称
                string jobName = $"Task_{taskId}";
                await _schedulerService.TriggerJobAsync(jobName);
                return Ok(new { Message = "任务已触发执行" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"触发任务 {taskId} 失败");
                return StatusCode(500, new { Message = "触发任务失败", Error = ex.Message });
            }
        }
        
        /// <summary>
        /// 创建API调用任务请求
        /// </summary>
        public class CreateApiJobRequest
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string TriggerType { get; set; } = "Cron";
            public string TriggerExpression { get; set; } = string.Empty;
            public bool IsEnabled { get; set; } = true;
            public DateTime? StartTime { get; set; }
            public DateTime? EndTime { get; set; }
            public int? Priority { get; set; }
            public ApiJobConfig? ApiConfig { get; set; }
        }
        
        /// <summary>
        /// API任务配置
        /// </summary>
        public class ApiJobConfig
        {
            public string ApiUrl { get; set; } = string.Empty;
            public string Method { get; set; } = "GET";
            public object? Body { get; set; }
        }
    }
}