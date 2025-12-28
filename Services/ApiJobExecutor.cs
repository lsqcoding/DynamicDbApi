using DynamicDbApi.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Quartz;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// API接口任务执行器，用于调用项目中的自定义接口
    /// </summary>
    public class ApiJobExecutor : IJobExecutor
    {
        public string JobType => "ApiJob";
        
        private readonly ILogger<ApiJobExecutor> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        
        public ApiJobExecutor(ILogger<ApiJobExecutor> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }
        
        public async Task<string> ExecuteAsync(ScheduledTask task, IJobExecutionContext context)
        {
            try
            {
                // 解析任务参数，格式应为：{"ApiUrl":"/api/xxx/xxx","Method":"GET|POST","Body":{...}}
                var jobData = context.JobDetail.JobDataMap.GetString("JobData") ?? string.Empty;
                
                ApiJobConfig config = null;
                if (!string.IsNullOrEmpty(jobData))
                {
                    config = JsonConvert.DeserializeObject<ApiJobConfig>(jobData);
                }
                
                if (config == null || string.IsNullOrEmpty(config.ApiUrl))
                {
                    // 尝试从任务描述中解析API信息
                    if (TryParseApiInfoFromDescription(task.Description, out config))
                    {
                        _logger.LogInformation($"从任务描述中解析API信息: {config.ApiUrl}");
                    }
                    else
                    {
                        throw new System.Exception("无法解析API配置信息，请在任务参数或描述中指定API URL");
                    }
                }
                
                _logger.LogInformation($"开始执行API任务: {task.Name}, URL: {config.ApiUrl}");
                
                // 创建HttpClient实例
                var client = _httpClientFactory.CreateClient();
                
                // 设置基础URL为当前服务地址
                // 注意：在实际部署环境中，应该使用配置的服务地址
                client.BaseAddress = new System.Uri("http://localhost:5000");
                
                // 设置超时时间
                client.Timeout = TimeSpan.FromSeconds(300); // 5分钟超时
                
                // 创建请求消息
                var request = new HttpRequestMessage(new HttpMethod(config.Method ?? "GET"), config.ApiUrl);
                
                // 添加请求体
                if (!string.IsNullOrEmpty(config.Method) && config.Method.ToUpper() == "POST" && config.Body != null)
                {
                    request.Content = new StringContent(
                        JsonConvert.SerializeObject(config.Body),
                        Encoding.UTF8,
                        "application/json");
                }
                
                // 执行请求
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                // 读取响应内容
                var responseContent = await response.Content.ReadAsStringAsync();
                
                _logger.LogInformation($"API任务执行成功: {task.Name}");
                return "Success: API调用成功";
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, $"API任务执行失败: {task.Name}");
                return $"Failed: {ex.Message}";
            }
        }
        
        /// <summary>
        /// 尝试从任务描述中解析API信息
        /// 格式示例：API:/api/CustomQuery/query,Method:POST,Body:{"db":"default","table":"Users"}
        /// </summary>
        private bool TryParseApiInfoFromDescription(string description, out ApiJobConfig config)
        {
            config = new ApiJobConfig();
            
            if (string.IsNullOrEmpty(description))
                return false;
            
            try
            {
                // 解析API URL
                var apiUrlPattern = "API:(.+?)(,|$)";
                var match = System.Text.RegularExpressions.Regex.Match(description, apiUrlPattern);
                if (match.Success)
                {
                    config.ApiUrl = match.Groups[1].Value.Trim();
                }
                else
                {
                    return false;
                }
                
                // 解析Method
                var methodPattern = "Method:(.+?)(,|$)";
                match = System.Text.RegularExpressions.Regex.Match(description, methodPattern);
                if (match.Success)
                {
                    config.Method = match.Groups[1].Value.Trim();
                }
                else
                {
                    config.Method = "GET";
                }
                
                // 解析Body
                var bodyPattern = "Body:(.+)(,|$)";
                match = System.Text.RegularExpressions.Regex.Match(description, bodyPattern);
                if (match.Success)
                {
                    var bodyJson = match.Groups[1].Value.Trim();
                    config.Body = JsonConvert.DeserializeObject(bodyJson);
                }
                
                return !string.IsNullOrEmpty(config.ApiUrl);
            }
            catch
            {
                return false;
            }
        }
    }
    
    /// <summary>
    /// API任务配置
    /// </summary>
    public class ApiJobConfig
    {
        public string ApiUrl { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public object Body { get; set; } = null;
    }
}