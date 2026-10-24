using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services.Providers;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>从数据源拉取行情并幂等入库。所有方法可重复执行，已存在的数据跳过、当日数据更新。</summary>
public class CollectorService(AppDbContext db, IQuoteProvider provider, IConfiguration config, ILogger<CollectorService> logger)
{
    private const int DailyBars = 500;      // 约两年日线
    private const int MoneyFlowDays = 120;  // 约半年资金流
    private const int MinuteBars = 100;     // 约 2 个交易日的 5 分钟线

    /// <summary>首次启动的初始化任务：注册示例标的并拉取其全部数据。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task InitialSyncAsync()
    {
        foreach (var symbol in GetSampleSymbols())
        {
            var basic = await EnsureBasicAsync(symbol);
            if (basic is null)
            {
                logger.LogWarning("示例标的 {Symbol} 未能通过搜索接口注册，跳过", symbol);
                continue;
            }
            await SyncSymbolAsync(symbol);
        }
    }

    /// <summary>确保标的已注册到 StockBasic。本地没有时通过搜索接口按代码精确查找并入库。</summary>
    public async Task<StockBasic?> EnsureBasicAsync(string symbol)
    {
        var local = await db.StockBasics.FirstOrDefaultAsync(x => x.Symbol == symbol);
        if (local is not null) return local;

        var suggestions = await provider.FetchSuggestAsync(symbol);
        var match = suggestions.FirstOrDefault(x => x.Symbol == symbol);
        if (match is null) return null;

        db.StockBasics.Add(match);
        await db.SaveChangesAsync();
        logger.LogInformation("标的 {Symbol} {Name} 已注册", match.Symbol, match.Name);
        return match;
    }

    /// <summary>
    /// 同步内置指数清单的日线（IndexDaily）。逐个请求且间隔 2 秒——
    /// 东财 push2his 对同 IP 高频连续请求会触发断连风控，必须低频慢速拉取。
    /// 风控惩罚可持续数小时，重试次数放大到 30 次（按递增间隔覆盖大半天）。
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 30)]
    public async Task SyncIndicesAsync()
    {
        var consecutiveFailures = 0;

        foreach (var (code, name, _, market) in MarketIndices.List)
        {
            try
            {
                // 1500 根约 6 年日线，支撑成交量年粒度聚合
                var bars = await provider.FetchKLineAsync(code, market, 101, 1500, fqt: 0);
                var existingDates = (await db.IndexDailies
                    .Where(x => x.Code == code)
                    .Select(x => x.Date)
                    .ToListAsync()).ToHashSet();

                var added = 0;
                foreach (var b in bars)
                {
                    if (existingDates.Contains(b.Time)) continue;
                    db.IndexDailies.Add(new IndexDaily
                    {
                        Code = code,
                        Market = market,
                        Date = b.Time,
                        Open = b.Open,
                        High = b.High,
                        Low = b.Low,
                        Close = b.Close,
                        Volume = b.Volume,
                        Amount = b.Amount,
                        ChangePct = b.ChangePct,
                    });
                    added++;
                }
                await db.SaveChangesAsync();
                logger.LogInformation("指数 {Code} {Name} 同步完成，新增 {Count} 根", code, name, added);
                consecutiveFailures = 0;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                logger.LogError(ex, "指数 {Code} {Name} 同步失败（连续失败 {Count}）", code, name, consecutiveFailures);
                // 连续 3 个失败视为触发东财 IP 风控，中止本轮（Hangfire 稍后自动重试整个任务）
                if (consecutiveFailures >= 3)
                    throw new InvalidOperationException("连续 3 个指数同步失败，疑似触发东财 IP 频率风控，中止本轮同步");
            }

            // 低频请求防止触发东财 IP 风控（长惩罚刚解除时 2 秒间隔仍会再次触发，放宽到 6 秒）
            await Task.Delay(6000);
        }
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
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
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
        return watchlist.Union(GetSampleSymbols()).Distinct().ToArray();
    }

    private string[] GetSampleSymbols()
        => config.GetSection("Collector:SampleSymbols").Get<string[]>() ?? [];

    private async Task<Dictionary<string, StockBasic>> GetBasicsAsync(IEnumerable<string> symbols)
        => await db.StockBasics.Where(x => symbols.Contains(x.Symbol)).ToDictionaryAsync(x => x.Symbol);
}
