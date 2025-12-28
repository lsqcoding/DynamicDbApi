using DynamicDbApi.Infrastructure;
using DynamicDbApi.Models;
using SqlSugar;
using System.Dynamic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 动态查询服务实现
    /// </summary>
    public class DynamicQueryService : IDynamicQueryService
    {
        private readonly IDatabaseConnectionManager _dbConnectionManager;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<DynamicQueryService> _logger;
        private readonly ITableAliasService _aliasService;
        private readonly IQueryAnalysisService _queryAnalysisService;
        private readonly ICacheService _cacheService;

        public DynamicQueryService(
            IDatabaseConnectionManager dbConnectionManager,
            IPermissionService permissionService,
            ILogger<DynamicQueryService> logger,
            ITableAliasService aliasService,
            IQueryAnalysisService queryAnalysisService,
            ICacheService cacheService)
        {
            _dbConnectionManager = dbConnectionManager;
            _permissionService = permissionService;
            _logger = logger;
            _aliasService = aliasService;
            _queryAnalysisService = queryAnalysisService;
            _cacheService = cacheService;
        }

        /// <summary>
        /// 执行动态查询
        /// </summary>
        public async Task<DynamicQueryResponse> ExecuteQueryAsync(DynamicQueryRequest request, string userId)
        {
            try
            {
                // 验证请求
                if (string.IsNullOrEmpty(request.TableName) && request.CteQuery == null)
                {
                    return DynamicQueryResponse.Fail("表名不能为空或CTE定义不能为空");
                }

                // 获取数据库连接
                string databaseId = request.DatabaseId ?? "default"; // 使用default作为默认数据库ID，而不是空字符串
                SqlSugarClient db;
                try
                {
                    db = string.IsNullOrEmpty(databaseId)
                        ? _dbConnectionManager.GetDefaultDbClient()
                        : _dbConnectionManager.GetDbClient(databaseId);
                }
                catch (Exception ex)
                {
                    return DynamicQueryResponse.Fail($"获取数据库连接失败: {ex.Message}");
                }

                // 检查权限
                var operation = request.Operation.ToLower();
                var hasPermission = await _permissionService.CheckPermissionAsync(
                    userId, databaseId, request.TableName, operation);

                if (!hasPermission)
                {
                    return DynamicQueryResponse.Fail($"没有权限执行 {operation} 操作");
                }

                // 根据操作类型执行不同的查询
                return operation switch
                {
                    "select" => await ExecuteSelectAsync(db, request),
                    "insert" => await ExecuteInsertAsync(db, request),
                    "update" => await ExecuteUpdateAsync(db, request),
                    "delete" => await ExecuteDeleteAsync(db, request),
                    "union" => await ExecuteUnionQueryAsync(db, request),
                    "cte" => await ExecuteCteQueryAsync(db, request),
                    _ => DynamicQueryResponse.Fail($"不支持的操作类型: {operation}")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行动态查询时发生错误");
                return DynamicQueryResponse.Fail($"执行查询失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建表
        /// </summary>
        public async Task<DynamicQueryResponse> CreateTableAsync(CreateTableRequest request, string userId)
        {
            try
            {
                // 验证请求
                if (string.IsNullOrEmpty(request.TableName))
                {
                    return DynamicQueryResponse.Fail("表名不能为空");
                }

                // 验证表名格式
                if (!IsValidTableName(request.TableName))
                {
                    return DynamicQueryResponse.Fail("无效的表名格式");
                }

                if (request.Columns == null || request.Columns.Count == 0)
                {
                    return DynamicQueryResponse.Fail("表至少需要一个列");
                }

                // 验证是否有主键
                if (!request.Columns.Any(c => c.IsPrimaryKey))
                {
                    return DynamicQueryResponse.Fail("表必须定义一个主键");
                }

                // 获取数据库连接
                string databaseId = request.DatabaseId ?? "";
                SqlSugarClient db;
                try
                {
                    db = string.IsNullOrEmpty(databaseId)
                        ? _dbConnectionManager.GetDefaultDbClient()
                        : _dbConnectionManager.GetDbClient(databaseId);
                }
                catch (Exception ex)
                {
                    return DynamicQueryResponse.Fail($"获取数据库连接失败: {ex.Message}");
                }

                // 检查权限
                var hasPermission = await _permissionService.CheckPermissionAsync(
                    userId, databaseId, request.TableName, "create");

                if (!hasPermission)
                {
                    return DynamicQueryResponse.Fail("没有权限创建表");
                }

                // 检查表是否已存在
                if (await Task.FromResult(db.DbMaintenance.IsAnyTable(request.TableName)))
                {
                    return DynamicQueryResponse.Fail($"表 {request.TableName} 已存在");
                }

                // 创建表结构
                var createTableResult = await Task.Run(() =>
                {
                    db.DbMaintenance.CreateTable(request.TableName, CreateColumnInfos(request.Columns));
                    return true;
                });

                _logger.LogInformation($"用户 {userId} 创建表成功: 连接ID={databaseId}, 表名={request.TableName}");
                return DynamicQueryResponse.Ok(new { success = true, table = request.TableName, message = "表创建成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"创建表时发生错误: 表名={request.TableName}");
                return DynamicQueryResponse.Fail($"创建表失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取表结构信息
        /// </summary>
        public async Task<DynamicQueryResponse> GetTableSchemaAsync(string? databaseId, string? tableName)
        {
            try
            {
                // 获取数据库连接
                SqlSugarClient db;
                try
                {
                    db = string.IsNullOrEmpty(databaseId)
                        ? _dbConnectionManager.GetDefaultDbClient()
                        : _dbConnectionManager.GetDbClient(databaseId);
                }
                catch (Exception ex)
                {
                    return DynamicQueryResponse.Fail($"获取数据库连接失败: {ex.Message}");
                }

                if (string.IsNullOrEmpty(tableName))
                {
                    // 获取所有表信息
                    var tables = await Task.FromResult(db.DbMaintenance.GetTableInfoList());
                    // 获取所有视图信息
                    var views = await Task.FromResult(db.DbMaintenance.GetViewInfoList());
                    // 合并表和视图
                    var allTablesAndViews = tables.Concat(views).ToList();
                    return DynamicQueryResponse.Ok(allTablesAndViews);
                }
                else
                {
                    // 获取指定表或视图的列信息
                    var columns = await Task.FromResult(db.DbMaintenance.GetColumnInfosByTableName(tableName));
                    return DynamicQueryResponse.Ok(columns);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取表结构信息时发生错误");
                return DynamicQueryResponse.Fail($"获取表结构信息失败: {ex.Message}");
            }
        }

        #region 私有方法

        /// <summary>
        /// 将列定义转换为SqlSugar的列信息
        /// </summary>
        private List<DbColumnInfo> CreateColumnInfos(List<ColumnDefinition> columns)
        {
            var columnInfos = new List<DbColumnInfo>();
            
            foreach (var col in columns)
            {
                var columnInfo = new DbColumnInfo
                {
                    DbColumnName = col.Name,
                    ColumnDescription = col.Description,
                    IsPrimarykey = col.IsPrimaryKey,
                    IsIdentity = col.IsAutoIncrement,
                    IsNullable = col.IsNullable,
                    DataType = col.Type
                };
                
                if (col.DefaultValue != null)
                {
                    columnInfo.DefaultValue = col.DefaultValue.ToString();
                }
                
                columnInfos.Add(columnInfo);
            }
            
            return columnInfos;
        }

        /// <summary>
        /// 验证表名格式是否有效
        /// </summary>
        private bool IsValidTableName(string tableName)
        {
            // 简单的表名验证：只允许字母、数字、下划线，且不能以数字开头
            if (string.IsNullOrEmpty(tableName))
                return false;
            
            // 正则表达式验证
            return Regex.IsMatch(tableName, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        /// <summary>
        /// 生成查询缓存键
        /// </summary>
        private string GenerateCacheKey(DynamicQueryRequest request)
        {
            // 基于查询的所有参数生成唯一键
            var keyBuilder = new StringBuilder();
            keyBuilder.Append($"select_{request.TableName}_");
            
            // 添加数据库ID
            if (!string.IsNullOrEmpty(request.DatabaseId))
            {
                keyBuilder.Append($"{request.DatabaseId}_");
            }
            
            // 添加查询条件
            if (request.WhereConditions != null && request.WhereConditions.Count > 0)
            {
                foreach (var condition in request.WhereConditions.OrderBy(c => c.Key))
                {
                    keyBuilder.Append($"{condition.Key}={condition.Value}_");
                }
            }
            
            // 添加排序
            if (request.OrderBy != null && request.OrderBy.Count > 0)
            {
                foreach (var order in request.OrderBy.OrderBy(o => o.Key))
                {
                    keyBuilder.Append($"{order.Key}_{order.Value}_");
                }
            }
            
            // 添加分页
            if (request.Page != null)
            {
                keyBuilder.Append($"page_{request.Page.PageIndex}_{request.Page.PageSize}_");
            }
            
            // 添加查询字段
            if (request.Columns != null && request.Columns.Count > 0)
            {
                foreach (var column in request.Columns.OrderBy(c => c))
                {
                    keyBuilder.Append($"{column}_");
                }
            }
            
            // 添加关联查询
            if (request.Joins != null && request.Joins.Count > 0)
            {
                foreach (var join in request.Joins.OrderBy(j => j.TableName))
                {
                    keyBuilder.Append($"{join.TableName}_{join.JoinType}_");
                    foreach (var condition in join.OnConditions.OrderBy(c => c.Key))
                    {
                        keyBuilder.Append($"{condition.Key}={condition.Value}_");
                    }
                    if (join.Columns != null)
                    {
                        foreach (var column in join.Columns.OrderBy(c => c))
                        {
                            keyBuilder.Append($"{column}_");
                        }
                    }
                }
            }
            
            // 使用MD5哈希确保键的长度合理
            using (var md5 = MD5.Create())
            {
                var inputBytes = Encoding.UTF8.GetBytes(keyBuilder.ToString());
                var hashBytes = md5.ComputeHash(inputBytes);
                
                // 转换为十六进制字符串
                var sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                
                return $"query_{sb.ToString()}";
            }
        }
        
        /// <summary>
        /// 执行查询操作
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteSelectAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 生成缓存键
                var cacheKey = GenerateCacheKey(request);
                
                // 尝试从缓存获取结果
                var cachedResponse = await _cacheService.GetAsync<DynamicQueryResponse>(cacheKey);
                if (cachedResponse != null)
                {
                    _logger.LogDebug("查询结果从缓存获取: {CacheKey}", cacheKey);
                    return cachedResponse;
                }
                
                // 记录查询开始时间
                var stopwatch = Stopwatch.StartNew();
                
                // 创建查询对象
                var realTableName = _aliasService.GetRealTableName(request.DatabaseId ?? "default", request.TableName);
                // 使用用户提供的主表别名，如果没有则使用真实表名
                var mainTableAlias = !string.IsNullOrEmpty(request.Alias) ? request.Alias : realTableName;
                var query = db.Queryable<ExpandoObject>().AS(realTableName, mainTableAlias);

                // 添加关联查询
                if (request.Joins != null && request.Joins.Count > 0)
                {
                    foreach (var join in request.Joins)
                    {
                        string joinType = join.JoinType.ToLower();
                        // 获取关联表的真实表名
                        string joinTable = _aliasService.GetRealTableName(request.DatabaseId ?? "default", join.TableName);
                        // 使用用户提供的别名，如果没有则使用默认别名
                        string joinAlias = !string.IsNullOrEmpty(join.Alias) ? join.Alias : $"j_{joinTable}";

                        // 构建关联条件
                        string joinCondition = string.Join(" AND ", join.OnConditions.Select(c => $"{c.Key} = {c.Value}"));

                        switch (joinType)
                        {
                            case "inner":
                                query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Inner);
                                break;
                            case "left":
                                query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Left);
                                break;
                            case "right":
                                query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Right);
                                break;
                            default:
                                throw new ArgumentException($"不支持的关联类型: {joinType}");
                        }

                        // 添加关联表的查询字段
                        if (join.Columns != null && join.Columns.Count > 0)
                        {
                            foreach (var column in join.Columns)
                            {
                                query = query.Select($"{joinAlias}.{column} as {joinTable}_{column}");
                            }
                        }
                    }
                }

                // 添加查询字段
                if (request.Columns != null && request.Columns.Count > 0)
                {
                    query = query.Select(string.Join(",", request.Columns));
                }

                // 添加DISTINCT去重
                if (request.Distinct)
                {
                    query = query.Distinct();
                }

                // 添加查询条件
                if (request.WhereConditions != null && request.WhereConditions.Count > 0)
                {
                    var whereExp = BuildWhereExpression(request.WhereConditions);
                    query = query.Where(whereExp);
                }

                // 添加分组条件
                if (request.GroupBy != null && request.GroupBy.Count > 0)
                {
                    query = query.GroupBy(string.Join(",", request.GroupBy));
                }

                // 添加HAVING过滤条件
                if (!string.IsNullOrEmpty(request.HavingCondition))
                {
                    query = query.Having(request.HavingCondition);
                }

                // 添加排序条件
                if (request.OrderBy != null && request.OrderBy.Count > 0)
                {
                    foreach (var order in request.OrderBy)
                    {
                        string direction = order.Value.ToLower();
                        if (direction == "asc")
                        {
                            query = query.OrderBy($"{order.Key} asc");
                        }
                        else if (direction == "desc")
                        {
                            query = query.OrderBy($"{order.Key} desc");
                        }
                    }
                }

                // 执行分页查询
                if (request.Page != null)
                {
                    int pageIndex = Math.Max(1, request.Page.PageIndex);
                    int pageSize = Math.Max(1, request.Page.PageSize);

                    var result = await query.ToPageListAsync(pageIndex, pageSize);
                    var total = await query.CountAsync();
                    
                    // 停止计时并记录查询模式
                    stopwatch.Stop();
                    await _queryAnalysisService.RecordQueryPatternAsync(request, stopwatch.ElapsedMilliseconds);
                    
                    // 创建响应对象，包含分页信息
                    var response = DynamicQueryResponse.Ok(result, "查询成功", total, pageIndex, pageSize);
                    
                    // 缓存结果（默认缓存5分钟）
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

                    return response;
                }
                else
                {
                    // 执行普通查询
                    var result = await query.ToListAsync();
                    
                    // 停止计时并记录查询模式
                    stopwatch.Stop();
                    await _queryAnalysisService.RecordQueryPatternAsync(request, stopwatch.ElapsedMilliseconds);
                    
                    // 创建响应对象
                    var response = DynamicQueryResponse.Ok(result, "查询成功", result.Count);
                    
                    // 缓存结果（默认缓存5分钟）
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));

                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行查询操作时发生错误");
                return DynamicQueryResponse.Fail($"查询失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除表的所有缓存
        /// </summary>
        private async Task ClearTableCache(string tableName)
        {
            await _cacheService.RemoveByPrefixAsync($"select_{tableName}_");
        }
        
        /// <summary>
        /// 执行插入操作
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteInsertAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 获取真实表名
                var realTableName = _aliasService.GetRealTableName(request.TableName, request.DatabaseId ?? "default");
                
                // 批量插入
                if (request.DataList != null && request.DataList.Count > 0)
                {
                    // 执行批量插入操作，使用SqlSugar的批量插入功能提高性能
                    var result = await db.Insertable(request.DataList)
                        .AS(realTableName)
                        .ExecuteCommandAsync();

                    return await HandleCustomReturnQuery(db, request, result, $"批量插入成功，共插入 {result} 条记录");
                }
                // 单个插入
                else if (request.Data != null && request.Data.Count > 0)
                {
                    // 将字典转换为动态对象
                    dynamic insertData = ConvertToDynamicObject(request.Data);

                    // 执行插入操作
                    var result = await db.Insertable(insertData)
                        .AS(realTableName)
                        .ExecuteReturnIdentityAsync();

                    return await HandleCustomReturnQuery(db, request, result, "插入成功");
                }
                else
                {
                    return DynamicQueryResponse.Fail("插入数据不能为空");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行插入操作时发生错误");
                return DynamicQueryResponse.Fail($"插入失败: {ex.Message}");
            }
            finally
            {
                // 清除相关表的缓存
                await ClearTableCache(request.TableName);
            }
        }

        /// <summary>
        /// 执行更新操作
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteUpdateAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 获取真实表名
                var realTableName = _aliasService.GetRealTableName(request.TableName, request.DatabaseId ?? "default");
                
                // 批量更新（每条记录有独立的更新数据和条件）
                if (request.DataList != null && request.DataList.Count > 0)
                {
                    // 检查每条记录是否包含ID字段（用于定位要更新的记录）
                    if (!request.DataList.All(item => item.ContainsKey("id") || item.ContainsKey("Id") || item.ContainsKey("ID")))
                    {
                        return DynamicQueryResponse.Fail("批量更新时每条记录必须包含ID字段");
                    }

                    // 使用SqlSugar的批量更新功能
                    var result = await db.Updateable(request.DataList)
                        .AS(realTableName)
                        .ExecuteCommandAsync();

                    return await HandleCustomReturnQuery(db, request, result, $"批量更新成功，共更新 {result} 条记录");
                }
                // 单条更新
                else if (request.Data != null && request.Data.Count > 0)
                {
                    if (request.WhereConditions == null || request.WhereConditions.Count == 0)
                    {
                        return DynamicQueryResponse.Fail("更新条件不能为空");
                    }

                    // 将字典转换为动态对象
                    dynamic updateData = ConvertToDynamicObject(request.Data);

                    // 构建更新条件
                    var whereExp = BuildWhereExpression(request.WhereConditions);

                    // 检查是否使用乐观锁
                    bool useOptimisticLock = request.Data.ContainsKey("Version") || request.Data.ContainsKey("RowVersion");
                    
                    // 执行更新操作
                    var result = await db.Updateable(updateData)
                        .AS(realTableName)
                        .Where(whereExp)
                        .ExecuteCommandAsync();

                    // 如果使用乐观锁且更新失败，说明发生了并发冲突
                    if (useOptimisticLock && result == 0)
                    {
                        return DynamicQueryResponse.Fail("更新失败：数据已被其他用户修改，请刷新后重试");
                    }

                    return await HandleCustomReturnQuery(db, request, result, "更新成功");
                }
                else
                {
                    return DynamicQueryResponse.Fail("更新数据不能为空");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行更新操作时发生错误");
                return DynamicQueryResponse.Fail($"更新失败: {ex.Message}");
            }
            finally
            {
                // 清除相关表的缓存
                await ClearTableCache(request.TableName);
            }
        }

        /// <summary>
        /// 执行删除操作
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteDeleteAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 获取真实表名
                var realTableName = _aliasService.GetRealTableName(request.TableName, request.DatabaseId ?? "default");
                
                int result;
                
                // 批量删除（通过ID列表）
                if (request.DataList != null && request.DataList.Count > 0)
                {
                    // 提取所有ID值
                    var ids = new List<object>();
                    foreach (var item in request.DataList)
                    {
                        if (item.TryGetValue("id", out var id) || 
                            item.TryGetValue("Id", out id) || 
                            item.TryGetValue("ID", out id))
                        {
                            ids.Add(id);
                        }
                    }

                    if (ids.Count == 0)
                    {
                        return DynamicQueryResponse.Fail("批量删除时未找到有效的ID字段");
                    }

                    // 执行批量删除操作
                    result = await db.Deleteable<ExpandoObject>()
                        .AS(realTableName)
                        .In(ids.ToArray())
                        .ExecuteCommandAsync();
                }
                // 条件删除
                else if (request.WhereConditions != null && request.WhereConditions.Count > 0)
                {
                    // 构建删除条件
                    var whereExp = BuildWhereExpression(request.WhereConditions);

                    // 执行删除操作
                    result = await db.Deleteable<ExpandoObject>()
                        .AS(realTableName)
                        .Where(whereExp)
                        .ExecuteCommandAsync();
                }
                else
                {
                    return DynamicQueryResponse.Fail("删除条件不能为空");
                }

                return await HandleCustomReturnQuery(db, request, result, $"删除成功，共删除 {result} 条记录");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行删除操作时发生错误");
                return DynamicQueryResponse.Fail($"删除失败: {ex.Message}");
            }
            finally
            {
                // 清除相关表的缓存
                await ClearTableCache(request.TableName);
            }
        }

        /// <summary>
        /// 构建查询条件表达式
        /// </summary>
        private string BuildWhereExpression(Dictionary<string, object> conditions)
        {
            List<string> whereList = new List<string>();

            foreach (var condition in conditions)
            {
                string key = condition.Key;
                object value = condition.Value;

                // 处理特殊操作符
                if (key.Contains("__"))
                {
                    var parts = key.Split("__");
                    string field = parts[0];
                    string op = parts[1].ToLower();

                    switch (op)
                    {
                        case "eq":
                            whereList.Add($"{field} = {FormatValue(value)}");
                            break;
                        case "neq":
                            whereList.Add($"{field} <> {FormatValue(value)}");
                            break;
                        case "gt":
                            whereList.Add($"{field} > {FormatValue(value)}");
                            break;
                        case "gte":
                            whereList.Add($"{field} >= {FormatValue(value)}");
                            break;
                        case "lt":
                            whereList.Add($"{field} < {FormatValue(value)}");
                            break;
                        case "lte":
                            whereList.Add($"{field} <= {FormatValue(value)}");
                            break;
                        case "like":
                            whereList.Add($"{field} LIKE {FormatValue($"%{value}%")}");
                            break;
                        case "in":
                            if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
                            {
                                var values = jsonElement.EnumerateArray()
                                    .Select(e => FormatValue(e))
                                    .ToList();
                                whereList.Add($"{field} IN ({string.Join(",", values)})");
                            }
                            break;
                        case "between":
                            if (value is JsonElement betweenElement && betweenElement.ValueKind == JsonValueKind.Array)
                            {
                                var betweenValues = betweenElement.EnumerateArray()
                                    .Select(e => FormatValue(e))
                                    .ToList();
                                if (betweenValues.Count == 2)
                                {
                                    whereList.Add($"{field} BETWEEN {betweenValues[0]} AND {betweenValues[1]}");
                                }
                            }
                            break;
                        default:
                            whereList.Add($"{field} = {FormatValue(value)}");
                            break;
                    }
                }
                else
                {
                    // 默认为等于操作
                    whereList.Add($"{key} = {FormatValue(value)}");
                }
            }

            return string.Join(" AND ", whereList);
        }

        /// <summary>
        /// 格式化SQL值
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null)
            {
                return "NULL";
            }

            if (value is JsonElement jsonElement)
            {
                switch (jsonElement.ValueKind)
                {
                    case JsonValueKind.String:
                        return $"'{jsonElement.GetString()?.Replace("'", "''")}'";
                    case JsonValueKind.Number:
                        return jsonElement.ToString();
                    case JsonValueKind.True:
                        return "1";
                    case JsonValueKind.False:
                        return "0";
                    case JsonValueKind.Null:
                        return "NULL";
                    default:
                        return $"'{jsonElement.ToString()?.Replace("'", "''")}'";
                }
            }

            if (value is string strValue)
            {
                return $"'{strValue.Replace("'", "''")}'";
            }

            if (value is bool boolValue)
            {
                return boolValue ? "1" : "0";
            }

            if (value is DateTime dateValue)
            {
                return $"'{dateValue:yyyy-MM-dd HH:mm:ss}'";
            }

            return value.ToString() ?? "NULL";
        }

        /// <summary>
        /// 将字典转换为动态对象
        /// </summary>
        private dynamic ConvertToDynamicObject(Dictionary<string, object> dict)
        {
            dynamic obj = new ExpandoObject();
            var objDict = (IDictionary<string, object>)obj;

            foreach (var item in dict)
            {
                objDict[item.Key] = item.Value;
            }

            return obj;
        }

        /// <summary>
        /// 处理自定义返回值查询
        /// </summary>
        private async Task<DynamicQueryResponse> HandleCustomReturnQuery(
            SqlSugarClient db,
            DynamicQueryRequest request,
            object operationResult,
            string successMessage)
        {
            // 如果没有自定义返回值查询配置，直接返回默认响应
            if (request.ReturnQuery == null)
            {
                return DynamicQueryResponse.Ok(operationResult, successMessage);
            }

            try
            {
                // 设置自定义查询的操作类型为select
                request.ReturnQuery.Operation = "select";
                
                // 如果自定义查询没有指定表名，使用原请求的表名
                if (string.IsNullOrEmpty(request.ReturnQuery.TableName))
                {
                    request.ReturnQuery.TableName = request.TableName;
                }
                
                // 如果自定义查询没有指定数据库ID，使用原请求的数据库ID
                if (string.IsNullOrEmpty(request.ReturnQuery.DatabaseId))
                {
                    request.ReturnQuery.DatabaseId = request.DatabaseId;
                }

                // 执行自定义查询
                var queryResponse = await ExecuteSelectAsync(db, request.ReturnQuery);

                // 如果查询失败，返回原操作结果和查询失败信息
                if (!queryResponse.Success)
                {
                    return DynamicQueryResponse.Ok(
                        new
                        {
                            operationResult = operationResult,
                            returnQueryResult = (object?)null,
                            queryError = queryResponse.Message
                        },
                        $"{successMessage}，但自定义查询失败: {queryResponse.Message}");
                }

                // 返回包含原操作结果和自定义查询结果的响应
                return DynamicQueryResponse.Ok(
                    new
                    {
                        operationResult = operationResult,
                        returnQueryResult = queryResponse.Data,
                        total = queryResponse.Total
                    },
                    successMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行自定义返回值查询时发生错误");
                
                // 如果自定义查询执行失败，返回原操作结果和错误信息
                return DynamicQueryResponse.Ok(
                    new
                    {
                        operationResult = operationResult,
                        returnQueryResult = (object?)null,
                        queryError = ex.Message
                    },
                    $"{successMessage}，但自定义查询执行失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行UNION查询
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteUnionQueryAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 生成缓存键
                var cacheKey = GenerateCacheKey(request);
                
                // 尝试从缓存获取结果
                var cachedResponse = await _cacheService.GetAsync<DynamicQueryResponse>(cacheKey);
                if (cachedResponse != null)
                {
                    _logger.LogDebug("UNION查询结果从缓存获取: {CacheKey}", cacheKey);
                    return cachedResponse;
                }
                
                // 记录查询开始时间
                var stopwatch = Stopwatch.StartNew();
                
                // 检查UNION配置
                if (request.UnionQuery == null || request.UnionQuery.SubQueries.Count < 2)
                {
                    return DynamicQueryResponse.Fail("UNION查询至少需要2个子查询");
                }
                
                // 构建所有子查询的SQL语句
                var subQuerySqlList = new List<string>();
                var parametersList = new List<List<SugarParameter>>();
                
                foreach (var subQuery in request.UnionQuery.SubQueries)
                {
                    // 创建子查询对象
                    var query = await CreateSubQueryAsync(db, subQuery);
                    
                    // 获取子查询的SQL和参数
                    var sqlInfo = query.ToSql();
                    subQuerySqlList.Add(sqlInfo.Key);
                    parametersList.Add(sqlInfo.Value.ToList());
                }
                
                // 合并所有参数
                var allParameters = parametersList.SelectMany(p => p).ToArray();
                
                // 构建UNION查询
                string unionOperator = request.UnionQuery.UseUnionAll ? " UNION ALL " : " UNION ";
                string unionSql = string.Join(unionOperator, subQuerySqlList);
                
                // 添加排序条件
                if (request.OrderBy != null && request.OrderBy.Count > 0)
                {
                    var orderByClauses = request.OrderBy.Select(order => 
                        $"{order.Key} {(order.Value.ToLower() == "desc" ? "DESC" : "ASC")}").ToList();
                    unionSql += $" ORDER BY {string.Join(", ", orderByClauses)}";
                }
                
                // 执行查询
                List<ExpandoObject> result;
                int total;
                
                if (request.Page != null)
                {
                    int pageIndex = Math.Max(1, request.Page.PageIndex);
                    int pageSize = Math.Max(1, request.Page.PageSize);
                    
                    // 执行分页查询
                    result = await db.Ado.SqlQueryAsync<ExpandoObject>(unionSql, allParameters);
                    total = result.Count;
                    
                    // 手动分页
                    result = result.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                }
                else
                {
                    // 执行普通查询
                    result = await db.Ado.SqlQueryAsync<ExpandoObject>(unionSql, allParameters);
                    total = result.Count;
                }
                
                // 停止计时并记录查询模式
                stopwatch.Stop();
                await _queryAnalysisService.RecordQueryPatternAsync(request, stopwatch.ElapsedMilliseconds);
                
                // 创建响应对象
                var response = request.Page != null 
                    ? DynamicQueryResponse.Ok(result, "UNION查询成功", total, request.Page.PageIndex, request.Page.PageSize)
                    : DynamicQueryResponse.Ok(result, "UNION查询成功", total);
                
                // 缓存结果（默认缓存5分钟）
                await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
                
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行UNION查询时发生错误");
                return DynamicQueryResponse.Fail($"UNION查询失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 创建子查询对象
        /// </summary>
        private async Task<ISugarQueryable<ExpandoObject>> CreateSubQueryAsync(SqlSugarClient db, DynamicQueryRequest subQuery)
        {
            // 设置子查询操作类型为select
            subQuery.Operation = "select";
            
            // 创建查询对象
            var realTableName = _aliasService.GetRealTableName(subQuery.TableName, subQuery.DatabaseId ?? "default");
            // 使用用户提供的主表别名，如果没有则使用真实表名
            var mainTableAlias = !string.IsNullOrEmpty(subQuery.Alias) ? subQuery.Alias : realTableName;
            var query = db.Queryable<ExpandoObject>().AS(realTableName, mainTableAlias);
            
            // 添加关联查询
            if (subQuery.Joins != null && subQuery.Joins.Count > 0)
            {
                foreach (var join in subQuery.Joins)
                {
                    string joinType = join.JoinType.ToLower();
                    // 获取真实表名
                    string joinTable = _aliasService.GetRealTableName(join.TableName, subQuery.DatabaseId ?? "default");
                    string joinAlias = !string.IsNullOrEmpty(join.Alias) ? join.Alias : $"j_{joinTable}";
                    
                    // 构建关联条件
                    string joinCondition = string.Join(" AND ", join.OnConditions.Select(c => $"{c.Key} = {c.Value}"));

                    switch (joinType)
                    {
                        case "inner":
                            query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Inner);
                            break;
                        case "left":
                            query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Left);
                            break;
                        case "right":
                            query = query.AddJoinInfo(joinTable, joinAlias, joinCondition, JoinType.Right);
                            break;
                        default:
                            throw new ArgumentException($"不支持的关联类型: {joinType}");
                    }

                    // 添加关联表的查询字段
                    if (join.Columns != null && join.Columns.Count > 0)
                    {
                        foreach (var column in join.Columns)
                        {
                            query = query.Select($"{joinAlias}.{column} as {joinTable}_{column}");
                        }
                    }
                }
            }
            
            // 添加查询字段
            if (subQuery.Columns != null && subQuery.Columns.Count > 0)
            {
                query = query.Select(string.Join(",", subQuery.Columns));
            }
            
            // 添加查询条件
            if (subQuery.WhereConditions != null && subQuery.WhereConditions.Count > 0)
            {
                var whereExp = BuildWhereExpression(subQuery.WhereConditions);
                query = query.Where(whereExp);
            }
            
            // 添加DISTINCT去重
            if (subQuery.Distinct)
            {
                query = query.Distinct();
            }
            
            // 添加分组条件
            if (subQuery.GroupBy != null && subQuery.GroupBy.Count > 0)
            {
                query = query.GroupBy(string.Join(",", subQuery.GroupBy));
            }
            
            // 添加HAVING过滤条件
            if (!string.IsNullOrEmpty(subQuery.HavingCondition))
            {
                query = query.Having(subQuery.HavingCondition);
            }
            
            return query;
        }

        #region
        /// <summary>
        /// 执行CTE查询操作
        /// </summary>
        private async Task<DynamicQueryResponse> ExecuteCteQueryAsync(SqlSugarClient db, DynamicQueryRequest request)
        {
            try
            {
                // 生成缓存键
                var cacheKey = GenerateCacheKey(request);
                
                // 尝试从缓存获取结果
                var cachedResponse = await _cacheService.GetAsync<DynamicQueryResponse>(cacheKey);
                if (cachedResponse != null)
                {
                    _logger.LogDebug("查询结果从缓存获取: {CacheKey}", cacheKey);
                    return cachedResponse;
                }
                
                // 记录查询开始时间
                var stopwatch = Stopwatch.StartNew();
                
                // 构建CTE部分
                StringBuilder cteBuilder = new StringBuilder("WITH ");
                List<object> parameters = new List<object>();
                
                for (int i = 0; i < request.CteQuery.Definitions.Count; i++)
                {
                    var cte = request.CteQuery.Definitions[i];
                    string cteQuery = cte.Query;
                    
                    // 如果有参数，替换为SqlSugar参数占位符
                    if (request.Parameters != null && request.Parameters.Count > 0)
                    {
                        foreach (var param in request.Parameters)
                        {
                            cteQuery = cteQuery.Replace($"@{param.Key}", $"@p{i}_{param.Key}");
                            parameters.Add(param.Value);
                        }
                    }
                    
                    if (i > 0)
                    {
                        cteBuilder.Append(", ");
                    }
                    
                    cteBuilder.Append($"{cte.Name} AS ({cteQuery})");
                    
                    if (cte.IsRecursive)
                    {
                        cteBuilder.Append(" RECURSIVE");
                    }
                }
                
                // 构建主查询部分
                string mainQuery;
                if (!string.IsNullOrEmpty(request.TableName))
                {
                    // 如果指定了表名，使用表名构建主查询
                    var realTableName = _aliasService.GetRealTableName(request.DatabaseId ?? "default", request.TableName);
                    var mainTableAlias = !string.IsNullOrEmpty(request.Alias) ? request.Alias : realTableName;
                    
                    mainQuery = db.Queryable<ExpandoObject>().AS(realTableName, mainTableAlias).ToSqlString();
                    
                    // 添加查询字段
                    if (request.Columns != null && request.Columns.Count > 0)
                    {
                        mainQuery = mainQuery.Replace("SELECT *", $"SELECT {string.Join(",", request.Columns)}");
                    }
                    
                    // 添加DISTINCT去重
                    if (request.Distinct)
                    {
                        mainQuery = mainQuery.Replace("SELECT ", "SELECT DISTINCT ");
                    }
                    
                    // 添加查询条件
                    if (request.WhereConditions != null && request.WhereConditions.Count > 0)
                    {
                        var whereExp = BuildWhereExpression(request.WhereConditions);
                        // 这里简单处理，实际可能需要更复杂的SQL构建
                        string whereSql = whereExp.ToSqlString();
                        mainQuery = mainQuery.Replace("WHERE", whereSql) + " WHERE";
                    }
                    
                    // 添加排序条件
                    if (request.OrderBy != null && request.OrderBy.Count > 0)
                    {
                        string orderBySql = " ORDER BY " + string.Join(",", request.OrderBy.Select(o => $"{o.Key} {o.Value.ToUpper()}"));
                        mainQuery += orderBySql;
                    }
                }
                else
                {
                    // 如果没有指定表名，使用CTE名称作为主查询的表
                    mainQuery = db.Queryable<ExpandoObject>().AS(request.CteQuery.Definitions[0].Name).ToSqlString();
                }
                
                // 组合CTE和主查询
                string fullQuery = $"{cteBuilder} {mainQuery}";
                
                // 执行查询
                List<ExpandoObject> result;
                if (request.Page != null)
                {
                    // 分页查询
                    int pageIndex = Math.Max(1, request.Page.PageIndex);
                    int pageSize = Math.Max(1, request.Page.PageSize);
                    
                    result = await db.Ado.SqlQueryAsync<ExpandoObject>(fullQuery, parameters);
                    int total = result.Count;
                    
                    // 手动分页
                    result = result.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
                    
                    // 停止计时并记录查询模式
                    stopwatch.Stop();
                    await _queryAnalysisService.RecordQueryPatternAsync(request, stopwatch.ElapsedMilliseconds);
                    
                    // 创建响应对象，包含分页信息
                    var response = DynamicQueryResponse.Ok(result, "查询成功", total, pageIndex, pageSize);
                    
                    // 缓存结果（默认缓存5分钟）
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
                    
                    return response;
                }
                else
                {
                    // 普通查询
                    result = await db.Ado.SqlQueryAsync<ExpandoObject>(fullQuery, parameters);
                    
                    // 停止计时并记录查询模式
                    stopwatch.Stop();
                    await _queryAnalysisService.RecordQueryPatternAsync(request, stopwatch.ElapsedMilliseconds);
                    
                    // 创建响应对象
                    var response = DynamicQueryResponse.Ok(result, "查询成功", result.Count);
                    
                    // 缓存结果（默认缓存5分钟）
                    await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromMinutes(5));
                    
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "执行CTE查询操作时发生错误");
                return DynamicQueryResponse.Fail($"CTE查询失败: {ex.Message}");
            }
        }
        #endregion
        
        #endregion
    }
}