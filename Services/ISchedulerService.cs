using DynamicDbApi.Models;
using Quartz;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    public interface ISchedulerService
    {
        Task StartAsync();
        Task StopAsync();
        Task ScheduleJobAsync(ScheduledTask task);
        Task PauseJobAsync(string jobName);
        Task ResumeJobAsync(string jobName);
        Task DeleteJobAsync(string jobName);
        Task TriggerJobAsync(string jobName);
    }
}