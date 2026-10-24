using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using FundAnalysis.Data;
using FundAnalysis.Jobs;
using FundAnalysis.Models;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

// Npgsql 时间兼容：系统统一使用本地时间（DateTime.Now），timestamptz 列直写不要求 UTC。
// 仅运行时启用——设计时（dotnet ef）保持默认推断，与迁移快照的列类型保持一致（避免 PendingModelChanges）
if (!EF.IsDesignTime)
    AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// 支持命令行覆盖配置（例如：dotnet run -- --Database:Provider mysql）
builder.Configuration.AddCommandLine(args);

// ---------- 后端监听地址：appsettings.json 的 Server 节可自由配置 ----------
// 优先级：命令行 --urls > 环境变量 ASPNETCORE_URLS > 这里（Server:Host/Port）> 默认 5000
// 宝塔部署示例：appsettings.json 改 "Port": 5200，nginx 反代对应端口即可
var serverHost = config.GetValue("Server:Host", "0.0.0.0");
var serverPort = config.GetValue("Server:Port", 5000);
builder.WebHost.UseUrls($"http://{serverHost}:{serverPort}");

// ---------- MVC + 中文友好 JSON ----------
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// ---------- EF Core（多数据库，按配置切换，默认 SQLite） ----------
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseDatabase(config));

// ---------- Identity + JWT ----------
builder.Services.AddIdentityCore<AppUser>(opt =>
{
    opt.Password.RequireDigit = true;
    opt.Password.RequireLowercase = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequiredLength = 6;
    opt.User.RequireUniqueEmail = false;
    opt.Lockout.MaxFailedAccessAttempts = 10;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager<SignInManager<AppUser>>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();

// ---------- Hangfire 存储：MySQL 数据库时持久化任务历史，其余（SQLite）用内存存储 ----------
// 说明：定时任务每次启动都会重新注册，内存存储开发可用；生产（MySQL）自动持久化
builder.Services.AddHangfire(h =>
{
    h.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
     .UseSimpleAssemblyNameTypeSerializer()
     .UseRecommendedSerializerSettings();

    var dbProvider = (config["Database:Provider"] ?? "sqlite").Trim().ToLowerInvariant();
    switch (dbProvider)
    {
        case "mysql":
            h.UseStorage(new Hangfire.MySql.MySqlStorage(
                config.GetConnectionString("Default"),
                new Hangfire.MySql.MySqlStorageOptions
                {
                    // 生产建议：放宽轮询间隔，降低 MySQL 压力
                    QueuePollInterval = TimeSpan.FromSeconds(15),
                    JobExpirationCheckInterval = TimeSpan.FromHours(1),
                }));
            break;
        case "postgresql":
        case "npgsql":
            h.UsePostgreSqlStorage(config.GetConnectionString("Default"));
            break;
        default:
            h.UseInMemoryStorage();
            break;
    }
});
builder.Services.AddHangfireServer();

// ---------- 业务服务 ----------
builder.Services.AddHttpClient("eastmoney", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 32,
    });
builder.Services.AddHttpClient("tushare");
builder.Services.AddScoped<EastmoneyProvider>();
builder.Services.AddScoped<TushareProvider>();
// 行情数据源可切换：Quote:Provider = eastmoney（默认）| tushare
var quoteProvider = (config["Quote:Provider"] ?? "eastmoney").Trim().ToLowerInvariant();
builder.Services.AddScoped<IQuoteProvider>(sp => quoteProvider switch
{
    "tushare" => sp.GetRequiredService<TushareProvider>(),
    _ => sp.GetRequiredService<EastmoneyProvider>(),
});
builder.Services.AddScoped<CollectorService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<LiquidityService>();
builder.Services.AddScoped<HsgtService>();
builder.Services.AddScoped<FundamentalService>();
builder.Services.AddScoped<FundService>();
builder.Services.AddScoped<AlertService>();
builder.Services.AddScoped<TokenService>();

// ---------- Swagger + CORS ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "基金/股票资金流分析系统 API",
        Version = "v1",
        Description = "资金流入流出 · 多周期趋势 · 收益统计 · 用户权限",
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "先调 /api/auth/login 拿到 accessToken，然后填入：Bearer {token}",
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        },
    });
});
builder.Services.AddCors(o => o.AddPolicy("all",
    p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddMemoryCache();

var app = builder.Build();

// ---------- 数据库初始化 + 定时任务注册 ----------
// EF.IsDesignTime：dotnet ef 迁移工具会执行本文件，此时跳过运行时初始化
//
// 采集调度开关：当部署独立采集器（FundAnalysis.Collector，见 backend/FundAnalysis.Collector）时，
// 将 Web 的 "Collector:ScheduleEnabled" 设为 false，避免两处重复采集。
var scheduleEnabled = config.GetValue("Collector:ScheduleEnabled", true);

if (!EF.IsDesignTime)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 注意：数据库需预先创建（见 deploy/createdb.sql 建库脚本）；表结构由下方迁移自动创建
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider);

        if (scheduleEnabled)
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
            CollectorJobs.Register(jobs);

            // 首次启动（还没有任何行情数据）时，后台队列自动拉取示例标的数据
            if (!db.StockDailies.Any() && !db.FundNavs.Any())
            {
                var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
                bg.Enqueue<CollectorService>(s => s.InitialSyncAsync());
            }

        // 首次启动时拉取央行流动性数据
        if (!db.ShiborRates.Any())
        {
            var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            bg.Enqueue<LiquidityService>(s => s.SyncAllAsync());
        }

        // 首次启动时同步内置指数清单（IndexDaily）
        if (!db.IndexDailies.Any())
        {
            var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            bg.Enqueue<CollectorService>(s => s.SyncIndicesAsync());
        }

        // 首次启动时同步全市场基本面数据（选股）
        if (!db.StockFundamentals.Any())
        {
            var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            bg.Enqueue<FundamentalService>(s => s.SyncAllAsync());
        }

        // 首次启动时同步全量基金清单（基金页）
        if (!db.StockBasics.Any(x => x.Type == SymbolTypes.Fund))
        {
            var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            bg.Enqueue<FundService>(s => s.SyncFundListAsync());
        }
        }
        else
        {
            app.Logger.LogInformation("Web 端采集调度已关闭（Collector:ScheduleEnabled=false），采集由独立采集器负责");
        }
    }
}

// ---------- 生产环境安全校验 ----------
if (!app.Environment.IsDevelopment())
{
    var jwtKey = config["Jwt:Key"] ?? "";
    if (jwtKey.Length < 32 || jwtKey.Contains("Change_Me", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            "生产环境必须配置强 JWT 密钥：设置环境变量 Jwt__Key（至少 32 个随机字符），禁止使用默认值");
}

// ---------- Swagger：仅开发环境开放 ----------
var showSwagger = app.Environment.IsDevelopment() || config.GetValue("Swagger:Enabled", false);

if (showSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FundAnalysis v1"));
}

// ---------- CORS：生产环境必须配置 Cors:Origins 白名单 ----------
var corsOrigins = config.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    app.UseCors(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
}
else if (app.Environment.IsDevelopment())
{
    app.UseCors("all"); // 开发环境放开
}
// 生产环境未配置白名单则不启用 CORS（同域部署不需要）
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHangfireDashboard("/hangfire"); // 仅本机可访问（Hangfire 默认安全策略）

app.Run();
