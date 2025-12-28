namespace DynamicDbApi.Models
{
    /// <summary>
    /// 限流配置选项
    /// </summary>
    public class RateLimitingOptions
    {
        /// <summary>
        /// 是否启用限流
        /// </summary>
        public bool Enabled { get; set; }
        
        /// <summary>
        /// 限流拒绝状态码
        /// </summary>
        public int RejectionStatusCode { get; set; }
        
        /// <summary>
        /// 默认限流策略
        /// </summary>
        public FixedWindowPolicyOptions DefaultPolicy { get; set; }
        
        /// <summary>
        /// 查询操作限流策略
        /// </summary>
        public FixedWindowPolicyOptions QueryPolicy { get; set; }
    }
    
    /// <summary>
    /// 固定窗口限流策略配置
    /// </summary>
    public class FixedWindowPolicyOptions
    {
        /// <summary>
        /// 策略类型
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// 窗口大小（秒）
        /// </summary>
        public int WindowInSeconds { get; set; }
        
        /// <summary>
        /// 允许的请求数量
        /// </summary>
        public int PermitLimit { get; set; }
        
        /// <summary>
        /// 队列长度
        /// </summary>
        public int QueueLimit { get; set; }
    }
}