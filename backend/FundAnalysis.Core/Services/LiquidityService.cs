using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FundAnalysis.Data;
using FundAnalysis.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 央行流动性数据：从东方财富数据中心拉取 Shibor、货币供应量、存款准备金率、LPR 并入库。
/// 数据源 reportName 在浏览器实测获得，均验证可用。
/// </summary>
public class LiquidityService(AppDbContext db, IHttpClientFactory factory, ILogger<LiquidityService> logger)
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";
    private static readonly CultureInfo Cn = CultureInfo.InvariantCulture;

    /// <summary>同步全部流动性数据（定时任务与手动触发入口）。加锁防止并发任务同时写库。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    public async Task SyncAllAsync()
    {
        await SyncShiborAsync(200);
        await SyncMoneySupplyAsync(60);
        await SyncReserveRatioAsync();
        await SyncLprAsync();
        await SyncOmoAsync();
    }

    // ---------- 采集 ----------

    public async Task SyncShiborAsync(int days)
    {
        // 每天约 8 个期限，pageSize 放大保证覆盖
        var rows = await FetchAsync(
            $"reportName=RPT_IMP_INTRESTRATEN&columns=ALL&sortColumns=REPORT_DATE&sortTypes=-1&pageSize={days * 10}&pageNumber=1" +
            "&filter=(CURRENCY_CODE%3D%22CNY%22)", "Shibor");
        if (rows is null) return;

        var existing = await db.ShiborRates
            .Select(x => new { x.Period, x.Date })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Period, x.Date)).ToHashSet());

        var added = 0;
        foreach (var row in rows)
        {
            var period = Str(row, "REPORT_PERIOD");
            var date = Date(row, "REPORT_DATE");
            // CNY 过滤可能含多个市场（在岸/离岸），同日同期限取第一条
            if (period is null || date is null) continue;
            var key = (period, date.Value);
            if (existing.Contains(key)) continue;
            existing.Add(key);
            db.ShiborRates.Add(new ShiborRate
            {
                Period = period,
                Date = date.Value,
                Rate = Num(row, "IR_RATE"),
                ChangeBp = Num(row, "CHANGE_RATE"),
            });
            added++;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("Shibor 同步完成，新增 {Count} 条", added);
    }

    public async Task SyncMoneySupplyAsync(int months)
    {
        var rows = await FetchAsync(
            $"reportName=RPT_ECONOMY_CURRENCY_SUPPLY&columns=ALL&sortColumns=REPORT_DATE&sortTypes=-1&pageSize={months}&pageNumber=1",
            "货币供应量");
        if (rows is null) return;

        var existing = (await db.MoneySupplies.Select(x => x.Month).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var row in rows)
        {
            var date = Date(row, "REPORT_DATE");
            if (date is null || existing.Contains(date.Value)) continue;
            existing.Add(date.Value);
            db.MoneySupplies.Add(new MoneySupply
            {
                Month = date.Value,
                // 东财字段名与实际含义：BASIC_CURRENCY=M2、CURRENCY=M1、FREE_CASH=M0（按数值量级核实）
                M2 = Num(row, "BASIC_CURRENCY"),
                M1 = Num(row, "CURRENCY"),
                M0 = Num(row, "FREE_CASH"),
                M2YoY = Num(row, "BASIC_CURRENCY_SAME"),
                M1YoY = Num(row, "CURRENCY_SAME"),
                M0YoY = Num(row, "FREE_CASH_SAME"),
            });
            added++;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("货币供应量同步完成，新增 {Count} 条", added);
    }

    public async Task SyncReserveRatioAsync()
    {
        var rows = await FetchAsync(
            "reportName=RPT_ECONOMY_DEPOSIT_RESERVE&columns=ALL&sortColumns=REPORT_DATE&sortTypes=-1&pageSize=100&pageNumber=1",
            "存款准备金率");
        if (rows is null) return;

        var existing = (await db.ReserveRatios.Select(x => x.Date).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var row in rows)
        {
            var date = Date(row, "REPORT_DATE");
            // 报表可能含同日多条记录（不同发布批次），取第一条
            if (date is null || existing.Contains(date.Value)) continue;
            existing.Add(date.Value);
            db.ReserveRatios.Add(new ReserveRatio
            {
                Date = date.Value,
                Large = Num(row, "INTEREST_RATE_BA"),
                Small = Num(row, "INTEREST_RATE_SA"),
            });
            added++;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("存款准备金率同步完成，新增 {Count} 条", added);
    }

    public async Task SyncLprAsync()
    {
        var rows = await FetchAsync(
            "reportName=RPTA_WEB_RATE&columns=ALL&sortColumns=TRADE_DATE&sortTypes=-1&pageSize=200&pageNumber=1",
            "LPR");
        if (rows is null) return;

        var existing = (await db.LprRates.Select(x => x.Date).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var row in rows)
        {
            var date = Date(row, "TRADE_DATE");
            if (date is null || existing.Contains(date.Value)) continue;
            existing.Add(date.Value);
            db.LprRates.Add(new LprRate
            {
                Date = date.Value,
                Lpr1Y = Num(row, "LPR1Y"),
                Lpr5Y = Num(row, "LPR5Y"),
            });
            added++;
        }
        await db.SaveChangesAsync();
        logger.LogInformation("LPR 同步完成，新增 {Count} 条", added);
    }

    // ---------- 央行公开市场逆回购（人民银行官网公告） ----------

    // 公告栏目列表页（服务端渲染，每页 20 条，"下一页"的 tagname 属性给出后续分页地址）
    private const string PbocListUrl =
        "https://www.pbc.gov.cn/zhengcehuobisi/125207/125213/125431/125475/index.html";

    private static readonly Regex PbocListLink = new(
        "href=\"(?<path>/zhengcehuobisi/125207/125213/125431/125475/[^\"]+)\"[^>]*>\\s*(?<title>[^<]*公告[^<]*)<",
        RegexOptions.Compiled);
    private static readonly Regex PbocNextPage = new(
        "tagname=\"(?<path>/zhengcehuobisi/[^\"]+)\"[^>]*class=\"pagingNormal\">下一页",
        RegexOptions.Compiled);
    private static readonly Regex PbocUrlDate = new("/(?<d>\\d{14,19})/", RegexOptions.Compiled);
    private static readonly Regex PbocCnDate = new("(?<y>\\d{4})\\s*年\\s*(?<m>\\d{1,2})\\s*月\\s*(?<d>\\d{1,2})\\s*日", RegexOptions.Compiled);
    // 公告正文表格行：期限(7天/3个月) [利率%] 投标量(亿元) 中标量(亿元)。旧版带利率列且数字内可能被标签拆出空格（如"1. 40"）
    private static readonly Regex PbocTableRow = new(
        "(?<n>\\d+)\\s*(?<unit>天|个月)\\s+(?:(?<rate>[\\d.\\s]+?)\\s*%\\s+)?(?<bid>[\\d,]+(?:\\.\\d+)?)\\s*亿元\\s+(?<awd>[\\d,]+(?:\\.\\d+)?)\\s*亿元",
        RegexOptions.Compiled);
    // 正文内联操作："开展了2580亿元隔夜逆回购操作" / "开展了3543亿元逆回购操作"（无期限，期限看表格）
    private static readonly Regex PbocInlineOp = new(
        "开展了\\s*(?<amt>[\\d,]+(?:\\.\\d+)?)\\s*亿元(?<term>\\s*隔夜|\\s*\\d+\\s*天期?|\\s*\\d+\\s*个?月)?\\s*逆回购操作",
        RegexOptions.Compiled);

    /// <summary>
    /// 同步央行公开市场逆回购操作公告。列表按日期倒序增量抓取（比库内最新更早即停），
    /// "操作量为零"的公告也入库（Amount=0）以推进增量游标。到期日 = 操作日 + 期限自然日，
    /// 顺延至下一交易日（交易日历取上证指数日线，超出日历范围按工作日近似）。
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task SyncOmoAsync(int maxPages = 12)
    {
        var latestDate = await db.OmoOperations.MaxAsync(x => (DateTime?)x.Date);
        var calendar = (await db.IndexDailies
            .Where(x => x.Code == "000001")
            .Select(x => x.Date)
            .ToListAsync()).ToHashSet();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9");

        var existing = (await db.OmoOperations.Select(x => new { x.Date, x.Term }).ToListAsync())
            .Select(x => (x.Date, x.Term)).ToHashSet();

        var added = 0;
        var nextUrl = PbocListUrl;
        var calendarMax = calendar.Count > 0 ? calendar.Max() : DateTime.MinValue;
        try
        {
            for (var page = 1; page <= maxPages && nextUrl is not null; page++)
            {
                var html = await client.GetStringAsync(nextUrl);
                var links = PbocListLink.Matches(html);
                if (links.Count == 0)
                {
                    logger.LogWarning("央行公告列表解析失败（第 {Page} 页），可能页面结构变更", page);
                    break;
                }

                var reachedStored = false;
                foreach (Match link in links)
                {
                    var url = "https://www.pbc.gov.cn" + link.Groups["path"].Value;
                    var urlDate = PbocAnnouncementDate(url);
                    if (urlDate is null) continue;
                    // 列表倒序：碰到已入库（或更早）的公告即结束本轮
                    if (latestDate is not null && urlDate <= latestDate)
                    {
                        reachedStored = true;
                        break;
                    }

                    var title = WebUtility.HtmlDecode(link.Groups["title"].Value).Trim();
                    var text = StripHtml(await client.GetStringAsync(url));
                    var ops = ParseOmoAnnouncement(text);
                    if (ops.Count == 0)
                        logger.LogWarning("央行公告 {Title} 未能解析出逆回购操作，跳过", title);

                    foreach (var op in ops)
                    {
                        op.MaturityDate = AdjustToTradingDay(RawMaturity(op.Date, op.Term, op.TermDays), calendar, calendarMax);
                        if (existing.Contains((op.Date, op.Term))) continue;
                        existing.Add((op.Date, op.Term));
                        op.Announcement = title;
                        db.OmoOperations.Add(op);
                        added++;
                    }
                    await db.SaveChangesAsync();
                    await Task.Delay(1200); // 央行站点低频抓取
                }

                if (reachedStored) return;
                nextUrl = PbocNextPage.Match(html) is { Success: true } np
                    ? "https://www.pbc.gov.cn" + np.Groups["path"].Value
                    : null;
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "央行逆回购公告同步失败");
        }
        logger.LogInformation("央行逆回购同步完成，新增 {Count} 条", added);
    }

    /// <summary>逆回购操作查询：近 days 天的投放 + 全部未到期存续（供甘特图与到期统计）。</summary>
    public async Task<OmoResponse> GetOmoAsync(int days)
    {
        var since = DateTime.Today.AddDays(-days);
        var today = DateTime.Today;
        var rows = await db.OmoOperations
            .Where(x => x.Amount > 0 && (x.Date >= since || x.MaturityDate >= today))
            .OrderBy(x => x.Date)
            .ToListAsync();
        return new OmoResponse(rows.Select(r => new OmoItemDto(
            r.Date.ToString("yyyy-MM-dd"),
            r.MaturityDate.ToString("yyyy-MM-dd"),
            r.Term, r.TermDays, r.Amount, r.Rate, r.Announcement)).ToList());
    }

    /// <summary>解析公告正文为操作列表。到期日由调用方统一按交易日历顺延。</summary>
    private static List<OmoOperation> ParseOmoAnnouncement(string text)
    {
        var dateMatch = PbocCnDate.Match(text);
        if (!dateMatch.Success) return [];
        var date = new DateTime(
            int.Parse(dateMatch.Groups["y"].Value),
            int.Parse(dateMatch.Groups["m"].Value),
            int.Parse(dateMatch.Groups["d"].Value));

        var ops = new List<OmoOperation>();
        void Add(string term, int termDays, double amount, double? rate)
        {
            if (ops.Any(o => o.Term == term)) return;
            ops.Add(new OmoOperation { Date = date, Term = term, TermDays = termDays, Amount = amount, Rate = rate });
        }

        // 表格行（可能多行：同日多期限；含"操作量为零"的 0 量行）
        foreach (Match m in PbocTableRow.Matches(text))
        {
            var n = int.Parse(m.Groups["n"].Value);
            var months = m.Groups["unit"].Value == "个月";
            var rateStr = m.Groups["rate"].Success ? m.Groups["rate"].Value.Replace(" ", "") : "";
            var rate = double.TryParse(rateStr, NumberStyles.Float, Cn, out var r) ? r : (double?)null;
            Add(n + (months ? "个月" : "天"), months ? n * 30 : n, Num(m.Groups["awd"].Value), rate);
        }

        // 内联带期限的操作（隔夜等，通常无对应表格行）。期限以"期"结尾时先剥掉再判类型
        foreach (Match m in PbocInlineOp.Matches(text))
        {
            var termStr = m.Groups["term"].Value.Trim().TrimEnd('期');
            if (termStr.Length == 0) continue; // 无期限的旧版表述，期限已在表格行
            var amount = Num(m.Groups["amt"].Value);
            if (amount <= 0) continue;

            var nMatch = Regex.Match(termStr, "\\d+");
            if (termStr == "隔夜") Add("隔夜", 1, amount, null);
            else if (!nMatch.Success) continue;
            else if (termStr.Contains("个月"))
                Add(nMatch.Value + "个月", int.Parse(nMatch.Value) * 30, amount, null);
            else if (termStr.Contains("天"))
                Add(nMatch.Value + "天", int.Parse(nMatch.Value), amount, null);
        }

        // 未解析出任何操作也要留痕（0 量墓碑行），否则增量游标会卡在该公告日反复重抓
        if (ops.Count == 0)
            ops.Add(new OmoOperation { Date = date, Term = "未解析", TermDays = 0, Amount = 0 });
        return ops;
    }

    /// <summary>公告 URL 首段为发布时间（如 /2026102308535522110/），取前 8 位即操作日。</summary>
    private static DateTime? PbocAnnouncementDate(string url)
        => PbocUrlDate.Match(url) is { Success: true } m
           && DateTime.TryParseExact(m.Groups["d"].Value[..8], "yyyyMMdd", Cn, DateTimeStyles.None, out var d)
            ? d : null;

    private static double Num(string s) => double.TryParse(s.Replace(",", ""), NumberStyles.Float, Cn, out var v) ? v : 0;

    private static string StripHtml(string html)
    {
        html = Regex.Replace(html, "<script[\\s\\S]*?</script>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<style[\\s\\S]*?</style>", " ", RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<[^>]+>", " ");
        html = WebUtility.HtmlDecode(html);
        return Regex.Replace(html, "\\s+", " ");
    }

    /// <summary>到期原始日期：按天期限 = 操作日 + N 自然日；按月期限 = 操作日 + N 个自然月。</summary>
    private static DateTime RawMaturity(DateTime date, string term, int termDays)
        => term.EndsWith("个月") && int.TryParse(term[..^2], out var n)
            ? date.AddMonths(n)
            : date.AddDays(termDays);

    private DateTime AdjustToTradingDay(DateTime date, HashSet<DateTime> calendar, DateTime calendarMax)
    {
        while (!IsTradingDay(date, calendar, calendarMax))
            date = date.AddDays(1);
        return date;
    }

    private static bool IsTradingDay(DateTime date, HashSet<DateTime> calendar, DateTime calendarMax)
    {
        // 已知历史以指数日历为准；未来日期（日历未覆盖）按工作日近似，法定节假日顺延可能存在小幅偏差
        if (date <= calendarMax)
            return calendar.Contains(date);
        return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    // ---------- 查询 ----------

    public async Task<LiquidityOverviewDto> GetOverviewAsync()
    {
        var latestShiborDate = await db.ShiborRates.MaxAsync(x => (DateTime?)x.Date);
        var shiborOn = latestShiborDate is null
            ? null
            : await db.ShiborRates.FirstOrDefaultAsync(x => x.Date == latestShiborDate && x.Period.Contains("隔夜"));
        var shibor1W = latestShiborDate is null
            ? null
            : await db.ShiborRates.FirstOrDefaultAsync(x => x.Date == latestShiborDate && x.Period.Contains("1周"));

        var latestSupply = await db.MoneySupplies.OrderByDescending(x => x.Month).FirstOrDefaultAsync();
        var latestLpr = await db.LprRates.OrderByDescending(x => x.Date).FirstOrDefaultAsync();
        var latestReserve = await db.ReserveRatios.OrderByDescending(x => x.Date).FirstOrDefaultAsync();

        return new LiquidityOverviewDto(
            LatestShiborDate: latestShiborDate?.ToString("yyyy-MM-dd") ?? "-",
            ShiborOn: shiborOn?.Rate ?? 0,
            ShiborOnChangeBp: shiborOn?.ChangeBp ?? 0,
            Shibor1W: shibor1W?.Rate ?? 0,
            Shibor1WChangeBp: shibor1W?.ChangeBp ?? 0,
            LatestSupplyMonth: latestSupply?.Month.ToString("yyyy-MM") ?? "-",
            M2YoY: latestSupply?.M2YoY ?? 0,
            M1YoY: latestSupply?.M1YoY ?? 0,
            M0YoY: latestSupply?.M0YoY ?? 0,
            Lpr1Y: latestLpr?.Lpr1Y ?? 0,
            Lpr5Y: latestLpr?.Lpr5Y ?? 0,
            LatestLprDate: latestLpr?.Date.ToString("yyyy-MM-dd") ?? "-",
            ReserveLarge: latestReserve?.Large ?? 0,
            ReserveSmall: latestReserve?.Small ?? 0,
            LatestReserveDate: latestReserve?.Date.ToString("yyyy-MM-dd") ?? "-");
    }

    public async Task<ShiborResponse> GetShiborAsync(int days)
    {
        var since = DateTime.Today.AddDays(-days);
        var rows = await db.ShiborRates
            .Where(x => x.Date >= since)
            .OrderBy(x => x.Date)
            .ToListAsync();

        var byDate = rows
            .GroupBy(x => x.Date)
            .ToDictionary(g => g.Key.ToString("yyyy-MM-dd"), g => g.ToDictionary(p => p.Period, p => p.Rate));

        var points = byDate
            .Select(kv => new ShiborPointDto(
                kv.Key,
                On: kv.Value.GetValueOrDefault("隔夜(O/N)"),
                W1: kv.Value.GetValueOrDefault("1周(1W)"),
                W2: kv.Value.GetValueOrDefault("2周(2W)"),
                M1: kv.Value.GetValueOrDefault("1月(1M)")))
            .ToList();
        return new ShiborResponse(points);
    }

    public async Task<MoneySupplyResponse> GetMoneySupplyAsync(int months)
    {
        var rows = await db.MoneySupplies
            .OrderByDescending(x => x.Month)
            .Take(months)
            .ToListAsync();
        rows.Reverse();
        return new MoneySupplyResponse(rows.Select(r => new MoneySupplyPointDto(
            r.Month.ToString("yyyy-MM"),
            r.M2, r.M1, r.M0, r.M2YoY, r.M1YoY, r.M0YoY)).ToList());
    }

    public async Task<ReserveRatioResponse> GetReserveRatiosAsync()
    {
        var rows = await db.ReserveRatios.OrderByDescending(x => x.Date).ToListAsync();
        var ordered = new List<ReserveRatio>(rows);
        ordered.Reverse();

        ReserveRatio? prev = null;
        var items = new List<ReserveRatioItemDto>();
        foreach (var r in ordered)
        {
            items.Add(new ReserveRatioItemDto(
                r.Date.ToString("yyyy-MM-dd"), r.Large, r.Small,
                prev is null ? null : r.Large - prev.Large));
            prev = r;
        }
        return new ReserveRatioResponse(items);
    }

    public async Task<LprResponse> GetLprAsync()
    {
        var rows = await db.LprRates.OrderByDescending(x => x.Date).Take(60).ToListAsync();
        rows.Reverse();
        return new LprResponse(rows.Select(r => new LprItemDto(
            r.Date.ToString("yyyy-MM-dd"), r.Lpr1Y, r.Lpr5Y)).ToList());
    }

    // ---------- 东财数据中心 API ----------

    private async Task<List<JsonElement>?> FetchAsync(string query, string name)
    {
        try
        {
            var client = factory.CreateClient("eastmoney");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
            using var doc = JsonDocument.Parse(await client.GetStringAsync($"{BaseUrl}?{query}"));

            var root = doc.RootElement;
            if (!root.TryGetProperty("result", out var result) || result.ValueKind == JsonValueKind.Null)
            {
                logger.LogWarning("{Name} 接口返回空 result", name);
                return null;
            }
            if (!result.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return null;
            // Clone() 使 JsonElement 独立于 JsonDocument 的生命周期（方法返回后 doc 被 dispose）
            return data.EnumerateArray().Select(e => e.Clone()).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Name} 同步失败", name);
            return null;
        }
    }

    private static string? Str(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double Num(JsonElement row, string name)
        => row.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static DateTime? Date(JsonElement row, string name)
    {
        var s = Str(row, name);
        return DateTime.TryParse(s, Cn, DateTimeStyles.None, out var d) ? d : null;
    }
}
