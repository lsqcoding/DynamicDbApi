namespace DynamicDbApi.Models
{
    /// <summary>
    /// 数据库连接配置
    /// </summary>
    public class DatabaseConnectionConfig
    {
        /// <summary>
        /// 连接ID，用于在JSON请求中引用
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 连接名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 数据库类型，使用DbTypeEnum枚举值
        /// 1: SQLite, 2: SQLServer, 3: MySQL, 4: PostgreSQL, 5: Oracle
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// 主库连接字符串（写操作）
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// 读库连接字符串（读操作）
        /// 如果为空，则使用主库连接
        /// </summary>
        public string? ReadConnectionString { get; set; }

        /// <summary>
        /// 是否启用读写分离
        /// </summary>
        public bool EnableReadWriteSeparation { get; set; } = false;

        /// <summary>
        /// 是否为默认连接
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// 是否启用该连接
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}