using System.Text.Json.Serialization;

namespace DynamicDbApi.Models
{
    /// <summary>
    /// 动态查询请求
    /// </summary>
    public class DynamicQueryRequest
    {
        /// <summary>
        /// 数据库连接ID
        /// </summary>
        [JsonPropertyName("dbId")]
        public string? DatabaseId { get; set; }

        /// <summary>
        /// 表名
        /// </summary>
        [JsonPropertyName("table")]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 主表别名
        /// </summary>
        [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        /// <summary>
        /// 操作类型
        /// </summary>
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = "select";

        /// <summary>
        /// 查询条件
        /// </summary>
        [JsonPropertyName("where")]
        public Dictionary<string, object>? WhereConditions { get; set; }

        /// <summary>
        /// 排序条件
        /// </summary>
        [JsonPropertyName("orderBy")]
        public Dictionary<string, string>? OrderBy { get; set; }

        /// <summary>
        /// 分页信息
        /// </summary>
        [JsonPropertyName("page")]
        public PageInfo? Page { get; set; }

        /// <summary>
        /// 要查询的字段
        /// </summary>
        [JsonPropertyName("columns")]
        public List<string>? Columns { get; set; }

        /// <summary>
        /// 要插入或更新的数据（单个）
        /// </summary>
        [JsonPropertyName("data")]
        public Dictionary<string, object>? Data { get; set; }

        /// <summary>
        /// 要插入或更新的批量数据
        /// </summary>
        [JsonPropertyName("dataList")]
        public List<Dictionary<string, object>>? DataList { get; set; }

        /// <summary>
        /// 关联查询
        /// </summary>
        [JsonPropertyName("joins")]
        public List<JoinInfo>? Joins { get; set; }

        /// <summary>
        /// 自定义返回值查询配置（增删改操作时使用）
        /// </summary>
        [JsonPropertyName("returnQuery")]
        public DynamicQueryRequest? ReturnQuery { get; set; }

        /// <summary>
        /// 是否使用DISTINCT去重
        /// </summary>
        [JsonPropertyName("distinct")]
        public bool Distinct { get; set; }

        /// <summary>
        /// HAVING过滤条件
        /// </summary>
        [JsonPropertyName("having")]
        public string? HavingCondition { get; set; }

        /// <summary>
        /// 分组字段
        /// </summary>
        [JsonPropertyName("groupBy")]
        public List<string>? GroupBy { get; set; }

        /// <summary>
        /// UNION查询配置
        /// </summary>
        [JsonPropertyName("union")]
        public UnionQueryConfig? UnionQuery { get; set; }
        
        /// <summary>
        /// CTE（通用表表达式）查询配置
        /// </summary>
        [JsonPropertyName("cte")]
        public CteQueryConfig? CteQuery { get; set; }
        
        /// <summary>
        /// 查询参数
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, object>? Parameters { get; set; }
    }

    /// <summary>
    /// CTE（通用表表达式）查询配置
    /// </summary>
    public class CteQueryConfig
    {
        /// <summary>
        /// CTE定义列表
        /// </summary>
        [JsonPropertyName("definitions")]
        public List<CteDefinition> Definitions { get; set; } = new List<CteDefinition>();
    }
    
    /// <summary>
    /// CTE定义
    /// </summary>
    public class CteDefinition
    {
        /// <summary>
        /// CTE名称
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// CTE查询内容
        /// </summary>
        [JsonPropertyName("query")]
        public string Query { get; set; } = string.Empty;
        
        /// <summary>
        /// 是否递归CTE
        /// </summary>
        [JsonPropertyName("recursive")]
        public bool IsRecursive { get; set; }
    }

    /// <summary>
    /// UNION查询配置
    /// </summary>
    public class UnionQueryConfig
    {
        /// <summary>
        /// 是否使用UNION ALL（保留重复行）
        /// </summary>
        [JsonPropertyName("all")]
        public bool UseUnionAll { get; set; }

        /// <summary>
        /// 子查询列表
        /// </summary>
        [JsonPropertyName("queries")]
        public List<DynamicQueryRequest> SubQueries { get; set; } = new List<DynamicQueryRequest>();
    }

    /// <summary>
    /// 分页信息
    /// </summary>
    public class PageInfo
    {
        /// <summary>
        /// 页码，从1开始
        /// </summary>
        [JsonPropertyName("index")]
        public int PageIndex { get; set; } = 1;

        /// <summary>
        /// 每页记录数
        /// </summary>
        [JsonPropertyName("size")]
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// 关联查询信息
    /// </summary>
    public class JoinInfo
    {
        /// <summary>
        /// 关联表名
        /// </summary>
        [JsonPropertyName("table")]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 表别名
        /// </summary>
        [JsonPropertyName("alias")]
        public string? Alias { get; set; }

        /// <summary>
        /// 关联类型
        /// </summary>
        [JsonPropertyName("type")]
        public string JoinType { get; set; } = "inner";

        /// <summary>
        /// 关联条件
        /// </summary>
        [JsonPropertyName("on")]
        public Dictionary<string, string> OnConditions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 要查询的字段
        /// </summary>
        [JsonPropertyName("columns")]
        public List<string>? Columns { get; set; }
    }
}