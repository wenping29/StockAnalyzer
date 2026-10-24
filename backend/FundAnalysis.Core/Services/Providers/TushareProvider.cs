using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using FundAnalysis.Models;

namespace FundAnalysis.Services.Providers;

/// <summary>
/// Tushare Pro 数据源适配器（备用数据源）。在 appsettings 配置 Tushare:Token 并将 Quote:Provider 设为 tushare 启用。
/// 接口：POST http://api.tushare.pro {api_name, token, params, fields}。
/// 局限：daily 为不复权价（fqt 参数忽略）；搜索基于本地缓存的股票/基金全量清单。
/// </summary>
public class TushareProvider(IHttpClientFactory factory, IConfiguration config, ILogger<TushareProvider> logger) : IQuoteProvider
{
    private const string ApiUrl = "http://api.tushare.pro";

    private string Token => config["Tushare:Token"] is { Length: > 0 } t
        ? t
        : throw new InvalidOperationException("Tushare:Token 未配置，请在 appsettings.json 或环境变量中设置");

    // 进程内清单缓存（搜索与全量列表共用，1 小时过期）
    private static readonly ConcurrentDictionary<string, (DateTime CachedAt, List<StockBasic> Items)> ListCache = new();

    // ---------- 公共请求 ----------

    private async Task<JsonElement?> CallAsync(string apiName, Dictionary<string, string> parameters, string fields, CancellationToken ct)
    {
        try
        {
            var client = factory.CreateClient("tushare");
            var payload = JsonSerializer.Serialize(new { api_name = apiName, token = Token, @params = parameters, fields });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync(ApiUrl, content, ct);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : -1;
            if (code != 0)
            {
                var msg = root.TryGetProperty("msg", out var m) ? m.GetString() : null;
                logger.LogWarning("Tushare {Api} 调用失败 code={Code} msg={Msg}", apiName, code, msg);
                return null;
            }
            return root.GetProperty("data");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tushare {Api} 请求异常", apiName);
            return null;
        }
    }

    private static List<StockBasic> MapItems(JsonElement data, string defaultType)
    {
        var list = new List<StockBasic>();
        if (!data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;
        if (!data.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Array)
            return list;
        var fields = fieldsEl.EnumerateArray().Select(f => f.GetString() ?? "").ToArray();

        foreach (var row in items.EnumerateArray())
        {
            var values = row.EnumerateArray().ToArray();
            string Get(string name) => Array.IndexOf(fields, name) is { } i && i >= 0 && i < values.Length && values[i].ValueKind == JsonValueKind.String
                ? values[i].GetString() ?? "" : "";
            double NumOf(string name) => Array.IndexOf(fields, name) is { } i2 && i2 >= 0 && i2 < values.Length && values[i2].ValueKind == JsonValueKind.Number
                ? values[i2].GetDouble() : 0;

            var tsCode = Get("ts_code");
            if (string.IsNullOrWhiteSpace(tsCode)) continue;
            var symbol = tsCode.Split('.')[0];
            var suffix = tsCode.Contains('.') ? tsCode[(tsCode.IndexOf('.') + 1)..] : "";

            list.Add(new StockBasic
            {
                Symbol = symbol,
                Name = Get("name"),
                SecMarket = suffix == "SH" ? 1 : 0,
                Type = defaultType,
                UpdatedAt = DateTime.Now,
            });
        }
        return list;
    }

    private async Task<List<StockBasic>> GetStockListAsync(CancellationToken ct)
    {
        if (ListCache.TryGetValue("stocks", out var cached) && (DateTime.Now - cached.CachedAt).TotalHours < 1)
            return cached.Items;

        var data = await CallAsync("stock_basic", new Dictionary<string, string> { ["list_status"] = "L" },
            "ts_code,name,market", ct);
        var items = data is null ? [] : MapItems(data.Value, SymbolTypes.Stock);
        ListCache["stocks"] = (DateTime.Now, items);
        return items;
    }

    private async Task<List<StockBasic>> GetFundListAsync(string market, string type, CancellationToken ct)
    {
        var key = $"fund:{market}";
        if (ListCache.TryGetValue(key, out var cached) && (DateTime.Now - cached.CachedAt).TotalHours < 1)
            return cached.Items;

        var data = await CallAsync("fund_basic", new Dictionary<string, string> { ["market"] = market }, "ts_code,name", ct);
        var items = data is null ? [] : MapItems(data.Value, type);
        ListCache[key] = (DateTime.Now, items);
        return items;
    }

    // ---------- IQuoteProvider ----------

    public async Task<List<StockBasic>> FetchSuggestAsync(string keyword, CancellationToken ct = default)
    {
        var k = keyword.Trim();
        var stocks = await GetStockListAsync(ct);
        var funds = await GetFundListAsync("E", SymbolTypes.Etf, ct);
        var otc = await GetFundListAsync("O", SymbolTypes.Fund, ct);

        return stocks.Concat(funds).Concat(otc)
            .Where(x => x.Symbol == k || x.Name.Contains(k, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
    }

    public Task<List<StockBasic>> FetchStockListAsync(CancellationToken ct = default)
        => GetStockListAsync(ct);

    public async Task<List<KLineItem>> FetchKLineAsync(string symbol, int secMarket, int klt, int limit, int fqt = 1, CancellationToken ct = default)
    {
        if (klt != 101)
            throw new NotSupportedException("Tushare 备用数据源仅支持日线（K 线分钟线请使用东方财富数据源）");

        var suffix = secMarket == 1 ? "SH" : "SZ";
        var data = await CallAsync("daily",
            new Dictionary<string, string> { ["ts_code"] = $"{symbol}.{suffix}", ["limit"] = limit.ToString() },
            "trade_date,open,high,low,close,vol,amount,pct_chg", ct);

        var list = new List<KLineItem>();
        if (data is null || !data.Value.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in items.EnumerateArray())
        {
            var arr = row.EnumerateArray().ToArray();
            if (arr.Length < 8) continue;
            if (!DateTime.TryParse(arr[0].GetString(), out var date)) continue;
            list.Add(new KLineItem(
                Time: date,
                Open: arr[1].GetDouble(),
                High: arr[2].GetDouble(),
                Low: arr[3].GetDouble(),
                Close: arr[4].GetDouble(),
                Volume: arr[5].GetDouble(),
                Amount: arr[6].GetDouble(),
                ChangePct: arr[7].GetDouble()));
        }
        return list.OrderBy(x => x.Time).ToList();
    }

    public async Task<List<MoneyFlowItem>> FetchMoneyFlowAsync(string symbol, int secMarket, int days, CancellationToken ct = default)
    {
        var suffix = secMarket == 1 ? "SH" : "SZ";
        var data = await CallAsync("moneyflow",
            new Dictionary<string, string> { ["ts_code"] = $"{symbol}.{suffix}", ["limit"] = days.ToString() },
            "trade_date,buy_lg_amount,sell_lg_amount,buy_elg_amount,sell_elg_amount,buy_md_amount,sell_md_amount,buy_sm_amount,sell_sm_amount", ct);

        var list = new List<MoneyFlowItem>();
        if (data is null || !data.Value.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in items.EnumerateArray())
        {
            var arr = row.EnumerateArray().ToArray();
            if (arr.Length < 9 || !DateTime.TryParse(arr[0].GetString(), out var date)) continue;
            // 单位：万元 → 元。主力 = 大单 + 超大单
            double Num(int i) => arr[i].ValueKind == JsonValueKind.Number ? arr[i].GetDouble() * 1e4 : 0;
            var large = Num(1) - Num(2) + Num(3) - Num(4);
            list.Add(new MoneyFlowItem(
                Date: date,
                MainNet: large,
                LargeNet: large,
                MidNet: Num(5) - Num(6),
                SmallNet: Num(7) - Num(8)));
        }
        return list.OrderBy(x => x.Date).ToList();
    }

    public async Task<List<FundNavItem>> FetchFundNavAsync(string fundCode, int pageSize, CancellationToken ct = default)
    {
        var data = await CallAsync("fund_nav",
            new Dictionary<string, string> { ["ts_code"] = fundCode, ["limit"] = pageSize.ToString() },
            "nav_date,unit_nav,accum_nav", ct);

        var list = new List<FundNavItem>();
        if (data is null || !data.Value.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in items.EnumerateArray())
        {
            var arr = row.EnumerateArray().ToArray();
            if (arr.Length < 3 || !DateTime.TryParse(arr[0].GetString(), out var date)) continue;
            var prev = list.LastOrDefault();
            list.Add(new FundNavItem(
                Date: date,
                Nav: arr[1].ValueKind == JsonValueKind.Number ? arr[1].GetDouble() : 0,
                AccNav: arr[2].ValueKind == JsonValueKind.Number ? arr[2].GetDouble() : 0,
                ChangePct: prev is { Nav: > 0 } ? Math.Round((arr[1].GetDouble() / prev.Nav - 1) * 100, 2) : 0));
        }
        return list.OrderBy(x => x.Date).ToList();
    }
}
