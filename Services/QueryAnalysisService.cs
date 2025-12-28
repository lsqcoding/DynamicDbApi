using DynamicDbApi.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 查询分析服务实现
    /// 用于记录查询模式并提供索引建议
    /// </summary>
    public class QueryAnalysisService : IQueryAnalysisService
    {
        private readonly ILogger<QueryAnalysisService> _logger;
        
        // 存储查询统计信息的内存缓存
        // key: 表名, value: 字段使用统计
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, FieldStatistics>> _tableFieldStatistics;

        public QueryAnalysisService(ILogger<QueryAnalysisService> logger)
        {
            _logger = logger;
            _tableFieldStatistics = new ConcurrentDictionary<string, ConcurrentDictionary<string, FieldStatistics>>();
        }

        /// <summary>
        /// 记录查询模式
        /// </summary>
        public async Task RecordQueryPatternAsync(DynamicQueryRequest request, long executionTime)
        {
            try
            {
                if (string.IsNullOrEmpty(request.TableName) || request.Operation != "select")
                {
                    return;
                }

                // 获取或创建表的字段统计
                var fieldStats = _tableFieldStatistics.GetOrAdd(request.TableName, 
                    _ => new ConcurrentDictionary<string, FieldStatistics>());

                // 记录WHERE条件中使用的字段
                if (request.WhereConditions != null)
                {
                    foreach (var condition in request.WhereConditions)
                    {
                        string fieldName = ExtractFieldNameFromCondition(condition.Key);
                        var stats = fieldStats.GetOrAdd(fieldName, _ => new FieldStatistics());
                        stats.WhereUsageCount++;
                        stats.LastUsed = DateTime.UtcNow;
                    }
                }

                // 记录ORDER BY中使用的字段
                if (request.OrderBy != null)
                {
                    foreach (var order in request.OrderBy)
                    {
                        string fieldName = order.Key;
                        var stats = fieldStats.GetOrAdd(fieldName, _ => new FieldStatistics());
                        stats.OrderByUsageCount++;
                        stats.LastUsed = DateTime.UtcNow;
                    }
                }

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "记录查询模式时发生错误");
            }
        }

        /// <summary>
        /// 获取表的索引建议
        /// </summary>
        public async Task<List<IndexSuggestion>> GetIndexSuggestionsAsync(string tableName)
        {
            var suggestions = new List<IndexSuggestion>();

            try
            {
                if (_tableFieldStatistics.TryGetValue(tableName, out var fieldStats) && fieldStats.Count > 0)
                {
                    // 排序字段，优先考虑频繁用于WHERE和ORDER BY的字段
                    var sortedFields = fieldStats.OrderByDescending(f => f.Value.TotalUsage)
                        .ThenByDescending(f => f.Value.LastUsed)
                        .ToList();

                    // 生成单列索引建议（对于使用频率高的字段）
                    foreach (var field in sortedFields.Where(f => f.Value.WhereUsageCount > 10))
                    {
                        var suggestion = CreateIndexSuggestion(tableName, new List<string> { field.Key }, field.Value);
                        suggestions.Add(suggestion);
                    }

                    // 生成组合索引建议（基于最常用的字段组合）
                    if (sortedFields.Count >= 2)
                    {
                        // 这里可以实现更复杂的组合索引逻辑
                        // 简单示例：使用前两个最常用的字段创建组合索引
                        var topFields = sortedFields.Take(2).Select(f => f.Key).ToList();
                        var combinedStats = new FieldStatistics
                        {
                            WhereUsageCount = sortedFields[0].Value.WhereUsageCount + sortedFields[1].Value.WhereUsageCount,
                            OrderByUsageCount = sortedFields[0].Value.OrderByUsageCount + sortedFields[1].Value.OrderByUsageCount,
                            LastUsed = DateTime.UtcNow
                        };
                        
                        var compositeSuggestion = CreateIndexSuggestion(tableName, topFields, combinedStats);
                        compositeSuggestion.IndexType = "NONCLUSTERED";
                        compositeSuggestion.Reason = "基于最常用的两个查询字段创建的组合索引，可提高多条件查询性能";
                        suggestions.Add(compositeSuggestion);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成索引建议时发生错误");
            }

            return await Task.FromResult(suggestions);
        }

        /// <summary>
        /// 清除指定表的查询统计
        /// </summary>
        public async Task ClearStatisticsAsync(string tableName)
        {
            try
            {
                _tableFieldStatistics.TryRemove(tableName, out _);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清除查询统计时发生错误");
            }
        }

        /// <summary>
        /// 从条件键中提取字段名（处理操作符后缀）
        /// </summary>
        private string ExtractFieldNameFromCondition(string conditionKey)
        {
            // 处理操作符后缀，如 "name__like" -> "name"
            if (conditionKey.Contains("__"))
            {
                return conditionKey.Split("__")[0];
            }
            return conditionKey;
        }

        /// <summary>
        /// 创建索引建议对象
        /// </summary>
        private IndexSuggestion CreateIndexSuggestion(string tableName, List<string> columns, FieldStatistics stats)
        {
            string indexName = GenerateIndexName(tableName, columns);
            string columnsStr = string.Join(", ", columns);
            string createSql = $"CREATE NONCLUSTERED INDEX [{indexName}] ON [{tableName}] ({columnsStr})";

            // 计算预计性能提升百分比（简单估算）
            int estimatedImprovement = Math.Min(90, (int)(stats.TotalUsage * 2));

            return new IndexSuggestion
            {
                IndexName = indexName,
                Columns = columns,
                IndexType = "NONCLUSTERED",
                Reason = $"该字段在查询中频繁使用（WHERE: {stats.WhereUsageCount}次, ORDER BY: {stats.OrderByUsageCount}次），创建索引可显著提高查询性能",
                EstimatedPerformanceImprovement = estimatedImprovement,
                CreateIndexSql = createSql
            };
        }

        /// <summary>
        /// 生成索引名称
        /// </summary>
        private string GenerateIndexName(string tableName, List<string> columns)
        {
            string columnsPart = string.Join("_", columns);
            return $"IX_{tableName}_{columnsPart}_Query";
        }

        /// <summary>
        /// 字段统计信息
        /// </summary>
        private class FieldStatistics
        {
            public int WhereUsageCount { get; set; } = 0;
            public int OrderByUsageCount { get; set; } = 0;
            public DateTime LastUsed { get; set; } = DateTime.UtcNow;
            
            // 总使用次数
            public int TotalUsage => WhereUsageCount + OrderByUsageCount;
        }
    }
}
