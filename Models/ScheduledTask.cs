using System;

namespace DynamicDbApi.Models
{
    public class ScheduledTask
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JobType { get; set; } = string.Empty;
        public string TriggerType { get; set; } = "Cron"; // "Simple" or "Cron"
        public string TriggerExpression { get; set; } = string.Empty; // For cron expressions or interval
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastRunTime { get; set; }
        public DateTime? NextRunTime { get; set; }
        public string LastRunStatus { get; set; } = string.Empty;
    }
}