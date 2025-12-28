using System.Text.Json.Serialization;

namespace DynamicDbApi.Models
{
    /// <summary>
    /// 动态查询响应
    /// </summary>
    public class DynamicQueryResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        [JsonPropertyName("data")]
        public object? Data { get; set; }

        /// <summary>
        /// 总记录数
        /// </summary>
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        /// <summary>
        /// 当前页码
        /// </summary>
        [JsonPropertyName("currentPage")]
        public int? CurrentPage { get; set; }

        /// <summary>
        /// 每页大小
        /// </summary>
        [JsonPropertyName("pageSize")]
        public int? PageSize { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        [JsonPropertyName("totalPages")]
        public int? TotalPages { get; set; }

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static DynamicQueryResponse Ok(object? data = null, string? message = "操作成功", int? total = null, int? currentPage = null, int? pageSize = null)
        {
            var response = new DynamicQueryResponse
            {
                Success = true,
                Message = message,
                Data = data,
                Total = total
            };

            // 如果提供了分页参数，则设置分页相关字段
            if (currentPage.HasValue && pageSize.HasValue && total.HasValue)
            {
                response.CurrentPage = currentPage;
                response.PageSize = pageSize;
                response.TotalPages = (int)Math.Ceiling((double)total.Value / pageSize.Value);
            }

            return response;
        }

        /// <summary>
        /// 创建失败响应
        /// </summary>
        public static DynamicQueryResponse Fail(string message = "操作失败")
        {
            return new DynamicQueryResponse
            {
                Success = false,
                Message = message
            };
        }
    }
}