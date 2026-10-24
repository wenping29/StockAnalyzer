using FundAnalysis.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FundAnalysis.Collector;

/// <summary>
/// 常驻模式（--mode=daemon）的定时调度：
///   交易日 16:35 后：日线 + 资金流 + 基金净值 + 央行流动性（每日一次）
///   交易时段（周一至五 9:00-15:55）：每 5 分钟同步分钟线
/// 节假日多跑一次无害（数据幂等），按周末近似判断交易日。
/// </summary>
public class CollectionScheduler(IServiceProvider services, ILogger<CollectionScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("采集器常驻模式启动：交易日 16:35 全量同步，交易时段每 5 分钟分钟线");

        var lastDaily = DateTime.MinValue;
        var lastMinutes = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var weekday = now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
                var afterClose = now.Hour > 16 || (now.Hour == 16 && now.Minute >= 35);
                var inTrading = now.Hour is >= 9 and <= 16;

                if (weekday && afterClose && lastDaily.Date != now.Date)
                {
                    using var scope = services.CreateScope();
                    var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
                    var liquidity = scope.ServiceProvider.GetRequiredService<LiquidityService>();
                    var fundamentals = scope.ServiceProvider.GetRequiredService<FundamentalService>();
                    await collector.SyncWatchlistDailyAsync();
                    await liquidity.SyncAllAsync();
                    await collector.SyncIndicesAsync();
                    await collector.SyncMarketIndicatorsAsync();
                    await fundamentals.SyncAllAsync();
                    lastDaily = now;
                    logger.LogInformation("每日全量同步完成");
                }

                if (weekday && inTrading && (now - lastMinutes).TotalMinutes >= 5)
                {
                    using var scope = services.CreateScope();
                    var collector = scope.ServiceProvider.GetRequiredService<CollectorService>();
                    await collector.SyncWatchlistMinutesAsync();
                    await collector.SyncIndexIntradayAsync();
                    lastMinutes = now;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "采集任务执行失败，1 分钟后重试");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
