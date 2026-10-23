using FundAnalysis.Data;
using FundAnalysis.Models;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 查询与分析：多周期 K 线聚合（小时/日/周/月）、区间收益、最大回撤、资金流统计。
/// SQLite 单机量级下按查询时内存聚合即可，数据量增大后可加预聚合表。
/// </summary>
public class AnalysisService(AppDbContext db)
{
    public async Task<StockBasic?> GetBasicAsync(string symbol)
        => await db.StockBasics.FirstOrDefaultAsync(x => x.Symbol == symbol);

    /// <summary>多周期 K 线。period：hour / day / week / month。</summary>
    public async Task<KLineResponse?> GetKLineAsync(string symbol, string period, int limit)
    {
        var basic = await GetBasicAsync(symbol);
        if (basic is null) return null;

        List<KLinePointDto> points;
        if (basic.Type == SymbolTypes.Fund)
        {
            if (period == "hour")
                return null; // 场外基金没有小时数据
            var navs = await db.FundNavs
                .Where(x => x.FundCode == symbol)
                .OrderBy(x => x.Date)
                .ToListAsync();
            points = period switch
            {
                "week" => NavAggregate(navs, WeekKey, limit),
                "month" => NavAggregate(navs, MonthKey, limit),
                _ => navs.Select(n => new KLinePointDto(
                        n.Date.ToString("yyyy-MM-dd"), n.Nav, n.Nav, n.Nav, n.Nav, 0, n.ChangePct))
                    .TakeLast(limit).ToList(),
            };
        }
        else
        {
            points = period switch
            {
                "hour" => await HourKLineAsync(symbol, limit),
                "week" => await DailyAggregateAsync(symbol, WeekKey, limit),
                "month" => await DailyAggregateAsync(symbol, MonthKey, limit),
                _ => await DailyKLineAsync(symbol, limit),
            };
        }

        return new KLineResponse(basic.Symbol, basic.Name, basic.Type, period, points);
    }

    /// <summary>区间收益统计：近1周/近1月/近3月/近6月/今年以来 + 最大回撤。</summary>
    public async Task<PerformanceResponse?> GetPerformanceAsync(string symbol)
    {
        var basic = await GetBasicAsync(symbol);
        if (basic is null) return null;

        // 收盘价序列（股票用日线收盘价，基金用单位净值）
        List<(DateTime Date, double Close)> series;
        if (basic.Type == SymbolTypes.Fund)
        {
            series = await db.FundNavs.Where(x => x.FundCode == symbol)
                .OrderBy(x => x.Date)
                .Select(x => new ValueTuple<DateTime, double>(x.Date, x.Nav))
                .ToListAsync();
        }
        else
        {
            series = await db.StockDailies.Where(x => x.Symbol == symbol)
                .OrderBy(x => x.Date)
                .Select(x => new ValueTuple<DateTime, double>(x.Date, x.Close))
                .ToListAsync();
        }

        if (series.Count == 0)
            return new PerformanceResponse(symbol, basic.Name, basic.Type, 0, "-", [], 0);

        var closes = series.Select(s => s.Close).ToList();
        var last = series[^1];

        double? ReturnPct(int tradingDays)
            => closes.Count > tradingDays ? (closes[^1] / closes[closes.Count - 1 - tradingDays] - 1) * 100 : null;

        var ytdStart = series.Where(s => s.Date.Year == DateTime.Now.Year).ToList();
        double? ytdPct = ytdStart.Count >= 2 ? (closes[^1] / ytdStart[0].Close - 1) * 100 : null;

        // 最大回撤：遍历历史最高点的最大跌幅
        var peak = closes[0];
        var maxDrawdown = 0.0;
        foreach (var c in closes)
        {
            if (c > peak) peak = c;
            var dd = (1 - c / peak) * 100;
            if (dd > maxDrawdown) maxDrawdown = dd;
        }

        var windows = new List<PerfWindowDto>
        {
            new("1w", "近1周", ReturnPct(5)),
            new("1m", "近1月", ReturnPct(21)),
            new("3m", "近3月", ReturnPct(63)),
            new("6m", "近6月", ReturnPct(126)),
            new("ytd", "今年以来", ytdPct),
        };

        return new PerformanceResponse(symbol, basic.Name, basic.Type,
            last.Close, last.Date.ToString("yyyy-MM-dd"), windows, Math.Round(maxDrawdown, 2));
    }

    /// <summary>每日资金流序列 + 区间主力净流入合计。仅股票/ETF 有资金流数据。</summary>
    public async Task<MoneyFlowResponse?> GetMoneyFlowAsync(string symbol, int days)
    {
        var basic = await GetBasicAsync(symbol);
        if (basic is null || basic.Type == SymbolTypes.Fund) return null;

        var rows = await db.MoneyFlowDailies
            .Where(x => x.Symbol == symbol)
            .OrderByDescending(x => x.Date)
            .Take(days)
            .ToListAsync();
        rows.Reverse();

        var items = rows.Select(r => new MoneyFlowItemDto(
            r.Date.ToString("yyyy-MM-dd"),
            r.MainNet, r.LargeNet, r.MidNet, r.SmallNet)).ToList();

        return new MoneyFlowResponse(symbol, items, rows.Sum(r => r.MainNet));
    }

    /// <summary>自选列表概览：最新价、涨跌幅、近1月收益、近5日主力净流入。</summary>
    public async Task<List<WatchlistItemDto>> GetWatchlistOverviewAsync(string userId)
    {
        var symbols = await db.WatchlistItems
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => x.Symbol)
            .ToListAsync();

        var basics = await db.StockBasics.Where(x => symbols.Contains(x.Symbol)).ToDictionaryAsync(x => x.Symbol);
        var result = new List<WatchlistItemDto>();

        foreach (var symbol in symbols)
        {
            if (!basics.TryGetValue(symbol, out var basic)) continue;

            if (basic.Type == SymbolTypes.Fund)
            {
                var navs = await db.FundNavs.Where(x => x.FundCode == symbol)
                    .OrderByDescending(x => x.Date).Take(22).ToListAsync();
                navs.Reverse();
                result.Add(new WatchlistItemDto(symbol, basic.Name, basic.Type,
                    navs.Count > 0 ? navs[^1].Nav : null,
                    navs.Count > 0 ? navs[^1].ChangePct : null,
                    MonthReturn(navs.Select(n => n.Nav).ToList()),
                    null));
                continue;
            }

            var dailies = await db.StockDailies.Where(x => x.Symbol == symbol)
                .OrderByDescending(x => x.Date).Take(22).ToListAsync();
            dailies.Reverse();

            var last5Main = await db.MoneyFlowDailies.Where(x => x.Symbol == symbol)
                .OrderByDescending(x => x.Date).Take(5).SumAsync(x => (double?)x.MainNet);

            result.Add(new WatchlistItemDto(symbol, basic.Name, basic.Type,
                dailies.Count > 0 ? dailies[^1].Close : null,
                dailies.Count > 0 ? dailies[^1].ChangePct : null,
                MonthReturn(dailies.Select(d => d.Close).ToList()),
                last5Main));
        }
        return result;
    }

    private async Task<List<KLinePointDto>> DailyKLineAsync(string symbol, int limit)
    {
        // TakeLast 无法翻译成 SQL：倒序取 N 条后在内存反转
        var rows = await db.StockDailies
            .Where(x => x.Symbol == symbol)
            .OrderByDescending(x => x.Date)
            .Take(limit)
            .ToListAsync();
        rows.Reverse();
        return rows.Select(r => new KLinePointDto(
            r.Date.ToString("yyyy-MM-dd"), r.Open, r.High, r.Low, r.Close, r.Volume, r.ChangePct)).ToList();
    }

    private async Task<List<KLinePointDto>> DailyAggregateAsync(string symbol, Func<DateTime, string> keyFunc, int limit)
    {
        var rows = await db.StockDailies
            .Where(x => x.Symbol == symbol)
            .OrderBy(x => x.Date)
            .ToListAsync();

        var points = rows
            .GroupBy(r => keyFunc(r.Date))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var bars = g.OrderBy(r => r.Date).ToList();
                return new KLinePointDto(g.Key,
                    bars[0].Open,
                    bars.Max(b => b.High),
                    bars.Min(b => b.Low),
                    bars[^1].Close,
                    bars.Sum(b => b.Volume), 0);
            })
            .TakeLast(limit)
            .ToList();
        return FillChangePct(points);
    }

    private async Task<List<KLinePointDto>> HourKLineAsync(string symbol, int limit)
    {
        var minutes = await db.StockMinutes
            .Where(x => x.Symbol == symbol)
            .OrderBy(x => x.Time)
            .ToListAsync();

        var points = minutes
            .GroupBy(m => m.Time.ToString("yyyy-MM-dd HH:00"))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var bars = g.OrderBy(m => m.Time).ToList();
                return new KLinePointDto(g.Key,
                    bars[0].Open,
                    bars.Max(b => b.High),
                    bars.Min(b => b.Low),
                    bars[^1].Close,
                    bars.Sum(b => b.Volume), 0);
            })
            .TakeLast(limit)
            .ToList();
        return FillChangePct(points);
    }

    private static List<KLinePointDto> NavAggregate(List<FundNav> navs, Func<DateTime, string> keyFunc, int limit)
    {
        var points = navs
            .GroupBy(n => keyFunc(n.Date))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var bars = g.OrderBy(n => n.Date).ToList();
                return new KLinePointDto(g.Key,
                    bars[0].Nav,
                    bars.Max(b => b.Nav),
                    bars.Min(b => b.Nav),
                    bars[^1].Nav, 0, 0);
            })
            .TakeLast(limit)
            .ToList();
        return FillChangePct(points);
    }

    private static double? MonthReturn(List<double> closes)
        => closes.Count > 21 ? (closes[^1] / closes[closes.Count - 1 - 21] - 1) * 100 : null;

    /// <summary>本周一（作为周分组的 key，避免跨年周计算问题）。</summary>
    private static string WeekKey(DateTime date)
        => date.AddDays(-((int)date.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd");

    private static string MonthKey(DateTime date) => date.ToString("yyyy-MM");

    private static List<KLinePointDto> FillChangePct(List<KLinePointDto> points)
    {
        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1].Close;
            if (prev != 0)
                points[i] = points[i] with { ChangePct = Math.Round((points[i].Close / prev - 1) * 100, 2) };
        }
        return points;
    }
}
