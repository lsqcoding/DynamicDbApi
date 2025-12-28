namespace DynamicDbApi.Models
{
    /// <summary>
    /// 数据库类型枚举
    /// 用数字表示不同的数据库类型，便于配置和使用
    /// </summary>
    public enum DbTypeEnum
    {
        /// <summary>
        /// SQLite数据库
        /// </summary>
        SQLite = 1,
        
        /// <summary>
        /// SQL Server数据库
        /// </summary>
        SQLServer = 2,
        
        /// <summary>
        /// MySQL数据库
        /// </summary>
        MySQL = 3,
        
        /// <summary>
        /// PostgreSQL数据库
        /// </summary>
        PostgreSQL = 4,
        
        /// <summary>
        /// Oracle数据库
        /// </summary>
        Oracle = 5
    }
}
