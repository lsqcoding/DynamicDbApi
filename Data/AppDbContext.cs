using SqlSugar;
using DynamicDbApi.Models;

namespace DynamicDbApi.Data;

/// <summary>
/// 数据库上下文类，用于SqlSugar ORM操作
/// </summary>
public class AppDbContext
{
    public ISqlSugarClient Db { get; private set; }

    public AppDbContext(ISqlSugarClient sqlSugarClient)
    {
        // 直接使用传入的SqlSugarClient实例
        Db = sqlSugarClient;
    }

    // 提供对各个表的访问
    public ISugarQueryable<StoredFile> StoredFiles => Db.Queryable<StoredFile>();
    public ISugarQueryable<FileShareLink> FileShareLinks => Db.Queryable<FileShareLink>();
    public ISugarQueryable<FileTypePolicy> FileTypePolicies => Db.Queryable<FileTypePolicy>();
    public ISugarQueryable<MailServer> MailServers => Db.Queryable<MailServer>();
    public ISugarQueryable<User> Users => Db.Queryable<User>();
    public ISugarQueryable<Role> Roles => Db.Queryable<Role>();
    public ISugarQueryable<Permission> Permissions => Db.Queryable<Permission>();
    public ISugarQueryable<TablePermission> TablePermissions => Db.Queryable<TablePermission>();
    public ISugarQueryable<UserRole> UserRoles => Db.Queryable<UserRole>();
}