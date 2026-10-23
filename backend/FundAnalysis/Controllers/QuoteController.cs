using FundAnalysis.Models;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using FundAnalysis.Data;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/quote")]
public class QuoteController(
    AppDbContext db, AnalysisService analysis, IQuoteProvider provider, IMemoryCache cache, HsgtService hsgt) : ControllerBase
{
    /// <summary>
    /// 人民币汇率趋势（离岸 USDCNH，东财 secid 133）。数值上升 = 人民币贬值，下降 = 升值。
    /// </summary>
    [Authorize]
    [HttpGet("fx-rate")]
    public async Task<IActionResult> FxRate([FromQuery] int days = 90)
    {
        days = Math.Clamp(days, 30, 365);
        const string cacheKey = "fx-rate";
        if (cache.TryGetValue(cacheKey, out FxRateDto? cached) && cached is not null)
            return Ok(cached);

        var bars = await provider.FetchKLineAsync("USDCNH", 133, 101, days);
        if (bars.Count == 0)
            return Ok(new FxRateDto(0, "-", 0, null, []));

        var points = bars.Select(b => new FxPointDto(b.Time.ToString("yyyy-MM-dd"), b.Close)).ToList();
        var last = bars[^1];
        var dto = new FxRateDto(
            Latest: last.Close,
            LatestDate: last.Time.ToString("yyyy-MM-dd"),
            DayChangePct: last.ChangePct,
            MonthChangePct: bars.Count >= 21 && bars[^21].Close > 0
                ? Math.Round((last.Close / bars[^21].Close - 1) * 100, 2)
                : null,
            Points: points);

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

        var bars = await provider.FetchKLineAsync("US10Y", 171, 101, days, fqt: 0);
        if (bars.Count == 0)
            return Ok(new Us10yDto(0, "-", 0, null, []));

        var points = bars.Select(b => new FxPointDto(b.Time.ToString("yyyy-MM-dd"), b.Close)).ToList();
        var last = bars[^1];

        double? dayChangeBp = bars.Count >= 2 ? Math.Round((last.Close - bars[^2].Close) * 10000, 1) : null;
        double? monthChangeBp = bars.Count >= 21 ? Math.Round((last.Close - bars[^21].Close) * 10000, 1) : null;

        var dto = new Us10yDto(
            Latest: last.Close,
            LatestDate: last.Time.ToString("yyyy-MM-dd"),
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

        // 三条期限并行拉取
        var y5 = await provider.FetchKLineAsync("CN5Y", 171, 101, days, fqt: 0);
        var y10 = await provider.FetchKLineAsync("CN10Y", 171, 101, days, fqt: 0);
        var y30 = await provider.FetchKLineAsync("CN30Y", 171, 101, days, fqt: 0);
        if (y10.Count == 0)
            return Ok(new CnBondDto(0, "-", 0, null, []));

        var y5Map = y5.ToDictionary(b => b.Time.Date, b => b.Close);
        var y30Map = y30.ToDictionary(b => b.Time.Date, b => b.Close);

        var points = y10
            .OrderBy(b => b.Time)
            .Select(b => new CnBondPointDto(
                b.Time.ToString("yyyy-MM-dd"),
                y5Map.GetValueOrDefault(b.Time.Date),
                b.Close,
                y30Map.GetValueOrDefault(b.Time.Date)))
            .ToList();

        var ordered = y10.OrderByDescending(b => b.Time).ToList();
        var last = ordered[0];
        var monthAgo = ordered.Count > 20 ? ordered[20] : null;

        var dto = new CnBondDto(
            Latest10y: last.Close,
            LatestDate: last.Time.ToString("yyyy-MM-dd"),
            DayChangeBp: ordered.Count > 1 ? Math.Round((last.Close - ordered[1].Close) * 10000, 1) : 0,
            MonthChangeBp: monthAgo is not null ? Math.Round((last.Close - monthAgo.Close) * 10000, 1) : null,
            Points: points);

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return Ok(dto);
    }

    /// <summary>指数页面清单（A股宽基 / 风格 / 跨境），含 30 日走势与当日成交额，缓存 60 秒。</summary>
    private static readonly (string Code, string Name, string Group, int SecMarket)[] IndexList =
    [
        ("000001", "上证指数", "A股宽基", 1),
        ("399001", "深证成指", "A股宽基", 0),
        ("000300", "沪深300", "A股宽基", 1),
        ("000905", "中证500", "A股宽基", 1),
        ("000852", "中证1000", "A股宽基", 1),
        ("000016", "上证50", "A股宽基", 1),
        ("399330", "深证100", "A股宽基", 0),
        ("399006", "创业板指", "A股宽基", 0),
        ("000688", "科创50", "A股宽基", 1),
        ("899050", "北证50", "A股宽基", 0),
        ("000922", "中证红利", "风格与主题", 1),
        ("HSI", "恒生指数", "跨境", 100),
        ("NDX", "纳斯达克", "跨境", 100),
        ("DJIA", "道琼斯", "跨境", 100),
        ("SPX", "标普500", "跨境", 100),
    ];

    [Authorize]
    [HttpGet("indices")]
    public async Task<IActionResult> Indices()
    {
        const string cacheKey = "indices-overview";
        if (cache.TryGetValue(cacheKey, out IndicesResponse? cached) && cached is not null)
            return Ok(cached);

        var result = new List<IndexItemDto>();
        foreach (var idx in IndexList)
        {
            var bars = await provider.FetchKLineAsync(idx.Code, idx.SecMarket, 101, 30, fqt: 0);
            if (bars.Count == 0) continue;
            var last = bars[^1];
            result.Add(new IndexItemDto(
                idx.Code, idx.Name, idx.Group,
                last.Close, last.ChangePct,
                Math.Round(last.Amount / 1e8, 1),
                bars.Select(b => b.Close).ToList()));
        }

        var dto = new IndicesResponse(result);
        cache.Set(cacheKey, dto, TimeSpan.FromSeconds(60));
        return Ok(dto);
    }

    /// <summary>指数 K 线（day/week/month，内存聚合，不落库），缓存 5 分钟。</summary>
    [Authorize]
    [HttpGet("index-kline")]
    public async Task<IActionResult> IndexKline(
        [FromQuery] string code, [FromQuery] int market = 1,
        [FromQuery] string period = "day", [FromQuery] int limit = 120)
    {
        if (period is not ("day" or "week" or "month"))
            return BadRequest(new { message = "period 只支持 day/week/month" });

        var cacheKey = $"index-kline:{code}:{period}";
        if (cache.TryGetValue(cacheKey, out KLineResponse? cached) && cached is not null)
            return Ok(cached);

        limit = Math.Clamp(limit, 30, 300);
        // 周/月线从更长的日线序列聚合
        var fetchCount = period == "day" ? limit : 600;
        var bars = await provider.FetchKLineAsync(code, market, 101, fetchCount, fqt: 0);
        if (bars.Count == 0)
            return NotFound(new { message = $"未找到指数 {code}" });

        List<KLinePointDto> points;
        if (period == "day")
        {
            points = bars
                .Select(b => new KLinePointDto(
                    b.Time.ToString("yyyy-MM-dd"), b.Open, b.High, b.Low, b.Close, b.Volume, b.ChangePct))
                .ToList();
        }
        else
        {
            Func<DateTime, string> key = period == "week"
                ? d => d.AddDays(-((int)d.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd") // 本周一
                : d => d.ToString("yyyy-MM");
            points = bars
                .GroupBy(b => key(b.Time))
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var sorted = g.OrderBy(b => b.Time).ToList();
                    return new KLinePointDto(
                        g.Key,
                        sorted[0].Open,
                        sorted.Max(b => b.High),
                        sorted.Min(b => b.Low),
                        sorted[^1].Close,
                        sorted.Sum(b => b.Volume), 0);
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

        var meta = IndexList.FirstOrDefault(i => i.Code == code);
        var response = new KLineResponse(code, meta.Name, "Index", period, points);

        cache.Set(cacheKey, response, TimeSpan.FromMinutes(5));
        return Ok(response);
    }

    /// <summary>
    /// 沪深港通资金（北上/南下）：北向成交额（净流入 2024-08 起停止披露）、南向净流入/流出，近 N 日趋势。
    /// </summary>
    [Authorize]
    [HttpGet("hsgt-flow")]
    public async Task<IActionResult> HsgtFlow([FromQuery] int days = 30)
        => Ok(await hsgt.GetFlowAsync(days));

    /// <summary>市场指数快照（上证/深成/创业板/沪深300），含近 30 日走势，缓存 60 秒。</summary>
    [Authorize]
    [HttpGet("market-overview")]
    public async Task<IActionResult> MarketOverview()
    {
        const string cacheKey = "market-overview";
        if (cache.TryGetValue(cacheKey, out List<MarketIndexDto>? cached) && cached is not null)
            return Ok(cached);

        // (代码, 名称, 所属市场板块, 东财 secid 市场前缀)
        (string Code, string Name, string Market, int SecMarket)[] indices =
        [
            ("000001", "上证指数", "沪市", 1),
            ("399001", "深证成指", "深市", 0),
            ("399006", "创业板指", "创业板", 0),
            ("000688", "科创50", "科创板", 1),
            ("899050", "北证50", "北交所", 0),
            ("000300", "沪深300", "跨市场", 1),
        ];

        var result = new List<MarketIndexDto>();
        foreach (var idx in indices)
        {
            var bars = await provider.FetchKLineAsync(idx.Code, idx.SecMarket, 101, 30);
            if (bars.Count == 0) continue;
            var last = bars[^1];
            result.Add(new MarketIndexDto(idx.Code, idx.Name, idx.Market, last.Close, last.ChangePct,
                bars.Select(b => b.Close).ToList()));
        }

        cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));
        return Ok(result);
    }

    /// <summary>
    /// 两市成交量趋势（近 N 个交易日）。沪市取上证指数成交额、深市取深证综指成交额（覆盖全部个股），
    /// 单位亿元，缓存 60 秒。
    /// </summary>
    [Authorize]
    [HttpGet("market-volume")]
    public async Task<IActionResult> MarketVolume([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 10, 120);
        const string cacheKey = "market-volume";
        if (cache.TryGetValue(cacheKey, out MarketVolumeDto? cached) && cached is not null)
            return Ok(cached);

        // 沪市总额：上证指数(1.000001)；深市总额：深证综指(0.399106)
        var shBars = await provider.FetchKLineAsync("000001", 1, 101, days);
        var szBars = await provider.FetchKLineAsync("399106", 0, 101, days);
        if (shBars.Count == 0 || szBars.Count == 0)
            return Ok(new MarketVolumeDto(0, 0, null, []));

        // K 线 Amount 单位为元，转亿元
        var sh = shBars.ToDictionary(b => b.Time.Date, b => b.Amount / 1e8);
        var sz = szBars.ToDictionary(b => b.Time.Date, b => b.Amount / 1e8);

        var points = sh.Keys
            .Where(d => sz.ContainsKey(d))
            .OrderBy(d => d)
            .Select(d => new MarketVolumePointDto(
                d.ToString("yyyy-MM-dd"),
                Math.Round(sh[d], 1),
                Math.Round(sz[d], 1),
                Math.Round(sh[d] + sz[d], 1)))
            .ToList();

        var dto = new MarketVolumeDto(
            TodayAmount: points.Count > 0 ? points[^1].Total : 0,
            YesterdayAmount: points.Count > 1 ? points[^2].Total : 0,
            ChangePct: points.Count > 1 && points[^2].Total > 0
                ? Math.Round((points[^1].Total / points[^2].Total - 1) * 100, 2)
                : null,
            Points: points);

        cache.Set(cacheKey, dto, TimeSpan.FromSeconds(60));
        return Ok(dto);
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
