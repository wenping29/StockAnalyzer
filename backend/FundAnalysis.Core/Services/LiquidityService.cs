using System.Globalization;
using System.Text.Json;
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
