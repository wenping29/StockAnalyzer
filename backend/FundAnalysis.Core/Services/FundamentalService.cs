using System.Globalization;
using System.Text.Json;
using FundAnalysis.Data;
using FundAnalysis.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 股票基本面数据（选股用）：
///   估值 = 东财估值表 RPT_VALUEANALYSIS_DET（最新交易日全市场：PE/PB/市值/收盘价/行业）
///   业绩 = 东财业绩报表 RPT_LICO_FN_CPD（最新报告期全量：ROE/营收同比/净利同比/毛利率）
/// 两表均走 datacenter 域名（未被风控），翻页间隔 1.5 秒低速拉取。仅保留估值表中的 A 股代码。
/// </summary>
public class FundamentalService(AppDbContext db, IHttpClientFactory factory, ILogger<FundamentalService> logger)
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";
    private const int PageSize = 500;
    private static readonly CultureInfo Cn = CultureInfo.InvariantCulture;

    // ---------- 采集 ----------

    [DisableConcurrentExecution(timeoutInSeconds: 900)]
    [AutomaticRetry(Attempts = 10)]
    public async Task SyncAllAsync()
    {
        // 1. 估值表（A 股全市场）
        var valuation = await FetchValuationAsync();

        // 2. 业绩表（全量，含三板；入库时只取估值表已有的 A 股代码）
        var earnings = await FetchEarningsAsync();

        // 3. 合并 upsert
        var existing = await db.StockFundamentals.ToDictionaryAsync(x => x.Symbol);
        var upserted = 0;

        foreach (var (symbol, v) in valuation)
        {
            if (!existing.TryGetValue(symbol, out var row))
            {
                row = new StockFundamental { Symbol = symbol };
                db.StockFundamentals.Add(row);
                existing[symbol] = row;
            }

            row.Name = v.Name;
            row.Industry = v.Industry;
            row.ClosePrice = v.ClosePrice;
            row.ChangePct = v.ChangePct is { } c ? Math.Round(c, 2) : null;
            row.PeTtm = v.PeTtm;
            row.PbMrq = v.PbMrq;
            row.PsTtm = v.PsTtm;
            row.MarketCap = v.MarketCap;

            if (earnings.TryGetValue(symbol, out var e))
            {
                row.ReportDate = e.ReportDate;
                row.Revenue = e.Revenue;
                row.RevenueYoY = e.RevenueYoY;
                row.NetProfit = e.NetProfit;
                row.NetProfitYoY = e.NetProfitYoY;
                row.Roe = e.Roe;
                row.GrossMargin = e.GrossMargin;
                row.NetMargin = e.Revenue > 0 ? Math.Round(e.NetProfit / e.Revenue * 100, 2) : null;
            }
            row.UpdatedAt = DateTime.Now;
            upserted++;
        }

        await db.SaveChangesAsync();
        logger.LogInformation("基本面数据同步完成：估值 {Valuation} 只，业绩匹配 {Earnings} 只，共更新 {Count} 只",
            valuation.Count, valuation.Count(k => earnings.ContainsKey(k.Key)), upserted);
    }

    private record ValuationRow(string Name, string? Industry, double ClosePrice, double? ChangePct, double? PeTtm, double? PbMrq, double? PsTtm, double? MarketCap);
    private record EarningsRow(DateTime? ReportDate, double Revenue, double? RevenueYoY, double NetProfit, double? NetProfitYoY, double? Roe, double? GrossMargin);

    private async Task<Dictionary<string, ValuationRow>> FetchValuationAsync()
    {
        var result = new Dictionary<string, ValuationRow>();

        // 探测最新交易日（columns=ALL 无 filter 会被 9701 拒，需显式列出字段）
        var probe = await FetchPageAsync(
            "reportName=RPT_VALUEANALYSIS_DET&columns=SECURITY_CODE,TRADE_DATE&sortColumns=TRADE_DATE&sortTypes=-1&pageSize=1&pageNumber=1", "估值表");
        var latestDate = probe?.Count > 0 ? Str(probe[0], "TRADE_DATE")?.Split(' ')[0] : null;
        if (latestDate is null)
        {
            logger.LogWarning("估值表无数据");
            return result;
        }

        for (var page = 1; page <= 30; page++)
        {
            var rows = await FetchPageWithRetryAsync(
                $"reportName=RPT_VALUEANALYSIS_DET&columns=ALL&sortColumns=SECURITY_CODE&sortTypes=1&pageSize={PageSize}&pageNumber={page}" +
                $"&filter=(TRADE_DATE%3D%27{latestDate}%27)", "估值表");
            if (rows is null || rows.Count == 0) break;

            foreach (var row in rows)
            {
                var symbol = Str(row, "SECURITY_CODE");
                if (string.IsNullOrWhiteSpace(symbol) || result.ContainsKey(symbol)) continue;
                result[symbol] = new ValuationRow(
                    Str(row, "SECURITY_NAME_ABBR") ?? symbol,
                    Str(row, "BOARD_NAME"),
                    Num(row, "CLOSE_PRICE"),
                    NumOrNull(row, "CHANGE_RATE"),
                    NumOrNull(row, "PE_TTM"),
                    NumOrNull(row, "PB_MRQ"),
                    NumOrNull(row, "PS_TTM"),
                    NumOrNull(row, "TOTAL_MARKET_CAP") is { } cap ? Math.Round(cap / 1e8, 2) : null);
            }

            if (rows.Count < PageSize) break;
            await Task.Delay(1500);
        }
        return result;
    }

    private async Task<Dictionary<string, EarningsRow>> FetchEarningsAsync()
    {
        var result = new Dictionary<string, EarningsRow>();

        // 报告期候选：最近 5 个季度末倒序（三季报等披露期内多数公司未披露，记录数 < 3000 时回退上一期）
        var candidates = Enumerable.Range(0, 5)
            .Select(i => DateTime.Today.AddMonths(-3 * i))
            .Select(d => new DateTime(d.Year, ((d.Month - 1) / 3) * 3 + 1, 1).AddMonths(3).AddDays(-1))
            .Select(d => d.ToString("yyyy-MM-dd"))
            .Distinct()
            .ToList();

        string? latestReport = null;
        foreach (var d in candidates)
        {
            var count = await FetchCountAsync(
                $"reportName=RPT_LICO_FN_CPD&columns=SECURITY_CODE&pageSize=1&pageNumber=1&filter=(REPORTDATE%3D%27{d}%27)", "业绩表");
            logger.LogInformation("报告期 {Date} 记录数 {Count}", d, count);
            if (count >= 3000)
            {
                latestReport = d;
                break;
            }
        }

        if (latestReport is null)
        {
            logger.LogWarning("业绩表未找到披露完整的报告期");
            return result;
        }

        for (var page = 1; page <= 40; page++)
        {
            var rows = await FetchPageWithRetryAsync(
                $"reportName=RPT_LICO_FN_CPD&columns=ALL&sortColumns=SECURITY_CODE&sortTypes=1&pageSize={PageSize}&pageNumber={page}" +
                $"&filter=(REPORTDATE%3D%27{latestReport}%27)", "业绩表");
            if (rows is null || rows.Count == 0) break;

            foreach (var row in rows)
            {
                var symbol = Str(row, "SECURITY_CODE");
                if (string.IsNullOrWhiteSpace(symbol) || result.ContainsKey(symbol)) continue;
                var revenue = Num(row, "TOTAL_OPERATE_INCOME") / 1e8;
                result[symbol] = new EarningsRow(
                    Date(row, "REPORTDATE"),
                    Math.Round(revenue, 2),
                    NumOrNull(row, "YSTZ"),
                    Math.Round(Num(row, "PARENT_NETPROFIT") / 1e8, 2),
                    NumOrNull(row, "SJLTZ"),
                    NumOrNull(row, "WEIGHTAVG_ROE"),
                    NumOrNull(row, "XSMLL"));
            }

            if (rows.Count < PageSize) break;
            await Task.Delay(1500);
        }
        return result;
    }

    private async Task<List<JsonElement>?> FetchPageAsync(string query, string name)
    {
        try
        {
            var client = factory.CreateClient("eastmoney");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"{BaseUrl}?{query}"));

            if (!doc.RootElement.TryGetProperty("result", out var res) || res.ValueKind == JsonValueKind.Null)
                return null;
            if (!res.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;
            return data.EnumerateArray().Select(e => e.Clone()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Name} 拉取失败：{Query}", name, query[^60..]);
            return null;
        }
    }

    /// <summary>翻页拉取：datacenter 偶发 9701 限流（HTTP 200 + result null），退避重试 3 次。</summary>
    private async Task<List<JsonElement>?> FetchPageWithRetryAsync(string query, string name)
    {
        for (var attempt = 1; ; attempt++)
        {
            var rows = await FetchPageAsync(query, name);
            if (rows is not null) return rows;
            if (attempt >= 3)
            {
                logger.LogWarning("{Name} 翻页连续失败（疑似限流），第 {Attempt} 次后停止：{Query}",
                    name, attempt, query[^60..]);
                return null;
            }
            await Task.Delay(4000 * attempt);
        }
    }

    /// <summary>拉取报表记录总数（result.count）。</summary>
    private async Task<int> FetchCountAsync(string query, string name)
    {
        try
        {
            var client = factory.CreateClient("eastmoney");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"{BaseUrl}?{query}"));
            if (doc.RootElement.TryGetProperty("result", out var res) &&
                res.ValueKind != JsonValueKind.Null &&
                res.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number)
                return c.GetInt32();
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Name} count 查询失败", name);
            return 0;
        }
    }

    // ---------- 筛选查询 ----------

    public async Task<(int Total, List<StockFundamental> Items)> ScreenAsync(
        string? keyword, string? industry,
        double? minRoe, double? maxRoe,
        double? minPe, double? maxPe,
        double? maxPb,
        double? minNetMargin, double? minGrossMargin,
        double? minRevenueYoy, double? minNetProfitYoY,
        double? minMarketCap,
        string sort, bool desc,
        int page, int pageSize)
    {
        var q = db.StockFundamentals.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(x => x.Symbol.Contains(k) || x.Name.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(industry))
            q = q.Where(x => x.Industry == industry);
        if (minRoe is > 0) q = q.Where(x => x.Roe >= minRoe);
        if (maxRoe is > 0) q = q.Where(x => x.Roe != null && x.Roe <= maxRoe);
        if (minPe is > 0) q = q.Where(x => x.PeTtm != null && x.PeTtm >= minPe);
        if (maxPe is > 0) q = q.Where(x => x.PeTtm != null && x.PeTtm <= maxPe);
        if (maxPb is > 0) q = q.Where(x => x.PbMrq != null && x.PbMrq <= maxPb);
        if (minNetMargin is > 0) q = q.Where(x => x.NetMargin >= minNetMargin);
        if (minGrossMargin is > 0) q = q.Where(x => x.GrossMargin >= minGrossMargin);
        if (minRevenueYoy is not null) q = q.Where(x => x.RevenueYoY >= minRevenueYoy);
        if (minNetProfitYoY is not null) q = q.Where(x => x.NetProfitYoY >= minNetProfitYoY);
        if (minMarketCap is > 0) q = q.Where(x => x.MarketCap >= minMarketCap);

        q = (sort, desc) switch
        {
            ("pe", false) => q.OrderBy(x => x.PeTtm ?? double.MaxValue),
            ("pe", true) => q.OrderByDescending(x => x.PeTtm ?? double.MinValue),
            ("pb", false) => q.OrderBy(x => x.PbMrq ?? double.MaxValue),
            ("roe", true) => q.OrderByDescending(x => x.Roe ?? double.MinValue),
            ("netMargin", true) => q.OrderByDescending(x => x.NetMargin ?? double.MinValue),
            ("revenueYoy", true) => q.OrderByDescending(x => x.RevenueYoY ?? double.MinValue),
            ("netProfitYoy", true) => q.OrderByDescending(x => x.NetProfitYoY ?? double.MinValue),
            ("marketCap", true) => q.OrderByDescending(x => x.MarketCap ?? double.MinValue),
            ("marketCap", false) => q.OrderBy(x => x.MarketCap ?? double.MaxValue),
            ("closePrice", true) => q.OrderByDescending(x => x.ClosePrice),
            ("closePrice", false) => q.OrderBy(x => x.ClosePrice),
            _ => q.OrderByDescending(x => x.MarketCap ?? double.MinValue),
        };

        var total = await q.CountAsync();
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return (total, items);
    }

    /// <summary>行业列表（筛选下拉用）。</summary>
    public async Task<List<string>> GetIndustriesAsync()
        => await db.StockFundamentals
            .Where(x => x.Industry != null)
            .Select(x => x.Industry!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    // ---------- 个股浏览（按板块） ----------

    /// <summary>板块定义：按代码前缀划分 A 股市场板块。</summary>
    public static readonly (string Key, string Name, string[] Prefixes)[] Boards =
    [
        ("sh", "沪市主板", ["60"]),
        ("star", "科创板", ["688", "689"]),
        ("sz", "深市主板", ["00"]),
        ("chinext", "创业板", ["30"]),
        ("bse", "北交所", ["43", "83", "87", "88", "92"]),
    ];

    /// <summary>各板块股票数量统计。</summary>
    public async Task<Dictionary<string, int>> GetBoardStatsAsync()
    {
        var all = await db.StockFundamentals.Select(x => x.Symbol).ToListAsync();
        var stats = Boards.ToDictionary(b => b.Key, _ => 0);
        stats["all"] = all.Count;
        foreach (var symbol in all)
        {
            foreach (var (key, _, prefixes) in Boards)
            {
                if (prefixes.Any(p => symbol.StartsWith(p)))
                {
                    stats[key]++;
                    break;
                }
            }
        }
        return stats;
    }

    /// <summary>个股列表：板块 + 关键字 + 行业 + 排序分页。</summary>
    public async Task<(int Total, List<StockFundamental> Items)> GetStocksAsync(
        string? board, string? keyword, string? industry, string sort, bool desc, int page, int pageSize)
    {
        var q = db.StockFundamentals.AsNoTracking().AsQueryable();

        // 板块过滤（代码前缀）
        if (!string.IsNullOrEmpty(board) && board != "all")
        {
            var boardDef = Boards.FirstOrDefault(b => b.Key == board);
            if (boardDef.Prefixes is { Length: > 0 })
            {
                q = q.Where(x => boardDef.Prefixes.Any(p => x.Symbol.StartsWith(p)));
            }
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(x => x.Symbol.Contains(k) || x.Name.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(industry))
            q = q.Where(x => x.Industry == industry);

        q = (sort, desc) switch
        {
            ("changePct", true) => q.OrderByDescending(x => x.ChangePct ?? double.MinValue),
            ("changePct", false) => q.OrderBy(x => x.ChangePct ?? double.MaxValue),
            ("pe", false) => q.OrderBy(x => x.PeTtm ?? double.MaxValue),
            ("roe", true) => q.OrderByDescending(x => x.Roe ?? double.MinValue),
            ("revenueYoy", true) => q.OrderByDescending(x => x.RevenueYoY ?? double.MinValue),
            ("closePrice", true) => q.OrderByDescending(x => x.ClosePrice),
            ("closePrice", false) => q.OrderBy(x => x.ClosePrice),
            ("marketCap", false) => q.OrderBy(x => x.MarketCap ?? double.MaxValue),
            _ => q.OrderByDescending(x => x.MarketCap ?? double.MinValue),
        };

        var total = await q.CountAsync();
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (total, items);
    }

    // ---------- JSON 解析 ----------

    private static string? Str(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double Num(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static double? NumOrNull(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;

    private static DateTime? Date(JsonElement row, string name)
    {
        var s = Str(row, name);
        return DateTime.TryParse(s, Cn, DateTimeStyles.None, out var d) ? d : null;
    }
}
