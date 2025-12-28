using DynamicDbApi.Models;
using FluentValidation;
using System;

namespace DynamicDbApi.Models.Validation
{
    public class ScheduledTaskValidator : AbstractValidator<ScheduledTask>
    {
        public ScheduledTaskValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("任务名称不能为空")
                .MaximumLength(100).WithMessage("任务名称不能超过100个字符")
                .Matches("^[a-zA-Z0-9_-]+$").WithMessage("任务名称只能包含字母、数字、下划线和连字符");

            RuleFor(x => x.Description)
                .MaximumLength(500).WithMessage("任务描述不能超过500个字符");

            RuleFor(x => x.TriggerType)
                .NotEmpty().WithMessage("触发器类型不能为空")
                .Must(x => x.ToLower() == "cron" || x.ToLower() == "simple")
                .WithMessage("触发器类型必须是 'Cron' 或 'Simple'");

            RuleFor(x => x.TriggerExpression)
                .NotEmpty().WithMessage("触发器表达式不能为空");

            RuleFor(x => x)
                .Must(x => ValidateTriggerExpression(x))
                .WithMessage("触发器表达式格式无效");

            RuleFor(x => x.StartTime)
                .Must((task, startTime) => startTime == null || startTime > DateTime.UtcNow)
                .WithMessage("开始时间必须大于当前时间");

            RuleFor(x => x.EndTime)
                .Must((task, endTime) => endTime == null || endTime > task.StartTime)
                .WithMessage("结束时间必须大于开始时间");
        }

        private bool ValidateTriggerExpression(ScheduledTask task)
        {
            if (string.IsNullOrWhiteSpace(task.TriggerType) || string.IsNullOrWhiteSpace(task.TriggerExpression))
                return false;

            if (task.TriggerType.ToLower() == "cron")
            {
                try
                {
                    new CronExpression(task.TriggerExpression);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            else if (task.TriggerType.ToLower() == "simple")
            {
                return TimeSpan.TryParse(task.TriggerExpression, out var interval) && interval.TotalSeconds >= 1;
            }

            return false;
        }
    }

    public class CronExpression
    {
        public CronExpression(string expression)
        {
            // 简化的Cron表达式验证
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Cron表达式不能为空");

            var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 && parts.Length != 6)
                throw new ArgumentException("Cron表达式格式无效");
        }
    }
}