using FundAnalysis.Services;
using Hangfire;

namespace FundAnalysis.Jobs;

/// <summary>Hangfire 定时任务注册。任务也可在 /hangfire 面板手动触发。</summary>
public static class CollectorJobs
{
    public static void Register(IRecurringJobManager jobs)
    {
        // 每周一早 8 点刷新全市场标的列表
        jobs.AddOrUpdate<CollectorService>(
            "stock-list", s => s.SyncStockListAsync(), Cron.Weekly(DayOfWeek.Monday, 8));

        // 交易日 16:30 收盘后同步日线 / 资金流 / 基金净值
        jobs.AddOrUpdate<CollectorService>(
            "daily-sync", s => s.SyncWatchlistDailyAsync(), "30 16 * * 1-5");

        // 交易时段每 5 分钟同步 5 分钟线（9:00-15:55）
        jobs.AddOrUpdate<CollectorService>(
            "minute-sync", s => s.SyncWatchlistMinutesAsync(), "*/5 9-15 * * 1-5");

        // 央行流动性数据（Shibor/货币供应/存准率/LPR）：每日 20:00
        jobs.AddOrUpdate<LiquidityService>(
            "liquidity-sync", s => s.SyncAllAsync(), "0 20 * * *");
    }
}
