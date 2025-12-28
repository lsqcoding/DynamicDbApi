-- SQLite数据库初始化脚本
-- 基于项目实体模型优化版本，包含完整的表结构和初始测试数据

BEGIN TRANSACTION;

-- 系统日志表
CREATE TABLE IF NOT EXISTS SystemLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    LogLevel TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 用户表 (根据User.cs模型)
CREATE TABLE IF NOT EXISTS Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT UNIQUE NOT NULL,
    Password TEXT NOT NULL,
    Email TEXT,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 用户组表 (根据UserGroup.cs模型)
CREATE TABLE IF NOT EXISTS UserGroups (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId TEXT NOT NULL,
    GroupId TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(UserId, GroupId)
);

-- 群组信息表
CREATE TABLE IF NOT EXISTS Groups (
    Id TEXT PRIMARY KEY,
    GroupName TEXT NOT NULL,
    Description TEXT,
    CreatorId TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- 角色表 (根据Role.cs模型)
CREATE TABLE IF NOT EXISTS Roles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    IsActive BOOLEAN DEFAULT 1,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME
);

-- 权限表 (根据Permission.cs模型)
CREATE TABLE IF NOT EXISTS Permissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RoleId INTEGER NOT NULL,
    DatabaseId TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

-- 表权限表 (根据TablePermission.cs模型)
CREATE TABLE IF NOT EXISTS TablePermissions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    PermissionId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    AllowedOperations TEXT NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME,
    FOREIGN KEY (PermissionId) REFERENCES Permissions(Id)
);

-- 用户角色关联表 (根据UserRole.cs模型)
CREATE TABLE IF NOT EXISTS UserRoles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserId INTEGER NOT NULL,
    RoleId INTEGER NOT NULL,
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(UserId, RoleId),
    FOREIGN KEY (UserId) REFERENCES Users(Id),
    FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

-- 表别名表
CREATE TABLE IF NOT EXISTS TableAliases (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Alias TEXT NOT NULL,
    RealTableName TEXT NOT NULL,
    DatabaseId TEXT NOT NULL DEFAULT 'default',
    UNIQUE(Alias, DatabaseId)
);

-- 文件存储表 (根据FileStorage.cs的StoredFile模型)
CREATE TABLE IF NOT EXISTS StoredFiles (
    Id TEXT PRIMARY KEY, -- GUID类型
    OriginalName TEXT NOT NULL,
    DisplayName TEXT,
    StorageName TEXT NOT NULL,
    ContentType TEXT NOT NULL,
    Size INTEGER NOT NULL, -- SQLite不支持bigint，使用integer
    StoragePath TEXT NOT NULL,
    UploadTime DATETIME NOT NULL,
    UploaderId TEXT
);

-- 文件分享链接表 (根据FileStorage.cs的FileShareLink模型)
CREATE TABLE IF NOT EXISTS FileShareLinks (
    Id TEXT PRIMARY KEY, -- GUID类型
    FileId TEXT NOT NULL,
    PasswordHash TEXT,
    ExpireTime DATETIME NOT NULL,
    CreateTime DATETIME NOT NULL,
    CreatorId TEXT,
    IsActive INTEGER NOT NULL,
    FOREIGN KEY (FileId) REFERENCES StoredFiles(Id)
);

-- 文件类型策略表 (根据FileStorage.cs的FileTypePolicy模型)
CREATE TABLE IF NOT EXISTS FileTypePolicies (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FileType TEXT NOT NULL,
    IsBlacklisted INTEGER NOT NULL,
    Description TEXT
);

-- 邮件服务器配置表 (根据MailServer.cs模型)
CREATE TABLE IF NOT EXISTS MailServers (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT,
    Host TEXT,
    Port INTEGER NOT NULL,
    UserName TEXT,
    Password TEXT,
    EnableSsl INTEGER NOT NULL,
    DefaultFrom TEXT,
    DisplayName TEXT,
    IsDefault INTEGER NOT NULL,
    Enabled INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL
);

-- 实时消息表 (根据RealTimeMessage.cs模型)
CREATE TABLE IF NOT EXISTS RealTimeMessages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    SenderId TEXT NOT NULL,
    Content TEXT NOT NULL,
    ReceiverType INTEGER NOT NULL, -- 0: 用户, 1: 群组, 2: 用户组, 3: 广播
    ReceiverId TEXT,
    ActionType TEXT,
    ActionPayload TEXT,
    IsRead INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL
);

-- 定时任务表
CREATE TABLE IF NOT EXISTS ScheduledTasks (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    JobType TEXT NOT NULL,
    TriggerType TEXT NOT NULL,
    TriggerExpression TEXT NOT NULL,
    StartTime DATETIME,
    EndTime DATETIME,
    IsActive INTEGER NOT NULL,
    CreatedAt DATETIME NOT NULL,
    LastRunTime DATETIME,
    NextRunTime DATETIME,
    LastRunStatus TEXT NOT NULL
);

-- 插入初始测试用户数据
-- 使用更安全的密码存储方式，这里使用SHA-256哈希值 (密码: 123456)
INSERT OR IGNORE INTO Users (Username, Password, Email, CreatedAt) VALUES 
('admin', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'admin@example.com', CURRENT_TIMESTAMP),
('user1', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'user1@example.com', CURRENT_TIMESTAMP),
('user2', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'user2@example.com', CURRENT_TIMESTAMP),
('guest', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', 'guest@example.com', CURRENT_TIMESTAMP);

-- 插入群组数据
INSERT OR IGNORE INTO Groups (Id, GroupName, Description, CreatorId, CreatedAt) VALUES
('group1', '开发团队', '核心开发成员群组', '1', CURRENT_TIMESTAMP),
('group2', '测试团队', '测试人员群组', '1', CURRENT_TIMESTAMP),
('group3', '市场部', '市场推广团队群组', '1', CURRENT_TIMESTAMP);

-- 插入用户组关系数据
INSERT OR IGNORE INTO UserGroups (UserId, GroupId, CreatedAt) VALUES
('1', 'group1', CURRENT_TIMESTAMP),
('2', 'group1', CURRENT_TIMESTAMP),
('2', 'group2', CURRENT_TIMESTAMP),
('3', 'group2', CURRENT_TIMESTAMP),
('3', 'group3', CURRENT_TIMESTAMP);

-- 插入初始角色
INSERT OR IGNORE INTO Roles (Name, Description) VALUES 
('Admin', '系统管理员角色，拥有所有权限'),
('User', '普通用户角色，拥有基本权限'),
('Guest', '访客角色，拥有最低权限');

-- 获取角色ID
INSERT OR REPLACE INTO Roles (Id, Name, Description) SELECT 1, 'Admin', '系统管理员角色，拥有所有权限' WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Admin');
INSERT OR REPLACE INTO Roles (Id, Name, Description) SELECT 2, 'User', '普通用户角色，拥有基本权限' WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'User');
INSERT OR REPLACE INTO Roles (Id, Name, Description) SELECT 3, 'Guest', '访客角色，拥有最低权限' WHERE NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Guest');

-- 插入管理员权限（所有数据库）
INSERT OR IGNORE INTO Permissions (Id, RoleId, DatabaseId) VALUES (1, 1, '*');

-- 插入管理员表权限（所有表，所有操作）
INSERT OR IGNORE INTO TablePermissions (PermissionId, Name, AllowedOperations) VALUES 
(1, '*', 'select,insert,update,delete');

-- 插入普通用户权限（仅默认数据库）
INSERT OR IGNORE INTO Permissions (Id, RoleId, DatabaseId) VALUES (2, 2, 'default');

-- 插入普通用户表权限（所有表，仅查询操作）
INSERT OR IGNORE INTO TablePermissions (PermissionId, Name, AllowedOperations) VALUES 
(2, '*', 'select');

-- 插入访客权限（仅默认数据库）
INSERT OR IGNORE INTO Permissions (Id, RoleId, DatabaseId) VALUES (3, 3, 'default');

-- 插入访客表权限（仅Products表，查询操作）
INSERT OR IGNORE INTO TablePermissions (PermissionId, Name, AllowedOperations) VALUES 
(3, 'Products', 'select');

-- 插入用户角色关联数据
-- admin用户拥有Admin角色
INSERT OR IGNORE INTO UserRoles (UserId, RoleId) VALUES 
(1, 1);

-- user1用户同时拥有User和Admin角色（用于测试多角色权限取并集）
INSERT OR IGNORE INTO UserRoles (UserId, RoleId) VALUES 
(2, 2),
(2, 1);

-- user2用户拥有User角色
INSERT OR IGNORE INTO UserRoles (UserId, RoleId) VALUES 
(3, 2);

-- guest用户拥有Guest角色
INSERT OR IGNORE INTO UserRoles (UserId, RoleId) VALUES 
(4, 3);

-- 插入表别名数据
INSERT OR IGNORE INTO TableAliases (Alias, RealTableName, DatabaseId) VALUES
('u', 'Users', 'default'),
('p', 'Products', 'default'),
('l', 'SystemLogs', 'default'),
('m', 'RealTimeMessages', 'default'),
('s', 'StoredFiles', 'default');

-- 初始化文件类型策略
INSERT OR IGNORE INTO FileTypePolicies (FileType, IsBlacklisted, Description)
VALUES 
('.exe', 1, 'Executable files'),
('.bat', 1, 'Batch files'),
('.cmd', 1, 'Command files'),
('.ps1', 1, 'PowerShell scripts'),
('.sh', 1, 'Shell scripts'),
('.dll', 1, 'Dynamic link libraries'),
('.js', 0, 'JavaScript files'),
('.json', 0, 'JSON files'),
('.txt', 0, 'Text files'),
('.pdf', 0, 'PDF documents'),
('.doc', 0, 'Word documents'),
('.docx', 0, 'Word documents'),
('.xls', 0, 'Excel documents'),
('.xlsx', 0, 'Excel documents'),
('.ppt', 0, 'PowerPoint documents'),
('.pptx', 0, 'PowerPoint documents'),
('.png', 0, 'PNG images'),
('.jpg', 0, 'JPEG images'),
('.jpeg', 0, 'JPEG images'),
('.gif', 0, 'GIF images'),
('.zip', 0, 'ZIP archives'),
('.rar', 0, 'RAR archives'),
('.7z', 0, '7-Zip archives');

-- 初始化邮件服务器配置
INSERT OR IGNORE INTO MailServers (Name, Host, Port, UserName, Password, EnableSsl, DefaultFrom, DisplayName, IsDefault, Enabled, CreatedAt)
VALUES (
'Default SMTP Server',
'smtp.example.com',
587,
'username',
'password',
1,
'noreply@example.com',
'DynamicDB API',
1,
1,
CURRENT_TIMESTAMP
);

-- 插入测试实时消息数据
INSERT OR IGNORE INTO RealTimeMessages (SenderId, Content, ReceiverType, ReceiverId, ActionType, ActionPayload, IsRead, CreatedAt) VALUES
('1', '欢迎使用动态数据库API系统！', 3, NULL, 'SystemWelcome', '{"version":"1.0.0"}', 0, CURRENT_TIMESTAMP),
('1', '请查看最新的系统公告', 1, 'group1', 'Announcement', '{"title":"系统更新"}', 0, CURRENT_TIMESTAMP),
('1', '你好，这是一条测试消息', 0, '2', 'Chat', '{"type":"text"}', 0, CURRENT_TIMESTAMP);

-- 插入测试定时任务
INSERT OR IGNORE INTO ScheduledTasks (Name, Description, JobType, TriggerType, TriggerExpression, StartTime, EndTime, IsActive, CreatedAt, LastRunTime, NextRunTime, LastRunStatus) VALUES
('数据库备份', '每日自动备份数据库', 'DatabaseBackup', 'Daily', '0 0 2 * * ?', CURRENT_TIMESTAMP, NULL, 1, CURRENT_TIMESTAMP, NULL, DATETIME(CURRENT_TIMESTAMP, '+1 day', '+2 hours'), 'Pending'),
('系统清理', '每周清理临时文件', 'SystemCleanup', 'Weekly', '0 0 1 * * 0', CURRENT_TIMESTAMP, NULL, 1, CURRENT_TIMESTAMP, NULL, DATETIME(CURRENT_TIMESTAMP, '+7 day', '+1 hour'), 'Pending');

-- 插入测试文件存储记录
INSERT OR IGNORE INTO StoredFiles (Id, OriginalName, DisplayName, StorageName, ContentType, Size, StoragePath, UploadTime, UploaderId) VALUES
('f1', 'test.txt', '测试文件.txt', 'test_123456.txt', 'text/plain', 1024, '/storage/files/test_123456.txt', CURRENT_TIMESTAMP, '1'),
('f2', 'sample.pdf', '示例文档.pdf', 'sample_789012.pdf', 'application/pdf', 20480, '/storage/files/sample_789012.pdf', CURRENT_TIMESTAMP, '2');

-- 插入测试文件分享链接
INSERT OR IGNORE INTO FileShareLinks (Id, FileId, PasswordHash, ExpireTime, CreateTime, CreatorId, IsActive) VALUES
('sl1', 'f1', NULL, DATETIME(CURRENT_TIMESTAMP, '+30 day'), CURRENT_TIMESTAMP, '1', 1),
('sl2', 'f2', '8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92', DATETIME(CURRENT_TIMESTAMP, '+7 day'), CURRENT_TIMESTAMP, '2', 1);

-- 插入系统日志
INSERT OR IGNORE INTO SystemLogs (LogLevel, Message, CreatedAt) VALUES
('Information', '系统初始化完成', CURRENT_TIMESTAMP),
('Information', '数据库表结构创建成功', CURRENT_TIMESTAMP),
('Information', '初始测试数据导入成功', CURRENT_TIMESTAMP);

COMMIT;

-- 初始化完成说明
-- 1. 系统已创建完整的表结构，包括用户、角色、权限、消息、文件存储等
-- 2. 已添加测试用户：admin、user1、user2、guest，密码均为：123456
-- 3. 已创建测试群组和用户组关系
-- 4. 已配置角色权限规则和文件类型策略
-- 5. 系统已准备就绪，可以开始使用