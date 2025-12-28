using DynamicDbApi.Models;
using DynamicDbApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using SqlSugar;

namespace DynamicDbApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SchedulerController : ControllerBase
    {
        private readonly ISchedulerService _schedulerService;
        private readonly ISqlSugarClient _db;
        private readonly ILogger<SchedulerController> _logger;

        public SchedulerController(
            ISchedulerService schedulerService,
            ISqlSugarClient db,
            ILogger<SchedulerController> logger)
        {
            _schedulerService = schedulerService;
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// 启动定时任务调度器
        /// </summary>
        /// <remarks>
        /// 启动Quartz.NET定时任务调度器，开始执行所有已配置的定时任务。
        /// 
        /// 注意：调度器启动后会自动执行所有激活状态的任务。
        /// </remarks>
        /// <returns>操作结果</returns>
        /// <response code="200">调度器启动成功</response>
        /// <response code="500">调度器启动失败</response>
        [HttpPost("start")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> StartScheduler()
        {
            try
            {
                await _schedulerService.StartAsync();
                return Ok(new { success = true, message = "Scheduler started" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start scheduler");
                return StatusCode(500, new { success = false, message = $"Failed to start scheduler: {ex.Message}" });
            }
        }

        [HttpPost("stop")]
        public async Task<IActionResult> StopScheduler()
        {
            try
            {
                await _schedulerService.StopAsync();
                return Ok(new { success = true, message = "Scheduler stopped" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop scheduler");
                return StatusCode(500, new { success = false, message = $"Failed to stop scheduler: {ex.Message}" });
            }
        }

        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask([FromBody] ScheduledTask task)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.Name)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "未授权的访问" });
                }

                await _db.Insertable(task).ExecuteCommandAsync();
                await _schedulerService.ScheduleJobAsync(task);

                return Ok(new { success = true, message = "Task created and scheduled" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create task");
                return StatusCode(500, new { success = false, message = $"Failed to create task: {ex.Message}" });
            }
        }

        [HttpPost("tasks/{id}/pause")]
        public async Task<IActionResult> PauseTask(int id)
        {
            try
            {
                var task = await _db.Queryable<ScheduledTask>().FirstAsync(t => t.Id == id);
                await _schedulerService.PauseJobAsync(task.Name);
                return Ok(new { success = true, message = $"Task {task.Name} paused" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to pause task");
                return StatusCode(500, new { success = false, message = $"Failed to pause task: {ex.Message}" });
            }
        }

        [HttpPost("tasks/{id}/resume")]
        public async Task<IActionResult> ResumeTask(int id)
        {
            try
            {
                var task = await _db.Queryable<ScheduledTask>().FirstAsync(t => t.Id == id);
                await _schedulerService.ResumeJobAsync(task.Name);
                return Ok(new { success = true, message = $"Task {task.Name} resumed" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume task");
                return StatusCode(500, new { success = false, message = $"Failed to resume task: {ex.Message}" });
            }
        }

        [HttpDelete("tasks/{id}")]
        public async Task<IActionResult> DeleteTask(int id)
        {
            try
            {
                var task = await _db.Queryable<ScheduledTask>().FirstAsync(t => t.Id == id);
                await _schedulerService.DeleteJobAsync(task.Name);
                await _db.Deleteable<ScheduledTask>().Where(t => t.Id == id).ExecuteCommandAsync();
                return Ok(new { success = true, message = $"Task {task.Name} deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete task");
                return StatusCode(500, new { success = false, message = $"Failed to delete task: {ex.Message}" });
            }
        }

        [HttpPost("tasks/{id}/trigger")]
        public async Task<IActionResult> TriggerTask(int id)
        {
            try
            {
                var task = await _db.Queryable<ScheduledTask>().FirstAsync(t => t.Id == id);
                await _schedulerService.TriggerJobAsync(task.Name);
                return Ok(new { success = true, message = $"Task {task.Name} triggered" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger task");
                return StatusCode(500, new { success = false, message = $"Failed to trigger task: {ex.Message}" });
            }
        }

        [HttpGet("tasks")]
        public async Task<IActionResult> GetTasks()
        {
            try
            {
                var tasks = await _db.Queryable<ScheduledTask>().ToListAsync();
                return Ok(new { success = true, data = tasks });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get tasks");
                return StatusCode(500, new { success = false, message = $"Failed to get tasks: {ex.Message}" });
            }
        }
    }
}