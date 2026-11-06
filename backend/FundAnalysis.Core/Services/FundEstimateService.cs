using System.Collections.Concurrent;
using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services.Providers;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 基金当日涨跌估算：读取最新季报前十大重仓股（缓存 7 天，缺失时现拉），
/// 批量取成分股实时行情后按占净值比例加权，得到基金当日估算涨跌幅。
/// 公募基金只按季披露前十大重仓，估算只覆盖披露部分，因此按"有行情的持仓权重"归一，
/// 并返回覆盖比例供前端提示。
/// </summary>
public class FundEstimateService(
    AppDbContext db, IQuoteProvider provider, ILogger<FundEstimateService> logger)
{
    private const int MaxFundsPerRequest = 60;
    private const int MaxConcurrency = 4;
    private static readonly TimeSpan HoldingTtl = TimeSpan.FromDays(7);

    // 同一进程内串行化缓存写入，避免并发请求重复删除/插入触发唯一索引冲突
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public async Task<List<FundEstimateDto>> GetEstimatesAsync(
        IEnumerable<string> fundSymbols, CancellationToken ct = default)
    {
        var symbols = fundSymbols
            .Select(s => s.Trim())
            .Where(s => s.EndsWith(".F", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxFundsPerRequest)
            .ToList();
        if (symbols.Count == 0) return [];

        var funds = await db.StockBasics.AsNoTracking()
            .Where(x => symbols.Contains(x.Symbol) && x.Type == SymbolTypes.Fund)
            .ToDictionaryAsync(x => x.Symbol, ct);
        if (funds.Count == 0) return [];

        // 1) 缓存过期（或从未拉取）的基金：并发请求上游，只做网络 IO、不碰 DbContext
        var syncStates = await db.FundHoldingSyncs.AsNoTracking()
            .Where(x => symbols.Contains(x.FundCode))
            .ToDictionaryAsync(x => x.FundCode, ct);

        var stale = funds.Keys
            .Where(s => !syncStates.TryGetValue(s, out var st) || DateTime.Now - st.FetchedAt > HoldingTtl)
            .ToList();
        if (stale.Count > 0)
            await RefreshHoldingsAsync(stale, ct);

        // 2) 读取持仓与采集状态
        var holdings = await db.FundHoldings.AsNoTracking()
            .Where(x => symbols.Contains(x.FundCode))
            .OrderBy(x => x.Rank)
            .ToListAsync(ct);
        var states = await db.FundHoldingSyncs.AsNoTracking()
            .Where(x => symbols.Contains(x.FundCode))
            .ToDictionaryAsync(x => x.FundCode, ct);

        // 3) 一次批量取所有成分股实时行情（跨基金去重）；
        // 行情源（东财/腾讯）同时不可用时降级为只返回重仓股明细，不让整个接口 500
        var secids = holdings
            .Select(h => (h.StockSymbol, h.SecMarket))
            .Distinct()
            .ToList();
        RealtimeQuoteBatch batch;
        try
        {
            batch = secids.Count > 0
                ? await provider.FetchRealtimeQuotesAsync(secids, ct)
                : new RealtimeQuoteBatch([], QuoteSourceNames.Eastmoney);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "成分股实时行情拉取失败，本次仅返回重仓股明细");
            batch = new RealtimeQuoteBatch([], QuoteSourceNames.None);
        }
        var quotes = batch.Quotes;

        // 4) 按基金聚合：加权平均 = Σ(占净值比例 × 成分股涨跌幅) / Σ(有行情的占净值比例)
        var byFund = holdings.GroupBy(h => h.FundCode).ToDictionary(g => g.Key, g => g.ToList());
        return funds.Values.Select(fund =>
        {
            var rows = byFund.GetValueOrDefault(fund.Symbol) ?? [];
            var items = new List<FundHoldingItemDto>(rows.Count);
            double weightedSum = 0, coveredWeight = 0;

            foreach (var h in rows)
            {
                quotes.TryGetValue($"{h.SecMarket}.{h.StockSymbol}", out var q);
                items.Add(new FundHoldingItemDto(
                    h.StockSymbol, h.StockName, h.SecMarket, Math.Round(h.WeightPct, 2),
                    q is null ? null : Math.Round(q.Price, 2),
                    q is null ? null : Math.Round(q.ChangePct, 2)));
                if (q is null) continue;
                weightedSum += h.WeightPct * q.ChangePct;
                coveredWeight += h.WeightPct;
            }

            states.TryGetValue(fund.Symbol, out var state);
            var estimate = coveredWeight > 0 ? Math.Round(weightedSum / coveredWeight, 2) : (double?)null;
            return new FundEstimateDto(
                fund.Symbol, fund.Name, estimate, Math.Round(coveredWeight, 2),
                state is { ReportDate.Year: > 1 } ? state.ReportDate : null,
                items, batch.Source);
        }).ToList();
    }

    /// <summary>并发拉取指定基金的重仓股并整体替换缓存；上游失败的基金不落缓存，下次查看时重试。</summary>
    private async Task RefreshHoldingsAsync(List<string> fundSymbols, CancellationToken ct)
    {
        var fetched = new ConcurrentDictionary<string, FundHoldingsSnapshot?>();
        await Parallel.ForEachAsync(
            fundSymbols,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrency, CancellationToken = ct },
            async (symbol, token) =>
            {
                try
                {
                    var fundCode = symbol[..^2]; // 去掉 ".F" 后缀
                    fetched[symbol] = await provider.FetchFundHoldingsAsync(fundCode, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "基金 {Symbol} 重仓股拉取失败，稍后重试", symbol);
                }
            });

        if (fetched.IsEmpty) return;

        await RefreshLock.WaitAsync(ct);
        try
        {
            var updated = 0;
            foreach (var (symbol, snapshot) in fetched)
            {
                if (snapshot is null) continue; // 响应异常：不写缓存，避免把"无数据"错误固化 7 天
                var reportDate = snapshot.ReportDate ?? DateTime.MinValue;

                await db.FundHoldings.Where(x => x.FundCode == symbol).ExecuteDeleteAsync(ct);

                var state = await db.FundHoldingSyncs.FirstOrDefaultAsync(x => x.FundCode == symbol, ct);
                if (state is null)
                {
                    state = new FundHoldingSync { FundCode = symbol };
                    db.FundHoldingSyncs.Add(state);
                }
                state.FetchedAt = DateTime.Now;
                state.ReportDate = reportDate;
                state.HoldingCount = snapshot.Items.Count;

                foreach (var h in snapshot.Items)
                {
                    db.FundHoldings.Add(new FundHolding
                    {
                        FundCode = symbol,
                        Rank = h.Rank,
                        StockSymbol = h.Symbol,
                        StockName = h.Name,
                        SecMarket = h.SecMarket,
                        WeightPct = h.WeightPct,
                        ReportDate = reportDate,
                    });
                }
                updated++;
            }

            await db.SaveChangesAsync(ct);
            logger.LogInformation("基金重仓股缓存更新：{Count} 只", updated);
        }
        finally
        {
            RefreshLock.Release();
        }
    }
}
