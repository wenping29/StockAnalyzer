using System.Net;
using FundAnalysis.Collector;
using FundAnalysis.Data;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// 命令行参数：--mode=once|daemon  --jobs=daily|minutes|liquidity|initial
builder.Configuration.AddCommandLine(args);

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseDatabase(builder.Configuration));
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
builder.Services.AddScoped<IQuoteProvider, EastmoneyProvider>();
builder.Services.AddScoped<CollectorService>();
builder.Services.AddScoped<LiquidityService>();
builder.Services.AddScoped<FundamentalService>();

// ---------- once 模式（默认）：执行一次采集即退出，供服务器 cron / K8s CronJob 调度 ----------
// 用法示例：
//   docker run --rm fund-collector                      # 收盘后全量（日线+资金流+净值+流动性）
//   docker run --rm fund-collector --jobs=minutes       # 盘中分钟线（每 5 分钟由 cron 触发一次）
//   docker run --rm fund-collector --jobs=initial       # 首次部署初始化示例标的数据
// 退出码：0 成功 / 1 失败（便于 cron 报警）
var mode = builder.Configuration["mode"] ?? "once";
var jobs = builder.Configuration["jobs"] ?? "daily";

if (mode != "daemon")
{
    var host = builder.Build();
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    try
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(); // 首次部署自动建表（与 Web 端共用同一套迁移）

        var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
        var liquidity = scope.ServiceProvider.GetRequiredService<LiquidityService>();
        var fundamentals = scope.ServiceProvider.GetRequiredService<FundamentalService>();

        logger.LogInformation("采集开始：mode=once jobs={Jobs} provider={Provider}",
            jobs, builder.Configuration["Database:Provider"]);

        switch (jobs)
        {
            case "minutes":
                await collector.SyncWatchlistMinutesAsync();
                break;
            case "liquidity":
                await liquidity.SyncAllAsync();
                break;
            case "initial":
                await collector.InitialSyncAsync();
                await liquidity.SyncAllAsync();
                break;
            default: // daily
                await collector.SyncWatchlistDailyAsync();
                await liquidity.SyncAllAsync();
                await collector.SyncIndicesAsync();
                await fundamentals.SyncAllAsync();
                break;
        }

        logger.LogInformation("采集完成");
        return 0;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "采集失败：jobs={Jobs}", jobs);
        return 1;
    }
}

// ---------- daemon 模式：常驻进程，内部定时循环 ----------
builder.Services.AddHostedService<CollectionScheduler>();
await builder.Build().RunAsync();
return 0;
