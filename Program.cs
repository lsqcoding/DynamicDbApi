using System.Text;
using System.IO;
using DynamicDbApi.Controllers;
using DynamicDbApi.Hubs;
using DynamicDbApi.Data;
using DynamicDbApi.Infrastructure;
using DynamicDbApi.Models; // 已导入Models命名空间，可以直接使用RateLimitingOptions
using DynamicDbApi.Models.Auth;
using DynamicDbApi.Models.Validation;
using DynamicDbApi.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Quartz;
using Quartz.Impl;
using Serilog;
using SqlSugar;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// 配置Serilog日志
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    // 控制台日志 - 不显示SQL日志
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(e => e.Properties.ContainsKey("SqlLog"))
        .WriteTo.Console())
    // 运行日志
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(e => e.Properties.ContainsKey("SqlLog"))
        .WriteTo.File("logs/log-.txt", 
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"))
    // SQL专用日志
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("SqlLog"))
        .WriteTo.File("logs/sql-log-.txt", 
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] SQL日志{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"))
    .CreateLogger();

builder.Host.UseSerilog();

// 添加控制器
builder.Services.AddControllers()
    .AddApplicationPart(typeof(DynamicQueryController).Assembly);

// 读取认证配置
var authSettings = builder.Configuration.GetSection("AuthSettings").Get<AuthSettings>();
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();

// 读取OAuth2Settings并确保Scopes不重复
var oauth2Settings = new OAuth2Settings();
builder.Configuration.GetSection("OAuth2Settings").Bind(oauth2Settings);

// 确保Scopes不重复 - 无论来自配置文件还是默认值
oauth2Settings.Scopes = oauth2Settings.Scopes.Distinct().ToList();

var windowsAdSettings = builder.Configuration.GetSection("WindowsADSettings").Get<WindowsADSettings>();

// 添加Swagger/OpenAPI支持
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    // 添加JWT认证支持
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
                Scheme = "oauth2",
                Name = "Bearer",
                In = ParameterLocation.Header,

            },
            new List<string>()
        }
    });
    
    // 明确指定要扫描的程序集
    options.SwaggerDoc("v1", new OpenApiInfo 
    {
        Title = "DynamicDB API",
        Version = "v1",
        Description = "Dynamic Database API with table alias support"
    });
    
    // 确保所有API都包含在文档中
    options.DocInclusionPredicate((docName, apiDesc) => true);
    
    // 添加控制器文档包含规则
    options.TagActionsBy(api => new[] { api.ActionDescriptor.RouteValues["controller"] });
    
    // 添加一个明确的过滤器，确保所有控制器都被包含
    options.DocumentFilter<AllControllersDocumentFilter>();
});

// 配置认证

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = authSettings?.DefaultAuthentication switch
        {
            "OAuth2" => OpenIdConnectDefaults.AuthenticationScheme,
            "WindowsAD" => CookieAuthenticationDefaults.AuthenticationScheme, // Windows AD使用Cookie认证
            _ => JwtBearerDefaults.AuthenticationScheme // 默认使用JWT
        };
        options.DefaultChallengeScheme = authSettings?.DefaultAuthentication switch
        {
            "OAuth2" => OpenIdConnectDefaults.AuthenticationScheme,
            "WindowsAD" => CookieAuthenticationDefaults.AuthenticationScheme,
            _ => JwtBearerDefaults.AuthenticationScheme
        };
    });
    
    // 添加JWT认证支持
    if (authSettings?.AllowedAuthentications.Contains("Jwt") == true && jwtSettings != null)
    {
        authBuilder.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key ?? "DefaultKeyForDevelopmentPurposesOnly"))
            };
        });
    }
    
    // 添加OAuth2认证支持
    if (authSettings?.AllowedAuthentications.Contains("OAuth2") == true && oauth2Settings != null)
    {
        authBuilder.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = oauth2Settings.Authority;
            options.ClientId = oauth2Settings.ClientId;
            options.ClientSecret = oauth2Settings.ClientSecret;
            options.CallbackPath = oauth2Settings.CallbackPath;
            options.ResponseType = "code";
            
            // 添加所需的作用域（去重后）
            options.Scope.Clear();
            var distinctScopes = oauth2Settings.Scopes.Distinct().ToList();
            foreach (var scope in distinctScopes)
            {
                options.Scope.Add(scope);
            }
            
            // 保存令牌
            options.SaveTokens = true;
            
            // OpenID Connect中间件会自动映射标准声明，无需手动配置
            
            // 配置事件处理
            options.Events = new OpenIdConnectEvents
            {
                OnTokenValidated = context =>
                {
                    // 可以在这里添加自定义的令牌验证逻辑
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    context.Response.Redirect($"/Home/Error?message={context.Exception.Message}");
                    context.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });
    }
    
    // 添加Windows AD认证支持
    if (authSettings?.AllowedAuthentications.Contains("WindowsAD") == true && windowsAdSettings != null)
    {
        authBuilder.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // 添加Windows认证（集成Windows身份验证）
        authBuilder.AddNegotiate();
        
        // 配置认证策略
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("WindowsAD", policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
                policy.AuthenticationSchemes.Add(NegotiateDefaults.AuthenticationScheme);
            });
        });
    }
    
    // 注册日志服务（必须先注册，其他服务依赖它）
    builder.Services.AddSingleton<ILoggingService, LoggingService>();
    
    // 注册数据库连接管理器（依赖ILoggingService）
    builder.Services.AddSingleton<IDatabaseConnectionManager, DatabaseConnectionManager>();
    
    // 注册表别名服务（依赖IDatabaseConnectionManager和ILoggingService）
    builder.Services.AddSingleton<ITableAliasService, TableAliasService>();
    // 注册表别名自动刷新服务
    builder.Services.AddHostedService<TableAliasRefreshService>();
    
    // 注册数据库连接管理服务（依赖ILoggingService）
    builder.Services.AddSingleton<IConnectionManager, ConnectionManager>();
    
    // 注册SqlSugar客户端服务
    builder.Services.AddSingleton<ISqlSugarClient>(provider =>
    {
        var connectionManager = provider.GetRequiredService<IDatabaseConnectionManager>();
        return connectionManager.GetDbClient("default");
    });
    
    // 注册AppDbContext (SqlSugar版本)
    builder.Services.AddSingleton(provider =>
    {
        var connectionManager = provider.GetRequiredService<IDatabaseConnectionManager>();
        var connection = connectionManager.GetDbClient("default");
        // 直接传递SqlSugarClient实例给AppDbContext
        return new AppDbContext(connection);
    });
    
    // 注册HttpClientFactory用于API调用
    builder.Services.AddHttpClient();
    
    // 注册动态查询服务
    builder.Services.AddScoped<IDynamicQueryService, DynamicQueryService>();
    builder.Services.AddScoped<IPermissionService, PermissionService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IMailService, MailService>();
    builder.Services.AddSignalR();
    builder.Services.AddScoped<IRealTimeMessageService, RealTimeMessageService>();
    builder.Services.AddScoped<IFileStorageService, FileStorageService>();
    builder.Services.AddSingleton<ISchedulerService, SchedulerService>();
    
    // 注册查询分析服务（用于索引建议）
    builder.Services.AddSingleton<IQueryAnalysisService, QueryAnalysisService>();
    
    // 注册缓存服务
    builder.Services.AddSingleton<DynamicDbApi.Services.ICacheService, DynamicDbApi.Services.MemoryCacheService>();
    
    // 配置API限流 - 从appsettings.json读取配置
    var rateLimitingConfig = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>();
    if (rateLimitingConfig?.Enabled == true)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = rateLimitingConfig.RejectionStatusCode;
            
            // 固定窗口限流策略
            options.AddFixedWindowLimiter("default", fixedWindowOptions =>
            {
                fixedWindowOptions.Window = TimeSpan.FromSeconds(rateLimitingConfig.DefaultPolicy.WindowInSeconds);
                fixedWindowOptions.PermitLimit = rateLimitingConfig.DefaultPolicy.PermitLimit;
                fixedWindowOptions.QueueLimit = rateLimitingConfig.DefaultPolicy.QueueLimit;
            });
            
            // 针对查询操作的更严格限流策略
            options.AddFixedWindowLimiter("query", fixedWindowOptions =>
            {
                fixedWindowOptions.Window = TimeSpan.FromSeconds(rateLimitingConfig.QueryPolicy.WindowInSeconds);
                fixedWindowOptions.PermitLimit = rateLimitingConfig.QueryPolicy.PermitLimit;
                fixedWindowOptions.QueueLimit = rateLimitingConfig.QueryPolicy.QueueLimit;
            });
        });
    }
    
    
    
    // 注册任务执行器
    builder.Services.AddScoped<IJobExecutor, ApiJobExecutor>();
    builder.Services.AddScoped<IJobExecutor, RefreshTableAliasesJobExecutor>();
    
    // 注册Quartz相关服务
    builder.Services.AddSingleton(provider => 
    {
        var jobFactory = new Quartz.Simpl.SimpleJobFactory();
        return (Quartz.Spi.IJobFactory)jobFactory;
    });
    builder.Services.AddSingleton(provider => 
    {
        var schedulerFactory = new Quartz.Impl.StdSchedulerFactory();
        return (Quartz.ISchedulerFactory)schedulerFactory;
    });
    
    // 注册IScheduler服务
    builder.Services.AddSingleton(provider => 
    {
        var schedulerFactory = provider.GetRequiredService<ISchedulerFactory>();
        var scheduler = schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        return scheduler;
    });
    
    // 注册JobDataService
    builder.Services.AddSingleton<JobDataService>();
    
    // 注册FluentValidation验证器
    builder.Services.AddValidatorsFromAssemblyContaining<ScheduledTaskValidator>();
    
    // 注册FluentValidation验证器
    builder.Services.AddValidatorsFromAssemblyContaining<ScheduledTaskValidator>();
    
    // 注册FluentValidation验证器
    builder.Services.AddValidatorsFromAssemblyContaining<ScheduledTaskValidator>();
    
    // 配置CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowSpecificOrigins",
            policy =>
            {
                var corsOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>();
                if (corsOrigins != null && corsOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        
        options.AddPolicy("FileUploadPolicy",
            policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("Content-Disposition"));
    });
    
    // 配置Kestrel服务器端口 - 监听所有IP地址以便调试
    builder.WebHost.ConfigureKestrel(serverOptions => {
        serverOptions.ListenAnyIP(5182); // HTTP - 监听所有IP地址
        serverOptions.ListenAnyIP(7042, listenOptions => { // HTTPS - 监听所有IP地址
            listenOptions.UseHttps();
        });
    });
    
    
    var app = builder.Build();
    
    // 初始化数据库 - 根据配置决定是否初始化
    var initEnabled = app.Configuration.GetValue<bool>("InitializeDatabase:Enabled");
    var seedData = app.Configuration.GetValue<bool>("InitializeDatabase:SeedData");
    
    // 使用控制台直接输出配置值进行调试
    Console.WriteLine($"[DEBUG] InitializeDatabase:Enabled = {initEnabled}");
    Console.WriteLine($"[DEBUG] InitializeDatabase:SeedData = {seedData}");
    
    if (initEnabled)
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbManager = scope.ServiceProvider.GetRequiredService<IDatabaseConnectionManager>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation($"开始数据库初始化... [Enabled={initEnabled}, SeedData={seedData}]");
            try
            {
                // 获取默认数据库连接
                var defaultDb = dbManager.GetDefaultDbClient();
                
                // 如果配置了需要初始化数据，则执行初始化SQL脚本
                if (app.Configuration.GetValue<bool>("InitializeDatabase:SeedData"))
                {
                    var initScriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "init_db.sql");
                    if (File.Exists(initScriptPath))
                    {
                        logger.LogInformation("正在执行初始化SQL脚本...");
                        var sql = File.ReadAllText(initScriptPath);
                        
                        // 使用SqlSugar执行SQL脚本
                        defaultDb.Ado.ExecuteCommand(sql);
                        logger.LogInformation("初始化SQL脚本执行完成");
                    }
                }
                
                logger.LogInformation("数据库初始化完成");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "数据库初始化失败");
                throw;
            }
        }
    }
    
    // 启动定时任务服务（如果配置启用）
    if (app.Configuration.GetValue<bool>("Scheduler:Enabled"))
    {
        var schedulerService = app.Services.GetRequiredService<ISchedulerService>();
        if (app.Configuration.GetValue<bool>("Scheduler:AutoStart"))
        {
            await schedulerService.StartAsync();
        }
    }
    
    // 配置HTTP请求管道
    // 禁用HTTPS重定向，因为我们使用nginx作为反向代理
    // app.UseHttpsRedirection();
    app.UseGlobalExceptionHandler();
    app.UseValidationExceptionHandler();
    app.UseIpWhitelist();
    app.UseCors("AllowSpecificOrigins");
    
    // 只在限流功能启用时添加API限流中间件
    if (rateLimitingConfig?.Enabled == true)
    {
        app.UseRateLimiter();
    }
    app.UseAuthentication();
    app.UseAuthorization();
    
    // 强制启用Swagger（无论环境）
    app.UseSwagger(options =>
    {
        options.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            Log.Information("Swagger PreSerializeFilter called for {RequestPath}", httpReq.Path);
        });
    });
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DynamicDB API v1");
        c.RoutePrefix = "swagger";
        c.ConfigObject.DisplayRequestDuration = true;
        Log.Information("SwaggerUI configured with endpoint /swagger/v1/swagger.json");
    });
    
    // 添加开发者异常页面（仅开发环境）
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    
    app.MapControllers();
    app.MapHub<MessageHub>("/messageHub");
    
    // 数据库初始化 - 检查并创建admin用户
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loggingService = scope.ServiceProvider.GetRequiredService<ILoggingService>();
        
        try
        {
            // 检查是否已经存在admin用户
            var adminUser = dbContext.Users.Where(u => u.Username == "admin").First();
            loggingService.LogInformation("Admin user already exists");
        }
        catch
        {
            // 创建admin用户
            var adminUser = new User
            {
                Username = "admin",
                Password = "admin123", // 注意：在实际应用中，密码应该进行哈希处理
                Role = "Admin",
                Email = "admin@example.com"
            };
            
            dbContext.Db.Insertable(adminUser).ExecuteCommand();
            loggingService.LogInformation("Admin user created successfully");
        }
    }
    app.Run();

// 自定义文档过滤器，确保所有控制器都被包含
public class AllControllersDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        // 确保所有控制器都被包含
        var allControllers = context.ApiDescriptions.Select(desc => desc.ActionDescriptor.RouteValues["controller"]).Distinct();
        foreach (var controller in allControllers)
        {
            // 为每个控制器添加一个标签
            swaggerDoc.Tags.Add(new OpenApiTag { Name = controller });
        }
    }
}