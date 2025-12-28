using DynamicDbApi.Models;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 查询分析服务接口
    /// 用于记录查询模式并提供索引建议
    /// </summary>
    public interface IQueryAnalysisService
    {
        /// <summary>
        /// 记录查询模式
        /// </summary>
        /// <param name="request">查询请求</param>
        /// <param name="executionTime">执行时间（毫秒）</param>
        Task RecordQueryPatternAsync(DynamicQueryRequest request, long executionTime);

        /// <summary>
        /// 获取表的索引建议
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <returns>索引建议列表</returns>
        Task<List<IndexSuggestion>> GetIndexSuggestionsAsync(string tableName);

        /// <summary>
        /// 清除指定表的查询统计
        /// </summary>
        /// <param name="tableName">表名</param>
        Task ClearStatisticsAsync(string tableName);
    }

    /// <summary>
    /// 索引建议模型
    /// </summary>
    public class IndexSuggestion
    {
        /// <summary>
        /// 建议的索引名称
        /// </summary>
        public string IndexName { get; set; } = string.Empty;

        /// <summary>
        /// 索引的列名
        /// </summary>
        public List<string> Columns { get; set; } = new List<string>();

        /// <summary>
        /// 索引类型
        /// </summary>
        public string IndexType { get; set; } = "NONCLUSTERED";

        /// <summary>
        /// 建议理由
        /// </summary>
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// 预计性能提升百分比
        /// </summary>
        public int EstimatedPerformanceImprovement { get; set; }

        /// <summary>
        /// 生成的索引创建SQL
        /// </summary>
        public string CreateIndexSql { get; set; } = string.Empty;
    }
}
