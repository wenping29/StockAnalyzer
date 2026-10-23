using System.Text;
using System.Text.Encodings.Web;
using FundAnalysis.Data;
using FundAnalysis.Jobs;
using FundAnalysis.Models;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- MVC + 中文友好 JSON ----------
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// ---------- EF Core (SQLite) ----------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(config.GetConnectionString("Default")));

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

// ---------- Hangfire（内存存储，Dashboard 在 /hangfire） ----------
// 说明：定时任务在每次启动时重新注册，内存存储足够；以后上 MySQL 可换持久化存储
builder.Services.AddHangfire(h => h
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());
builder.Services.AddHangfireServer();

// ---------- 业务服务 ----------
builder.Services.AddHttpClient("eastmoney");
builder.Services.AddScoped<IQuoteProvider, EastmoneyProvider>();
builder.Services.AddScoped<CollectorService>();
builder.Services.AddScoped<AnalysisService>();
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

var app = builder.Build();

// ---------- 数据库初始化 + 定时任务注册 ----------
// EF.IsDesignTime：dotnet ef 迁移工具会执行本文件，此时跳过运行时初始化
if (!EF.IsDesignTime)
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(scope.ServiceProvider);

        var jobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
        CollectorJobs.Register(jobs);

        // 首次启动（还没有任何行情数据）时，后台队列自动拉取示例标的数据
        if (!db.StockDailies.Any() && !db.FundNavs.Any())
        {
            var bg = scope.ServiceProvider.GetRequiredService<IBackgroundJobClient>();
            bg.Enqueue<CollectorService>(s => s.InitialSyncAsync());
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "FundAnalysis v1"));
app.UseCors("all");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHangfireDashboard("/hangfire"); // 仅本机可访问（Hangfire 默认安全策略）

app.Run();
