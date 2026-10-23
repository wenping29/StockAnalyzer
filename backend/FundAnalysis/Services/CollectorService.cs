using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>从数据源拉取行情并幂等入库。所有方法可重复执行，已存在的数据跳过、当日数据更新。</summary>
public class CollectorService(AppDbContext db, IQuoteProvider provider, IConfiguration config, ILogger<CollectorService> logger)
{
    private const int DailyBars = 500;      // 约两年日线
    private const int MoneyFlowDays = 120;  // 约半年资金流
    private const int MinuteBars = 100;     // 约 2 个交易日的 5 分钟线

    /// <summary>首次启动的初始化任务：全市场标的列表 + 示例标的的日线与分钟线。</summary>
    public async Task InitialSyncAsync()
    {
        await SyncStockListAsync();
        foreach (var symbol in GetAllSymbols())
            await SyncSymbolAsync(symbol);
    }

    /// <summary>同步全市场股票/ETF 基础列表。</summary>
    public async Task SyncStockListAsync()
    {
        try
        {
            var remote = await provider.FetchStockListAsync();
            var existing = await db.StockBasics.ToDictionaryAsync(x => x.Symbol);

            var added = 0;
            foreach (var item in remote)
            {
                if (existing.TryGetValue(item.Symbol, out var local))
                {
                    local.Name = item.Name;
                    local.SecMarket = item.SecMarket;
                    local.Type = item.Type;
                    local.UpdatedAt = DateTime.Now;
                }
                else
                {
                    db.StockBasics.Add(item);
                    added++;
                }
            }
            await db.SaveChangesAsync();
            logger.LogInformation("标的列表同步完成：共 {Total} 条，新增 {Added} 条", remote.Count, added);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "标的列表同步失败");
        }
    }

    /// <summary>定时任务：同步所有用户自选 + 示例标的的日线、资金流、基金净值。</summary>
    public async Task SyncWatchlistDailyAsync()
    {
        foreach (var symbol in GetAllSymbols())
            await SyncSymbolAsync(symbol, includeMinutes: false);
    }

    /// <summary>定时任务：交易时段同步自选标的的 5 分钟线（用于小时趋势图）。</summary>
    public async Task SyncWatchlistMinutesAsync()
    {
        var basics = await GetBasicsAsync(GetAllSymbols());
        foreach (var (symbol, basic) in basics)
        {
            if (basic.Type == SymbolTypes.Fund) continue;
            await SyncMinutesAsync(basic);
        }
    }

    /// <summary>同步单个标的的全部数据（添加自选时立即调用，让数据尽快可见）。</summary>
    public async Task SyncSymbolAsync(string symbol)
        => await SyncSymbolAsync(symbol, includeMinutes: true);

    private async Task SyncSymbolAsync(string symbol, bool includeMinutes)
    {
        var basics = await GetBasicsAsync([symbol]);
        if (!basics.TryGetValue(symbol, out var basic))
        {
            logger.LogWarning("未找到标的 {Symbol}，跳过同步（可能标的列表尚未同步）", symbol);
            return;
        }

        try
        {
            if (basic.Type == SymbolTypes.Fund)
            {
                await SyncFundNavAsync(basic);
            }
            else
            {
                await SyncDailyAsync(basic);
                await SyncMoneyFlowAsync(basic);
                if (includeMinutes)
                    await SyncMinutesAsync(basic);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "标的 {Symbol} 同步失败", symbol);
        }
    }

    private async Task SyncDailyAsync(StockBasic basic)
    {
        var items = await provider.FetchKLineAsync(basic.Symbol, basic.SecMarket, 101, DailyBars);
        var existingDates = (await db.StockDailies
            .Where(x => x.Symbol == basic.Symbol)
            .Select(x => x.Date)
            .ToListAsync()).ToHashSet();

        var latest = items.Count > 0 ? items[^1] : null;
        StockDaily? latestEntity = null;
        if (latest != null && existingDates.Contains(latest.Time))
            latestEntity = await db.StockDailies.FirstAsync(x => x.Symbol == basic.Symbol && x.Date == latest.Time);

        foreach (var item in items)
        {
            if (existingDates.Contains(item.Time))
            {
                // 已有数据只更新最新一根（盘中收盘价会变）
                if (latest != null && item.Time == latest.Time && latestEntity != null)
                {
                    latestEntity.Open = item.Open;
                    latestEntity.High = item.High;
                    latestEntity.Low = item.Low;
                    latestEntity.Close = item.Close;
                    latestEntity.Volume = item.Volume;
                    latestEntity.Amount = item.Amount;
                    latestEntity.ChangePct = item.ChangePct;
                }
                continue;
            }
            db.StockDailies.Add(new StockDaily
            {
                Symbol = basic.Symbol,
                Date = item.Time,
                Open = item.Open,
                High = item.High,
                Low = item.Low,
                Close = item.Close,
                Volume = item.Volume,
                Amount = item.Amount,
                ChangePct = item.ChangePct,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("{Symbol} {Name} 日线同步完成 {Count} 根", basic.Symbol, basic.Name, items.Count);
    }

    private async Task SyncMinutesAsync(StockBasic basic)
    {
        var items = await provider.FetchKLineAsync(basic.Symbol, basic.SecMarket, 5, MinuteBars);
        var existingTimes = (await db.StockMinutes
            .Where(x => x.Symbol == basic.Symbol)
            .Select(x => x.Time)
            .ToListAsync()).ToHashSet();

        foreach (var item in items)
        {
            if (existingTimes.Contains(item.Time)) continue;
            db.StockMinutes.Add(new StockMinute
            {
                Symbol = basic.Symbol,
                Time = item.Time,
                Open = item.Open,
                High = item.High,
                Low = item.Low,
                Close = item.Close,
                Volume = item.Volume,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SyncMoneyFlowAsync(StockBasic basic)
    {
        var items = await provider.FetchMoneyFlowAsync(basic.Symbol, basic.SecMarket, MoneyFlowDays);
        var existingDates = (await db.MoneyFlowDailies
            .Where(x => x.Symbol == basic.Symbol)
            .Select(x => x.Date)
            .ToListAsync()).ToHashSet();

        foreach (var item in items)
        {
            if (existingDates.Contains(item.Date)) continue;
            db.MoneyFlowDailies.Add(new MoneyFlowDaily
            {
                Symbol = basic.Symbol,
                Date = item.Date,
                MainNet = item.MainNet,
                LargeNet = item.LargeNet,
                MidNet = item.MidNet,
                SmallNet = item.SmallNet,
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task SyncFundNavAsync(StockBasic basic)
    {
        var items = await provider.FetchFundNavAsync(basic.Symbol, DailyBars);
        var existingDates = (await db.FundNavs
            .Where(x => x.FundCode == basic.Symbol)
            .Select(x => x.Date)
            .ToListAsync()).ToHashSet();

        foreach (var item in items)
        {
            if (existingDates.Contains(item.Date)) continue;
            db.FundNavs.Add(new FundNav
            {
                FundCode = basic.Symbol,
                Date = item.Date,
                Nav = item.Nav,
                AccNav = item.AccNav,
                ChangePct = item.ChangePct,
            });
        }
        await db.SaveChangesAsync();
        logger.LogInformation("{Symbol} {Name} 基金净值同步完成 {Count} 条", basic.Symbol, basic.Name, items.Count);
    }

    /// <summary>所有用户自选 ∪ 配置中的示例标的。</summary>
    private string[] GetAllSymbols()
    {
        var watchlist = db.WatchlistItems.Select(x => x.Symbol).Distinct().ToList();
        var samples = config.GetSection("Collector:SampleSymbols").Get<string[]>() ?? [];
        return watchlist.Union(samples).Distinct().ToArray();
    }

    private async Task<Dictionary<string, StockBasic>> GetBasicsAsync(IEnumerable<string> symbols)
        => await db.StockBasics.Where(x => symbols.Contains(x.Symbol)).ToDictionaryAsync(x => x.Symbol);
}
