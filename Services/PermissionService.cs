using System.Text.Json;
using DynamicDbApi.Data;
using DynamicDbApi.Models;

namespace DynamicDbApi.Services
{
    /// <summary>
    /// 权限服务实现
    /// </summary>
    public class PermissionService : IPermissionService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<PermissionService> _logger;
        private readonly AppDbContext _appDbContext;

        // 角色权限缓存
        private readonly Dictionary<string, List<RolePermission>> _rolePermissionsCache = new();

        public PermissionService(IConfiguration configuration, ILogger<PermissionService> logger, AppDbContext appDbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _appDbContext = appDbContext;
            InitializePermissions();
        }

        /// <summary>
        /// 初始化权限配置
        /// </summary>
        private void InitializePermissions()
        {
            try
            {
                // 从数据库加载角色权限
                var roles = _appDbContext.Roles.ToList();
                
                if (roles.Any())
                {
                    foreach (var role in roles)
                    {
                        var permissions = new List<RolePermission>();
                        
                        // 获取角色的权限
                        var rolePermissions = _appDbContext.Permissions
                            .Where(p => p.RoleId == role.Id)
                            .ToList();
                        
                        foreach (var rolePermission in rolePermissions)
                        {
                            // 获取权限对应的表权限
                            var tablePermissions = _appDbContext.TablePermissions
                                .Where(tp => tp.PermissionId == rolePermission.Id)
                                .ToList();
                            
                            var permission = new RolePermission
                            {
                                DatabaseId = rolePermission.DatabaseId,
                                Tables = tablePermissions.Select(tp => new TablePermission
                                {
                                    Name = tp.Name,
                                    AllowedOperations = tp.AllowedOperations.Split(',').ToList()
                                }).ToList()
                            };
                            
                            permissions.Add(permission);
                        }
                        
                        _rolePermissionsCache[role.Name] = permissions;
                    }
                }
                else
                {
                    // 如果数据库中没有角色，使用配置文件或默认值作为后备
                    LoadPermissionsFromConfig();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库加载权限配置时发生错误");
                
                // 从配置文件或默认值加载作为后备
                LoadPermissionsFromConfig();
            }
        }
        
        /// <summary>
        /// 从配置文件加载权限（作为数据库加载失败的后备）
        /// </summary>
        private void LoadPermissionsFromConfig()
        {
            try
            {
                var rolePermissionsConfig = _configuration.GetSection("RolePermissions").Get<List<RolePermissionConfig>>();
                if (rolePermissionsConfig != null)
                {
                    foreach (var roleConfig in rolePermissionsConfig)
                    {
                        var permissions = new List<RolePermission>();
                        foreach (var permConfig in roleConfig.Permissions)
                        {
                            var rolePermission = new RolePermission
                            {
                                DatabaseId = permConfig.DatabaseId,
                                Tables = permConfig.Tables.Select(t => new TablePermission
                                {
                                    Name = t.Name,
                                    AllowedOperations = t.AllowedOperations
                                }).ToList()
                            };
                            permissions.Add(rolePermission);
                        }
                        _rolePermissionsCache[roleConfig.Role] = permissions;
                    }
                }
                else
                {
                    // 如果配置文件中没有配置，创建默认权限配置
                    var adminPermissions = new List<RolePermission>
                    {
                        new RolePermission
                        {
                            DatabaseId = "*",
                            Tables = new List<TablePermission>
                            {
                                new TablePermission
                                {
                                    Name = "*",
                                    AllowedOperations = new List<string> { "*", "select", "insert", "update", "delete" }
                                }
                            }
                        }
                    };
                    
                    var userPermissions = new List<RolePermission>
                    {
                        new RolePermission
                        {
                            DatabaseId = "default",
                            Tables = new List<TablePermission>
                            {
                                new TablePermission
                                {
                                    Name = "*",
                                    AllowedOperations = new List<string> { "select" }
                                }
                            }
                        }
                    };
                    
                    _rolePermissionsCache["Admin"] = adminPermissions;
                    _rolePermissionsCache["User"] = userPermissions;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从配置文件加载权限配置时发生错误");
                
                // 创建默认权限配置作为最后后备
                var defaultPermissions = new List<RolePermission>
                {
                    new RolePermission
                    {
                        DatabaseId = "*",
                        Tables = new List<TablePermission>
                        {
                            new TablePermission
                            {
                                Name = "*",
                                AllowedOperations = new List<string> { "*", "select", "insert", "update", "delete" }
                            }
                        }
                    }
                };
                
                _rolePermissionsCache["Admin"] = defaultPermissions;
                _rolePermissionsCache["User"] = defaultPermissions;
            }
        }

        /// <summary>
        /// 检查用户是否有权限执行指定操作
        /// </summary>
        public async Task<bool> CheckPermissionAsync(string userId, string databaseId, string tableName, string operation)
        {
            try
            {
                // 获取用户角色
                var roles = await GetUserRolesAsync(userId);
                if (roles == null || roles.Count == 0)
                {
                    return false;
                }

                // 检查每个角色的权限
                foreach (var role in roles)
                {
                    var permissions = await GetRolePermissionsAsync(role);
                    if (permissions == null)
                    {
                        continue;
                    }

                    // 检查数据库权限
                    foreach (var permission in permissions)
                    {
                        // 通配符匹配所有数据库
                        if (permission.DatabaseId == "*" || permission.DatabaseId == databaseId)
                        {
                            // 检查表权限
                            foreach (var tablePermission in permission.Tables)
                            {
                                // 通配符匹配所有表
                                if (tablePermission.Name == "*" || tablePermission.Name.Equals(tableName, StringComparison.OrdinalIgnoreCase))
                                {
                                    // 检查操作权限
                                    if (tablePermission.AllowedOperations.Contains("*") ||
                                        tablePermission.AllowedOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
                                    {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查权限时发生错误");
                return false;
            }
        }

        /// <summary>
        /// 获取用户角色
        /// </summary>
        public async Task<List<string>> GetUserRolesAsync(string userId)
        {
            try
            {
                // 从数据库获取用户角色 - 使用安全的查询方式
                var user = await _appDbContext.Users
                    .Where(u => u.Username == userId)
                    .FirstAsync();
                
                // 获取用户关联的所有角色
                var userRoles = await _appDbContext.UserRoles
                    .Where(ur => ur.UserId == user.Id)
                    .ToListAsync();
                
                if (userRoles.Any())
                {
                    // 获取角色名称列表
                    var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
                    var roles = await _appDbContext.Roles
                        .Where(r => roleIds.Contains(r.Id))
                        .Where(r => r.IsActive)
                        .ToListAsync();
                    
                    return roles.Select(r => r.Name).ToList();
                }
                
                // 如果用户没有角色，使用默认角色
                var defaultRoles = new List<string> { "User" };
                
                // 假设 "admin" 是管理员用户
                if (userId == "admin")
                {
                    defaultRoles.Add("Admin");
                }

                return defaultRoles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "从数据库获取用户角色时发生错误");
                
                // 出现错误时使用默认逻辑
                var defaultRoles = new List<string> { "User" };
                
                // 假设 "admin" 是管理员用户
                if (userId == "admin")
                {
                    defaultRoles.Add("Admin");
                }

                return defaultRoles;
            }
        }

        /// <summary>
        /// 获取角色权限
        /// </summary>
        public Task<List<RolePermission>> GetRolePermissionsAsync(string role)
        {
            if (_rolePermissionsCache.TryGetValue(role, out var permissions))
            {
                return Task.FromResult(permissions);
            }

            return Task.FromResult(new List<RolePermission>());
        }
        
        /// <summary>
        /// 刷新权限缓存
        /// </summary>
        public void RefreshPermissions()
        {
            _rolePermissionsCache.Clear();
            InitializePermissions();
        }
    }

    /// <summary>
    /// 角色权限配置（从配置文件读取时使用）
    /// </summary>
    public class RolePermissionConfig
    {
        public string Role { get; set; } = string.Empty;
        public List<PermissionConfig> Permissions { get; set; } = new List<PermissionConfig>();
    }

    /// <summary>
    /// 权限配置（从配置文件读取时使用）
    /// </summary>
    public class PermissionConfig
    {
        public string DatabaseId { get; set; } = string.Empty;
        public List<TablePermissionConfig> Tables { get; set; } = new List<TablePermissionConfig>();
    }

    /// <summary>
    /// 表权限配置（从配置文件读取时使用）
    /// </summary>
    public class TablePermissionConfig
    {
        public string Name { get; set; } = string.Empty;
        public List<string> AllowedOperations { get; set; } = new List<string>();
    }
}