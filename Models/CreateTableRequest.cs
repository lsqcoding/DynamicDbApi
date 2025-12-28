using System.Text.Json.Serialization;

namespace DynamicDbApi.Models
{
    /// <summary>
    /// 动态建表请求
    /// </summary>
    public class CreateTableRequest
    {
        /// <summary>
        /// 数据库连接ID
        /// </summary>
        [JsonPropertyName("dbId")]
        public string DatabaseId { get; set; } = "default";

        /// <summary>
        /// 表名
        /// </summary>
        [JsonPropertyName("table")]
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// 列定义
        /// </summary>
        [JsonPropertyName("columns")]
        public List<ColumnDefinition> Columns { get; set; } = new List<ColumnDefinition>();
    }

    /// <summary>
    /// 列定义
    /// </summary>
    public class ColumnDefinition
    {
        /// <summary>
        /// 列名
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据类型
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 是否为主键
        /// </summary>
        [JsonPropertyName("primaryKey")]
        public bool IsPrimaryKey { get; set; } = false;

        /// <summary>
        /// 是否自增
        /// </summary>
        [JsonPropertyName("autoIncrement")]
        public bool IsAutoIncrement { get; set; } = false;

        /// <summary>
        /// 是否可为空
        /// </summary>
        [JsonPropertyName("nullable")]
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// 默认值
        /// </summary>
        [JsonPropertyName("defaultValue")]
        public object? DefaultValue { get; set; } = null;

        /// <summary>
        /// 描述
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;
    }
}