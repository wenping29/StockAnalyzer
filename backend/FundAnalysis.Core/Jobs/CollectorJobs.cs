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

        // 央行逆回购公告（约 09:20 发布）：交易日 09:45 增量同步
        jobs.AddOrUpdate<LiquidityService>(
            "omo-sync", s => s.SyncOmoAsync(), "45 9 * * 1-5");

        // 内置指数清单日线（低频慢速拉取防风控）：交易日 16:40
        jobs.AddOrUpdate<CollectorService>(
            "indices-sync", s => s.SyncIndicesAsync(), "40 16 * * 1-5");

        // 全市场基本面（估值+业绩，选股用）：交易日 17:10
        jobs.AddOrUpdate<FundamentalService>(
            "fundamentals-sync", s => s.SyncAllAsync(), "10 17 * * 1-5");

        // 自选价格提醒检查：交易时段每 5 分钟
        jobs.AddOrUpdate<AlertService>(
            "alert-check", s => s.CheckAllAsync(), "*/5 9-15 * * 1-5");

        // 全量基金清单（类型/名称）：每周日 9:00
        jobs.AddOrUpdate<FundService>(
            "fund-list-sync", s => s.SyncFundListAsync(), Cron.Weekly(DayOfWeek.Sunday, 9));
    }
}
