using FundAnalysis.Models;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FundAnalysis.Data;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/quote")]
public class QuoteController(
    AppDbContext db, AnalysisService analysis, IQuoteProvider provider, IMemoryCache cache, HsgtService hsgt,
    ILogger<QuoteController> logger) : ControllerBase
{
    // 第三方接口现拉已从本控制器摘除：fx 汇率 / 美债 / 国债 / 指数日内分时均改读采集库
    // （MarketIndicator / IndexIntraday），由独立采集器 FundAnalysis.Collector 定时入库；
    // Web 请求路径不再依赖东财可用性，第三方故障最多导致数据陈旧，不会 500。
    // 仅 /api/quote/search 仍实时转发东财搜索接口（searchapi 本机可达，且搜索结果无法预采集）。

    /// <summary>
    /// 人民币汇率趋势（离岸 USDCNH，读 MarketIndicator 采集表，无实时外呼）。
    /// 数值上升 = 人民币贬值，下降 = 升值。
    /// </summary>
    [Authorize]
    [HttpGet("fx-rate")]
    public async Task<IActionResult> FxRate([FromQuery] int days = 90)
    {
        days = Math.Clamp(days, 30, 365);
        const string cacheKey = "fx-rate";
        if (cache.TryGetValue(cacheKey, out FxRateDto? cached) && cached is not null)
            return Ok(cached);

        var rows = await db.MarketIndicators
            .Where(x => x.Symbol == "USDCNH")
            .OrderBy(x => x.Date)
            .ToListAsync();
        if (rows.Count == 0)
            return Ok(new FxRateDto(0, "-", 0, null, [], 0, 0));

        var cutoff = DateTime.Now.Date.AddDays(-days);
        var recent = rows.Where(x => x.Date >= cutoff).ToList();
        if (recent.Count == 0) recent = rows;

        var points = recent.Select(r => new FxPointDto(r.Date.ToString("yyyy-MM-dd"), r.Value)).ToList();
        var last = recent[^1];
        var prev = recent.Count >= 2 ? recent[^2].Value : last.Value;
        var monthAgo = recent.Count >= 21 ? recent[^21].Value : recent[0].Value;
        var dayChangePct = prev != 0 ? Math.Round((last.Value / prev - 1) * 100, 2) : 0;
        double? monthChangePct = monthAgo != 0 ? Math.Round((last.Value / monthAgo - 1) * 100, 2) : null;

        // 在岸中间价（采集器入库的 USDCNYC 序列）
        double parityLatest = 0, parityChangePct = 0;
        var parity = await db.MarketIndicators
            .Where(x => x.Symbol == "USDCNYC")
            .OrderByDescending(x => x.Date)
            .FirstOrDefaultAsync();
        if (parity is not null)
        {
            parityLatest = parity.Value;
            parityChangePct = parity.ChangePct ?? 0;
        }

        var dto = new FxRateDto(
            Latest: last.Value,
            LatestDate: last.Date.ToString("yyyy-MM-dd"),
            DayChangePct: dayChangePct,
            MonthChangePct: monthChangePct,
            Points: points,
            ParityLatest: parityLatest,
            ParityChangePct: parityChangePct);

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>
    /// 十年期美债收益率（东财 secid 171.US10Y，需用不复权口径）。DayChangeBp 单位为基点。
    /// </summary>
    [Authorize]
    [HttpGet("us10y")]
    public async Task<IActionResult> Us10y([FromQuery] int days = 90)
    {
        days = Math.Clamp(days, 30, 365);
        const string cacheKey = "us10y";
        if (cache.TryGetValue(cacheKey, out Us10yDto? cached) && cached is not null)
            return Ok(cached);

        var rows = await db.MarketIndicators
            .Where(x => x.Symbol == "US10Y")
            .OrderBy(x => x.Date)
            .ToListAsync();
        if (rows.Count == 0)
            return Ok(new Us10yDto(0, "-", 0, null, []));

        var cutoff = DateTime.Now.Date.AddDays(-days);
        var recent = rows.Where(x => x.Date >= cutoff).ToList();
        if (recent.Count == 0) recent = rows;

        var points = recent.Select(r => new FxPointDto(r.Date.ToString("yyyy-MM-dd"), r.Value)).ToList();
        var last = recent[^1];

        double? dayChangeBp = recent.Count >= 2 ? Math.Round((last.Value - recent[^2].Value) * 10000, 1) : null;
        double? monthChangeBp = recent.Count >= 21 ? Math.Round((last.Value - recent[^21].Value) * 10000, 1) : null;

        var dto = new Us10yDto(
            Latest: last.Value,
            LatestDate: last.Date.ToString("yyyy-MM-dd"),
            DayChangeBp: dayChangeBp ?? 0,
            MonthChangeBp: monthChangeBp,
            Points: points);

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>
    /// 人民币中长期国债收益率（东财 secid 171，5 年 / 10 年 / 30 年期，不复权口径）。
    /// 以 10 年期为主指标，DayChangeBp 单位为基点。
    /// </summary>
    [Authorize]
    [HttpGet("cn-bond")]
    public async Task<IActionResult> CnBond([FromQuery] int days = 90)
    {
        days = Math.Clamp(days, 30, 365);
        const string cacheKey = "cn-bond";
        if (cache.TryGetValue(cacheKey, out CnBondDto? cached) && cached is not null)
            return Ok(cached);

        var y10 = await db.MarketIndicators.Where(x => x.Symbol == "CN10Y").OrderBy(x => x.Date).ToListAsync();
        if (y10.Count == 0)
            return Ok(new CnBondDto(0, "-", 0, null, []));

        var y5 = await db.MarketIndicators.Where(x => x.Symbol == "CN5Y").OrderBy(x => x.Date).ToListAsync();
        var y30 = await db.MarketIndicators.Where(x => x.Symbol == "CN30Y").OrderBy(x => x.Date).ToListAsync();

        var cutoff = DateTime.Now.Date.AddDays(-days);
        var y10r = y10.Where(x => x.Date >= cutoff).ToList();
        if (y10r.Count == 0) y10r = y10;
        var y5r = y5.Where(x => x.Date >= cutoff).ToList();
        var y30r = y30.Where(x => x.Date >= cutoff).ToList();
        var y5Map = y5r.ToDictionary(x => x.Date, x => x.Value);
        var y30Map = y30r.ToDictionary(x => x.Date, x => x.Value);

        var points = y10r
            .OrderBy(x => x.Date)
            .Select(b => new CnBondPointDto(
                b.Date.ToString("yyyy-MM-dd"),
                y5Map.GetValueOrDefault(b.Date),
                b.Value,
                y30Map.GetValueOrDefault(b.Date)))
            .ToList();

        var ordered = y10r.OrderByDescending(x => x.Date).ToList();
        var last = ordered[0];
        var monthAgo = ordered.Count > 20 ? ordered[20] : ordered[^1];

        var dto = new CnBondDto(
            Latest10y: last.Value,
            LatestDate: last.Date.ToString("yyyy-MM-dd"),
            DayChangeBp: ordered.Count > 1 ? Math.Round((last.Value - ordered[1].Value) * 10000, 1) : 0,
            MonthChangeBp: Math.Round((last.Value - monthAgo.Value) * 10000, 1),
            Points: points);

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>指数页面清单（A股宽基 / 风格 / 跨境），数据来自 IndexDaily 采集表（无实时外呼）。</summary>
    [Authorize]
    [HttpGet("indices")]
    public async Task<IActionResult> Indices()
    {
        var result = new List<IndexItemDto>();
        foreach (var (code, name, group, market) in MarketIndices.List)
        {
            // 最近 31 根日线：最新快照 + 30 日迷你走势
            var rows = await db.IndexDailies
                .Where(x => x.Code == code)
                .OrderByDescending(x => x.Date)
                .Take(31)
                .ToListAsync();
            if (rows.Count == 0) continue;
            rows.Reverse();

            var last = rows[^1];
            result.Add(new IndexItemDto(
                code, name, group,
                last.Close, last.ChangePct,
                Math.Round(last.Amount / 1e8, 1),
                rows.Select(r => r.Close).ToList()));
        }

        return Ok(new IndicesResponse(result));
    }

    /// <summary>指数 K 线（day/week/month，查 IndexDaily 表内存聚合，无实时外呼）。</summary>
    [Authorize]
    [HttpGet("index-kline")]
    public async Task<IActionResult> IndexKline(
        [FromQuery] string code, [FromQuery] string period = "day", [FromQuery] int limit = 120)
    {
        if (period is not ("day" or "week" or "month"))
            return BadRequest(new { message = "period 只支持 day/week/month" });
        limit = Math.Clamp(limit, 30, 500);

        var meta = MarketIndices.List.FirstOrDefault(i => i.Code == code);
        if (meta == default) return NotFound(new { message = $"未知指数 {code}" });

        var rows = await db.IndexDailies
            .Where(x => x.Code == code)
            .OrderBy(x => x.Date)
            .ToListAsync();
        if (rows.Count == 0)
            return NotFound(new { message = $"指数 {code} 暂无数据，请等待采集任务完成" });

        List<KLinePointDto> points;
        if (period == "day")
        {
            points = rows
                .Select(r => new KLinePointDto(
                    r.Date.ToString("yyyy-MM-dd"), r.Open, r.High, r.Low, r.Close, r.Volume, r.ChangePct))
                .ToList();
        }
        else
        {
            Func<DateTime, string> key = period == "week"
                ? d => d.AddDays(-((int)d.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd") // 本周一
                : d => d.ToString("yyyy-MM");
            points = rows
                .GroupBy(r => key(r.Date))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var sorted = g.OrderBy(r => r.Date).ToList();
                    return new KLinePointDto(
                        g.Key,
                        sorted[0].Open,
                        sorted.Max(r => r.High),
                        sorted.Min(r => r.Low),
                        sorted[^1].Close,
                        sorted.Sum(r => r.Volume), 0);
                })
                .ToList();
        }

        // 截取最近 limit 根并补涨跌幅
        if (points.Count > limit) points = points.Skip(points.Count - limit).ToList();
        for (var i = 1; i < points.Count; i++)
        {
            var prev = points[i - 1].Close;
            if (prev != 0)
                points[i] = points[i] with { ChangePct = Math.Round((points[i].Close / prev - 1) * 100, 2) };
        }

        return Ok(new KLineResponse(code, meta.Name, "Index", period, points));
    }

    /// <summary>
    /// 指数日内分时走势（东财实时 trends2，按指数缓存 1 分钟）。
    /// PrevClose 取 IndexDaily 上一交易日收盘价，作为分时图昨收基准线与断轴判断依据；
    /// 库内无历史数据（采集未完成）时 PrevClose 为 0，前端退化为不断轴模式。
    /// </summary>
    [Authorize]
    [HttpGet("index-trend")]
    public async Task<IActionResult> IndexTrend([FromQuery] string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "缺少参数 code" });

        var meta = MarketIndices.List.FirstOrDefault(i => i.Code == code);
        if (meta == default) return NotFound(new { message = $"未知指数 {code}" });

        var cacheKey = $"index-trend-{code}";
        if (cache.TryGetValue(cacheKey, out IndexTrendResponse? cached) && cached is not null)
            return Ok(cached);

        // 昨收：IndexDaily 最近两个交易日（升序取第二条）；库空时为 0
        var rows = await db.IndexDailies
            .Where(x => x.Code == code)
            .OrderByDescending(x => x.Date)
            .Take(2)
            .ToListAsync();
        var prevClose = rows.Count >= 2 ? rows[1].Close : rows.Count == 1 ? rows[0].Close : 0;

        // 最新交易日分时（库内无数据则提示暂无，不现拉第三方）
        var latestDate = await db.IndexIntradays
            .Where(x => x.Code == code)
            .Select(x => x.Time.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .FirstOrDefaultAsync();
        if (latestDate == default)
            return Ok(new IndexTrendResponse(code, meta.Name, "", prevClose, []));

        var trend = await db.IndexIntradays
            .Where(x => x.Code == code && x.Time.Date == latestDate)
            .OrderBy(x => x.Time)
            .ToListAsync();

        if (trend.Count == 0)
            return Ok(new IndexTrendResponse(code, meta.Name, "", prevClose, []));

        var dto = new IndexTrendResponse(
            code, meta.Name, latestDate.ToString("yyyy-MM-dd"), prevClose,
            trend.Select(t => new IndexTrendPointDto(
                t.Time.ToString("HH:mm"), t.Price, t.Volume, t.Avg)).ToList());

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(1));
        return Ok(dto);
    }

    /// <summary>
    /// 沪深港通资金（北上/南下）：北向成交额（净流入 2024-08 起停止披露）、南向净流入/流出，近 N 日趋势。
    /// </summary>
    [Authorize]
    [HttpGet("hsgt-flow")]
    public async Task<IActionResult> HsgtFlow([FromQuery] int days = 30)
        => Ok(await hsgt.GetFlowAsync(days));

    /// <summary>市场指数快照（上证/深成/创业板/沪深300），优先读 IndexDaily 采集表，库空时实时拉取。</summary>
    [Authorize]
    [HttpGet("market-overview")]
    public async Task<IActionResult> MarketOverview()
    {
        var result = new List<MarketIndexDto>();

        // 首页六大板块（按展示顺序）：沪市、深市、跨市场、创业板、科创板、北交所
        var homeCodes = new[] { "000001", "399001", "000300", "399006", "000688", "899050" };
        foreach (var (code, name, market, secMarket) in MarketIndices.List.Where(i => homeCodes.Contains(i.Code)))
        {
            // 优先查库（指数由定时任务低频采集，避免实时外呼触发东财风控）
            var rows = await db.IndexDailies
                .Where(x => x.Code == code)
                .OrderByDescending(x => x.Date)
                .Take(31)
                .ToListAsync();
            if (rows.Count > 0)
            {
                rows.Reverse();
                var last = rows[^1];
                result.Add(new MarketIndexDto(code, name, market, last.Close, last.ChangePct,
                    rows.Select(r => r.Close).ToList()));
                continue;
            }

            // 库无数据时跳过（采集未完成），不再实时外拉第三方
        }

        return Ok(result);
    }

    /// <summary>
    /// 两市成交量（近 30 个交易日）。沪市取上证指数成交额、深市取深证综指成交额，
    /// 数据来自 IndexDaily 采集表（库空时实时兜底），单位亿元。
    /// </summary>
    [Authorize]
    [HttpGet("market-volume")]
    public async Task<IActionResult> MarketVolume()
    {
        var dto = await BuildVolumeSeriesAsync("day", 30);
        if (dto.Points.Count == 0)
        {
            // 库无数据（采集未完成）时返回空，不再实时外拉第三方
        }

        return Ok(new MarketVolumeDto(
            dto.Points.Count > 0 ? dto.Points[^1].Total : 0,
            dto.Points.Count > 1 ? dto.Points[^2].Total : 0,
            dto.Points.Count > 1 && dto.Points[^2].Total > 0
                ? Math.Round((dto.Points[^1].Total / dto.Points[^2].Total - 1) * 100, 2)
                : null,
            dto.Points));
    }

    /// <summary>
    /// 两市成交量趋势（日/周/月/季度/年粒度），由 IndexDaily 日线聚合，单位亿元。
    /// </summary>
    [Authorize]
    [HttpGet("market-volume-series")]
    public async Task<IActionResult> MarketVolumeSeries([FromQuery] string granularity = "day")
    {
        if (granularity is not ("day" or "week" or "month" or "quarter" or "year"))
            return BadRequest(new { message = "granularity 只支持 day/week/month/quarter/year" });

        var limit = granularity switch
        {
            "day" => 250,    // 约一年
            "week" => 260,   // 约 5 年
            "month" => 84,   // 约 7 年
            "quarter" => 28, // 约 7 年
            _ => 10,         // year
        };

        var dto = await BuildVolumeSeriesAsync(granularity, limit);
        return Ok(new MarketVolumeDto(
            dto.Points.Count > 0 ? dto.Points[^1].Total : 0,
            dto.Points.Count > 1 ? dto.Points[^2].Total : 0,
            dto.Points.Count > 1 && dto.Points[^2].Total > 0
                ? Math.Round((dto.Points[^1].Total / dto.Points[^2].Total - 1) * 100, 2)
                : null,
            dto.Points));
    }

    /// <summary>从 IndexDaily 聚合两市成交量序列（沪=上证指数 000001，深=深证综指 399106）。</summary>
    private async Task<MarketVolumeDto> BuildVolumeSeriesAsync(string granularity, int limit)
    {
        var rows = await db.IndexDailies
            .Where(x => x.Code == "000001" || x.Code == "399106")
            .OrderBy(x => x.Date)
            .ToListAsync();

        var sh = rows.Where(r => r.Code == "000001").ToDictionary(r => r.Date, r => r.Amount / 1e8);
        var sz = rows.Where(r => r.Code == "399106").ToDictionary(r => r.Date, r => r.Amount / 1e8);

        var points = sh.Keys
            .Union(sz.Keys)
            .OrderBy(d => d)
            .GroupBy(d => DateGroupKeys.Key(d, granularity))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var days = g.ToList();
                // 日粒度取当日值；更长粒度取区间求和（成交量是流量）。缺失一侧按 0（数据未采集时不留白）
                var shv = granularity == "day"
                    ? days.Where(d => sh.ContainsKey(d)).Select(d => sh[d]).FirstOrDefault()
                    : days.Where(d => sh.ContainsKey(d)).Sum(d => sh[d]);
                var szv = granularity == "day"
                    ? days.Where(d => sz.ContainsKey(d)).Select(d => sz[d]).FirstOrDefault()
                    : days.Where(d => sz.ContainsKey(d)).Sum(d => sz[d]);
                return new MarketVolumePointDto(
                    g.Key, Math.Round(shv, 1), Math.Round(szv, 1), Math.Round(shv + szv, 1));
            })
            .ToList();

        if (points.Count > limit) points = points.Skip(points.Count - limit).ToList();
        return new MarketVolumeDto(0, 0, null, points);
    }
    /// <summary>标的搜索：转发东财搜索接口（代码/拼音/名称），结果自动入库供后续采集使用。</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(Array.Empty<SearchItemDto>());

        var items = await provider.FetchSuggestAsync(keyword.Trim());

        // 搜索结果入库（搜索即注册），这样添加自选时标的已存在
        var symbols = items.Select(x => x.Symbol).ToList();
        var existing = await db.StockBasics.Where(x => symbols.Contains(x.Symbol)).ToDictionaryAsync(x => x.Symbol);
        foreach (var item in items)
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
            }
        }
        await db.SaveChangesAsync();

        return Ok(items.Take(Math.Clamp(limit, 1, 20)).Select(x => new SearchItemDto(
            x.Symbol, x.Name, x.Type,
            x.Type == SymbolTypes.Fund ? "场外基金" : x.SecMarket == 1 ? "沪" : "深")));
    }

    /// <summary>标的基础信息。</summary>
    [HttpGet("basics")]
    public async Task<IActionResult> Basics([FromQuery] string symbol)
    {
        var basic = await analysis.GetBasicAsync(symbol);
        if (basic is null) return NotFound(new { message = $"未找到标的 {symbol}" });
        return Ok(new { basic.Symbol, basic.Name, basic.Type, market = basic.SecMarket == 1 ? "沪" : "深/北" });
    }

    /// <summary>多周期 K 线。小时线（分钟数据）需要 VIP 或 Admin 角色。</summary>
    [Authorize]
    [HttpGet("kline")]
    public async Task<IActionResult> KLine(
        [FromQuery] string symbol,
        [FromQuery] string period = "day",
        [FromQuery] int limit = 120)
    {
        if (period is not ("hour" or "day" or "week" or "month"))
            return BadRequest(new { message = "period 只支持 hour/day/week/month" });

        if (period == "hour" && !User.IsInRole("VIP") && !User.IsInRole("Admin"))
            return Forbid();

        var result = await analysis.GetKLineAsync(symbol, period, Math.Clamp(limit, 1, 800));
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol} 或该周期无数据" });
        return Ok(result);
    }

    /// <summary>区间收益统计（近1周/1月/3月/6月/今年以来 + 最大回撤）。</summary>
    [Authorize]
    [HttpGet("performance")]
    public async Task<IActionResult> Performance([FromQuery] string symbol)
    {
        var result = await analysis.GetPerformanceAsync(symbol);
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol}" });
        return Ok(result);
    }

    /// <summary>每日资金流明细（主力/大单/中单/小单）。需要 VIP 或 Admin 角色。</summary>
    [Authorize(Roles = "VIP,Admin")]
    [HttpGet("moneyflow")]
    public async Task<IActionResult> MoneyFlow([FromQuery] string symbol, [FromQuery] int days = 30)
    {
        var result = await analysis.GetMoneyFlowAsync(symbol, Math.Clamp(days, 1, 120));
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol} 的资金流数据（场外基金无资金流）" });
        return Ok(result);
    }
}
