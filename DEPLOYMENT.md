# DynamicDbApi 跨平台部署指南

本文档提供DynamicDbApi应用程序在跨平台环境下的部署方案，包括IIS和nginx两种反向代理服务器的配置。

## 1. 通用准备工作

### 1.1 安装.NET 8.0 SDK/Runtime

确保目标服务器已安装.NET 8.0 SDK或Runtime：

- **Windows**: 从 https://dotnet.microsoft.com/download/dotnet/8.0 下载安装
- **Linux**: 使用包管理器安装，如Ubuntu的 `apt install dotnet-runtime-8.0`
- **macOS**: 使用Homebrew安装 `brew install dotnet-sdk@8.0`

### 1.2 发布应用程序

在开发机器上发布应用程序：

```bash
# 发布到文件夹
dotnet publish -c Release -o ./publish

# 发布到特定目标（可选）
dotnet publish -c Release -o ./publish --runtime win-x64 --self-contained false
dotnet publish -c Release -o ./publish --runtime linux-x64 --self-contained false
```

### 1.3 复制发布文件

将发布文件夹（./publish）的内容复制到目标服务器的部署目录，例如：
- Windows: `C:\inetpub\wwwroot\DynamicDbApi`
- Linux: `/var/www/dynamicdbapi`

## 2. Windows + IIS 部署

### 2.1 安装IIS和ASP.NET Core模块

1. 打开「控制面板」>「程序」>「启用或关闭Windows功能」
2. 勾选「Internet Information Services」（全选或按需选择组件）
3. 勾选「Microsoft .NET Framework 4.8 Advanced Services」>「ASP.NET 4.8」
4. 下载并安装「ASP.NET Core Hosting Bundle」：https://dotnet.microsoft.com/download/dotnet/8.0

### 2.2 创建IIS站点

1. 打开「Internet Information Services (IIS) 管理器」
2. 右键点击「站点」>「添加网站」
3. 填写网站信息：
   - **网站名称**: DynamicDbApi
   - **物理路径**: 应用程序发布目录（如 `C:\inetpub\wwwroot\DynamicDbApi`）
   - **绑定**: 
     - 类型: http
     - IP地址: 全部未分配
     - 端口: 8080
     - 主机名: 可选

### 2.3 配置应用程序池

1. 在「应用程序池」中找到刚创建的网站对应的应用池
2. 右键点击应用池 >「高级设置」
3. 修改设置：
   - **.NET CLR版本**: 无托管代码
   - **管道模式**: 集成
   - **标识**: 建议使用专用用户或ApplicationPoolIdentity

### 2.4 配置URL重写（可选）

1. 安装「URL重写模块」：https://www.iis.net/downloads/microsoft/url-rewrite
2. 在网站根目录创建 `web.config` 文件（如果不存在）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path=".">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath=".\DynamicDbApi.exe" arguments="" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

## 3. Windows/Linux + nginx 部署

### 3.1 安装nginx

- **Windows**: 项目已包含nginx-1.28.0，可直接使用
- **Linux (Ubuntu)**: `apt install nginx`
- **Linux (CentOS)**: `yum install nginx`

### 3.2 配置nginx

#### 3.2.1 Windows下的nginx配置

1. 打开 `nginx-1.28.0/conf/nginx.conf` 文件
2. 修改 `http` 块中的 `server` 配置：

```nginx
http {
    # ... 其他配置 ...
    
    server {
        listen       80;
        server_name  localhost;

        location / {
            proxy_pass         http://localhost:5182;
            proxy_http_version 1.1;
            proxy_set_header   Upgrade $http_upgrade;
            proxy_set_header   Connection keep-alive;
            proxy_set_header   Host $host;
            proxy_cache_bypass $http_upgrade;
            proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto $scheme;
        }
        
        # Swagger UI 路径配置
        location /swagger {
            proxy_pass         http://localhost:5182/swagger;
            proxy_http_version 1.1;
            proxy_set_header   Upgrade $http_upgrade;
            proxy_set_header   Connection keep-alive;
            proxy_set_header   Host $host;
            proxy_cache_bypass $http_upgrade;
        }
        
        # WebSocket 配置（用于SignalR）
        location /messageHub {
            proxy_pass         http://localhost:5182/messageHub;
            proxy_http_version 1.1;
            proxy_set_header   Upgrade $http_upgrade;
            proxy_set_header   Connection "upgrade";
            proxy_set_header   Host $host;
            proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header   X-Forwarded-Proto $scheme;
        }
        
        # 静态文件配置
        location ~* \.(css|js|jpg|jpeg|png|gif|ico|svg|woff|woff2|ttf|eot)$ {
            proxy_pass         http://localhost:5182;
            expires            30d;
            add_header         Cache-Control "public, no-transform";
        }
        
        # 错误页面
        error_page   500 502 503 504  /50x.html;
        location = /50x.html {
            root   html;
        }
    }
    
    # ... 其他配置 ...
}
```

#### 3.2.2 Linux下的nginx配置

1. 创建nginx配置文件：`/etc/nginx/sites-available/dynamicdbapi`
2. 编写配置内容（与Windows版本类似）：

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass         http://localhost:5182;
        proxy_http_version 1.1;
        proxy_set_header   Upgrade $http_upgrade;
        proxy_set_header   Connection keep-alive;
        proxy_set_header   Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
    
    # 其他location配置与Windows版本相同...
}
```

3. 创建符号链接启用站点：

```bash
ln -s /etc/nginx/sites-available/dynamicdbapi /etc/nginx/sites-enabled/
```

4. 测试配置并重启nginx：

```bash
nginx -t
systemctl restart nginx
```

### 3.3 启动应用程序

在应用程序发布目录下运行：

```bash
# Windows
dotnet DynamicDbApi.dll
# 或直接运行可执行文件
DynamicDbApi.exe

# Linux
dotnet DynamicDbApi.dll
```

### 3.4 配置系统服务（Linux）

创建systemd服务文件 `/etc/systemd/system/dynamicdbapi.service`：

```ini
[Unit]
Description=DynamicDbApi
After=network.target

[Service]
WorkingDirectory=/var/www/dynamicdbapi
ExecStart=/usr/bin/dotnet DynamicDbApi.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
SyslogIdentifier=dynamicdbapi
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```

启用并启动服务：

```bash
systemctl enable dynamicdbapi
systemctl start dynamicdbapi
```

## 4. Docker容器部署

### 4.1 创建Dockerfile

在项目根目录创建 `Dockerfile`：

```dockerfile
# 使用官方.NET SDK作为构建环境
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 复制项目文件
COPY *.csproj ./
RUN dotnet restore

# 复制所有文件并构建
COPY . .
RUN dotnet publish -c Release -o out

# 使用官方.NET Runtime作为运行环境
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 复制构建输出
COPY --from=build /app/out .

# 暴露端口
EXPOSE 80
EXPOSE 443

# 启动应用
ENTRYPOINT ["dotnet", "DynamicDbApi.dll"]
```

### 4.2 创建docker-compose.yml

在项目根目录创建 `docker-compose.yml`：

```yaml
version: '3.8'

services:
  dynamicdbapi:
    build: .
    ports:
      - "5182:80"
      - "7042:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:80;https://+:443
    volumes:
      - ./Data:/app/Data
      - ./wwwroot:/app/wwwroot
      - ./logs:/app/logs
    restart: always

  nginx:
    image: nginx:latest
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx-1.28.0/conf/nginx.conf:/etc/nginx/nginx.conf
      - ./nginx-1.28.0/html:/usr/share/nginx/html
    depends_on:
      - dynamicdbapi
    restart: always
```

### 4.3 构建和运行容器

```bash
# 构建镜像
docker-compose build

# 运行容器
docker-compose up -d

# 查看日志
docker-compose logs -f
```

## 5. 配置SSL/TLS（HTTPS）

### 5.1 IIS配置HTTPS

1. 在IIS管理器中，右键点击网站 >「编辑绑定」
2. 点击「添加」> 类型选择「https」> 选择SSL证书
3. 点击「确定」保存配置

### 5.2 nginx配置HTTPS

修改nginx配置文件，添加HTTPS服务器块：

```nginx
server {
    listen 443 ssl;
    server_name your-domain.com;
    
    ssl_certificate /path/to/your/certificate.crt;
    ssl_certificate_key /path/to/your/private.key;
    
    # SSL配置优化
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers 'ECDHE-ECDSA-AES256-GCM-SHA384:ECDHE-RSA-AES256-GCM-SHA384';
    ssl_prefer_server_ciphers off;
    
    # 其他配置与HTTP版本相同...
    location / {
        proxy_pass http://localhost:5182;
        # ...
    }
}

# HTTP重定向到HTTPS
server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$host$request_uri;
}
```

## 6. 环境变量配置

应用程序支持通过环境变量覆盖配置文件：

```bash
# Windows
set ASPNETCORE_ENVIRONMENT=Production
set ConnectionStrings__SQLite=Data Source=Data/appdb.db

# Linux/macOS
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__SQLite="Data Source=Data/appdb.db"
```

## 7. 验证部署

部署完成后，验证应用程序是否正常运行：

1. 访问API端点：`http://your-server:5182/api/health`
2. 访问Swagger文档：`http://your-server:5182/swagger`
3. 尝试创建API调用任务：`http://your-server:5182/test/api-job-test.html`

## 8. 故障排除

### 8.1 日志查看

- **应用程序日志**: 查看 `logs` 文件夹中的日志文件
- **IIS日志**: `C:\inetpub\logs\LogFiles\W3SVC1`
- **nginx日志**: Windows下 `nginx-1.28.0/logs`，Linux下 `/var/log/nginx`

### 8.2 常见问题

1. **端口被占用**: 检查是否有其他服务占用了80或443端口
2. **权限问题**: 确保应用程序有足够的权限访问文件系统
3. **配置错误**: 检查 `appsettings.json` 或环境变量配置
4. **数据库连接**: 确保数据库文件路径正确且有读写权限

## 9. 监控和维护

- **应用程序状态**: 使用 `systemctl status dynamicdbapi` (Linux) 或IIS管理器(Windows)监控
- **日志监控**: 考虑使用ELK Stack或Graylog等工具集中管理日志
- **自动更新**: 配置CI/CD流程自动部署新版本

## 10. 安全建议

- 定期更新.NET Runtime和依赖包
- 限制API访问权限，使用JWT认证
- 配置HTTPS加密传输
- 定期备份数据库和配置文件
- 限制服务器开放的端口数量

## 11. 性能优化建议

### 11.1 数据库配置优化

根据使用的数据库类型，应用以下优化配置：

#### 11.1.1 MySQL配置优化

修改MySQL配置文件（my.cnf或my.ini）：

```ini
# 基本优化
[mysqld]
# 最大连接数
max_connections = 500
# 连接超时时间
wait_timeout = 300
interactive_timeout = 300

# 缓冲区优化
innodb_buffer_pool_size = 1G  # 建议设置为服务器内存的50-70%
key_buffer_size = 256M  # 仅对MyISAM表有效
join_buffer_size = 8M
sort_buffer_size = 8M
read_buffer_size = 4M
read_rnd_buffer_size = 4M

# 查询优化
query_cache_type = 1
query_cache_size = 64M
query_cache_limit = 2M

# 日志优化
slow_query_log = 1
slow_query_log_file = /var/log/mysql/mysql-slow.log
long_query_time = 2
```

#### 11.1.2 SQL Server配置优化

1. **内存配置**：
   - 打开SQL Server Management Studio
   - 右键点击服务器 > 属性 > 内存
   - 设置「最大服务器内存」为服务器内存的70-80%

2. **连接配置**：
   - 右键点击服务器 > 属性 > 连接
   - 设置「最大并发连接数」为适当值（默认0表示无限制）

3. **查询优化**：
   - 启用查询缓存
   - 定期更新统计信息

4. **读提交快照隔离级别（RCSI）配置**：
   RCSI可以提高并发性能，减少读写阻塞，推荐在OLTP系统中开启。对于生产环境，建议先切换到单用户模式再进行配置，以避免配置过程中的冲突。
   
   - **启用数据库级别RCSI**：
     ```sql
     -- 首先检查当前隔离级别配置
     SELECT name, snapshot_isolation_state, snapshot_isolation_state_desc,
            is_read_committed_snapshot_on
     FROM sys.databases
     WHERE name = 'YourDatabaseName';
     
     -- 1. 将数据库切换到单用户模式
     -- 注意：此操作会中断所有现有连接
     USE master;
     ALTER DATABASE YourDatabaseName
     SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
     
     -- 2. 启用读提交快照隔离级别
     ALTER DATABASE YourDatabaseName
     SET READ_COMMITTED_SNAPSHOT ON;
     
     -- 可选：同时启用快照隔离级别
     ALTER DATABASE YourDatabaseName
     SET ALLOW_SNAPSHOT_ISOLATION ON;
     
     -- 3. 将数据库恢复到多用户模式
     ALTER DATABASE YourDatabaseName
     SET MULTI_USER;
     ```
   
   - **检查是否需要增加tempdb大小**：
     RCSI使用tempdb存储行版本信息，需要确保tempdb有足够空间：
     ```sql
     -- 检查tempdb大小和使用情况
     SELECT name, size, growth, physical_name
     FROM sys.master_files
     WHERE database_id = DB_ID('tempdb');
     ```
   
   - **监控行版本使用情况**：
     ```sql
     -- 监控tempdb行版本使用情况
     SELECT 
         SUM(version_store_reserved_page_count) * 8 AS version_store_kb,
         SUM(internal_object_reserved_page_count) * 8 AS internal_objects_kb,
         SUM(user_object_reserved_page_count) * 8 AS user_objects_kb
     FROM sys.dm_db_file_space_usage;
     ```
   
   **注意**：启用RCSI后，所有使用读提交隔离级别的查询将自动使用RCSI，无需修改应用程序代码。

#### 11.1.3 PostgreSQL配置优化

修改PostgreSQL配置文件（postgresql.conf）：

```ini
# 基本优化
max_connections = 200
shared_buffers = 512MB  # 建议设置为服务器内存的25%

# 工作内存
work_mem = 4MB
maintenance_work_mem = 128MB

# 写缓存
wal_buffers = 16MB
checkpoint_segments = 16
checkpoint_timeout = 5min

# 优化器
random_page_cost = 4.0
effective_cache_size = 1024MB
```

#### 11.1.4 SQLite配置优化

对于SQLite数据库，可以通过连接字符串进行优化：

```csharp
// 优化的SQLite连接字符串
var connectionString = "Data Source=appdb.db;Pooling=True;Max Pool Size=100;Journal Mode=WAL;Synchronous=NORMAL;Cache Size=10000";
```

关键优化参数：
- `Pooling=True`: 启用连接池
- `Journal Mode=WAL`: 写入前日志模式，提高并发性能
- `Synchronous=NORMAL`: 减少磁盘同步操作次数
- `Cache Size=10000`: 增加SQLite内部缓存大小

### 11.2 Nginx优化建议

#### 11.2.1 基本性能优化

修改nginx.conf文件，在http块中添加以下配置：

```nginx
http {
    # 工作进程数，建议设置为CPU核心数
    worker_processes 4;
    worker_cpu_affinity 0001 0010 0100 1000;
    
    # 事件处理配置
    events {
        worker_connections 10240;  # 每个工作进程的最大连接数
        use epoll;  # Linux下使用epoll模型
        multi_accept on;  # 一次接受所有新连接
    }
    
    # 连接超时配置
    keepalive_timeout 65;
    keepalive_requests 10000;
    send_timeout 60;
    
    # 缓冲区配置
    client_max_body_size 100m;  # 允许的最大请求体大小
    client_body_buffer_size 128k;
    client_header_buffer_size 1k;
    large_client_header_buffers 4 8k;
    
    # 压缩配置
    gzip on;
    gzip_min_length 1k;
    gzip_buffers 4 16k;
    gzip_comp_level 5;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml application/xml+rss text/javascript;
    
    # 代理配置优化
    proxy_buffers 16 64k;
    proxy_buffer_size 128k;
    proxy_busy_buffers_size 256k;
    proxy_temp_file_write_size 256k;
    
    # 超时配置
    proxy_connect_timeout 60;
    proxy_send_timeout 60;
    proxy_read_timeout 60;
    
    # 其他优化
    reset_timedout_connection on;
    client_body_timeout 12;
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
}

#### 11.2.3 文件处理优化配置

针对文件上传和下载场景，进行以下优化配置：

```nginx
# 文件下载优化配置
location ~* /api/file/download/ {
    proxy_pass http://dynamicdbapi;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    
    # 大文件下载优化
    proxy_max_temp_file_size 0;  # 禁用临时文件
    proxy_buffering off;  # 禁用代理缓冲
    chunked_transfer_encoding on;  # 启用分块传输编码
    
    # 添加响应头
    add_header X-Accel-Buffering no;
}

# 文件上传优化配置
location ~* /api/file/upload {
    proxy_pass http://dynamicdbapi;
    proxy_http_version 1.1;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    
    # 大文件上传优化
    client_max_body_size 200m;  # 允许的最大上传文件大小
    proxy_request_buffering off;  # 禁用请求缓冲
}

# 静态文件优化配置
location ~* \.(css|js|jpg|jpeg|png|gif|ico|svg|woff|woff2|ttf|eot|pdf)$ {
    proxy_pass http://dynamicdbapi;
    expires            30d;  # 设置缓存过期时间
    add_header         Cache-Control "public, no-transform";
    add_header         Access-Control-Allow-Origin *;
    
    # 静态文件压缩
    gzip_static on;
    gzip_proxied any;
}

# 图片处理优化（可选，需安装nginx ngx_http_image_filter_module）
location ~* /api/image/ {
    proxy_pass http://dynamicdbapi;
    
    # 图片缩放示例
    # image_filter resize 300 200;
    # image_filter quality 90;
}
```

#### 11.2.2 负载均衡配置

如果需要部署多个应用实例，可以配置nginx负载均衡，以下是完善的负载均衡配置：

```nginx
# 负载均衡上游服务器配置
upstream dynamicdbapi {
    # 服务器节点配置
    server localhost:5182 weight=5 max_fails=3 fail_timeout=30s;
    server localhost:5183 weight=3 max_fails=3 fail_timeout=30s;
    server localhost:5184 weight=2 max_fails=3 fail_timeout=30s backup;  # 备份服务器
    
    # 负载均衡算法
    least_conn;  # 最少连接数算法
    # ip_hash;   # 基于客户端IP的会话保持（适合需要会话粘性的场景）
    # round_robin;  # 轮询算法（默认）
    # fair;      # 按响应时间分配
    
    # 健康检查配置
    check interval=5000 rise=2 fall=3 timeout=1000 type=http;
    check_http_send "HEAD /health HTTP/1.0\r\nHost: localhost\r\n\r\n";
    check_http_expect_alive http_2xx http_3xx;
}

server {
    listen 80;  # 如果80端口被占用（如IIS），可以改为其他端口如8080
    # listen 8080;  # 示例：使用8080端口避免与IIS冲突
    server_name your-domain.com;
    
    location / {
        proxy_pass http://dynamicdbapi;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # 代理超时配置
        proxy_connect_timeout 60s;
        proxy_send_timeout 60s;
        proxy_read_timeout 60s;
        
        # 缓冲区配置
        proxy_buffers 16 64k;
        proxy_buffer_size 128k;
        proxy_busy_buffers_size 256k;
        proxy_temp_file_write_size 256k;
    }
    
    # 负载均衡状态监控页面（可选）
    location /nginx_status {
        stub_status on;
        access_log off;
        allow 127.0.0.1;
        deny all;
    }
}
```

### 11.3 系统级优化建议

#### 11.3.1 Linux系统优化

1. **文件描述符限制**：
   ```bash
   # 临时设置
   ulimit -n 65535
   
   # 永久设置，编辑/etc/security/limits.conf
   * soft nofile 65535
   * hard nofile 65535
   ```

2. **内核参数优化**：
   修改/etc/sysctl.conf文件：
   ```bash
   # 网络优化
   net.core.somaxconn = 65535
   net.core.netdev_max_backlog = 65535
   net.ipv4.tcp_max_syn_backlog = 65535
   net.ipv4.tcp_synack_retries = 2
   net.ipv4.tcp_syn_retries = 2
   net.ipv4.tcp_fin_timeout = 30
   net.ipv4.tcp_keepalive_time = 1800
   net.ipv4.tcp_keepalive_probes = 3
   net.ipv4.tcp_keepalive_intvl = 15
   net.ipv4.tcp_max_tw_buckets = 6000
   net.ipv4.tcp_tw_recycle = 1
   net.ipv4.tcp_tw_reuse = 1
   
   # 内存优化
   vm.swappiness = 10
   vm.dirty_ratio = 20
   vm.dirty_background_ratio = 10
   
   # 应用配置
   sysctl -p
   ```

#### 11.3.2 Windows系统优化

1. **增加TCP连接限制**：
   - 编辑注册表：`HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters`
   - 添加/修改以下值：
     - `MaxUserPort`: 65534 (十进制)
     - `TcpTimedWaitDelay`: 30 (十进制)

2. **增加IIS连接限制**：
   - 打开IIS管理器
   - 右键点击服务器 > 属性 > 性能
   - 设置「最大并发连接数」为适当值

### 11.4 部署环境配合方案

#### 11.4.1 负载均衡器配置

如果使用硬件负载均衡器（如F5、Citrix NetScaler）或云服务负载均衡器（如AWS ELB、Azure Load Balancer），建议：

1. 启用会话保持（Sticky Sessions）以确保用户请求始终路由到同一服务器
2. 配置健康检查，定期验证应用程序状态
3. 启用SSL卸载，在负载均衡器层面处理SSL/TLS加密

#### 11.4.2 缓存层部署

为了提高API响应速度，可以部署专用缓存服务器：

1. **Redis缓存服务器**：
   ```bash
   # 安装Redis
   sudo apt install redis-server
   
   # 配置Redis
   sudo nano /etc/redis/redis.conf
   # 设置maxmemory和maxmemory-policy
   maxmemory 1gb
   maxmemory-policy allkeys-lru
   ```

2. **Memcached缓存服务器**：
   ```bash
   # 安装Memcached
   sudo apt install memcached
   
   # 配置Memcached
   sudo nano /etc/memcached.conf
   # 修改内存大小和监听地址
   -m 512
   -l 127.0.0.1
   ```

#### 11.4.3 数据库集群方案

对于高并发场景，建议部署数据库集群：

1. **MySQL主从复制**：实现读写分离
2. **PostgreSQL流复制**：高可用性和负载均衡
3. **SQL Server Always On**：企业级高可用性解决方案

---

部署完成后，您的DynamicDbApi应用程序将能够在跨平台环境下稳定运行，支持IIS或nginx作为反向代理服务器。通过实施上述优化建议，您可以进一步提升应用程序的性能和可靠性。