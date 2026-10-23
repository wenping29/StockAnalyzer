using System.Globalization;
using System.Text.Json;
using FundAnalysis.Models;

namespace FundAnalysis.Services.Providers;

/// <summary>
/// 东方财富公开接口实现。接口非官方文档，字段顺序以下面的 fields 参数为准：
/// K线 kline 行: 日期,开,收,高,低,量,额,振幅,涨跌幅,涨跌额,换手率
/// 资金流 fflow 行: 日期,主力净额,小单净额,中单净额,大单净额,...
/// </summary>
public class EastmoneyProvider(IHttpClientFactory factory) : IQuoteProvider
{
    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

    private static readonly CultureInfo Cn = CultureInfo.InvariantCulture;

    private HttpClient NewClient()
    {
        var client = factory.CreateClient("eastmoney");
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return client;
    }

    public async Task<List<StockBasic>> FetchStockListAsync(CancellationToken ct = default)
    {
        var result = new List<StockBasic>();

        // A 股（沪深主板/创业板/科创板/北交所）
        const string stockFs = "m:0+t:6,m:0+t:80,m:1+t:2,m:1+t:23,m:0+t:81+s:2048";
        await FetchCListAsync(stockFs, SymbolTypes.Stock, result, ct);

        // 场内 ETF
        const string etfFs = "b:MK0021,b:MK0022,b:MK0023,b:MK0024";
        await FetchCListAsync(etfFs, SymbolTypes.Etf, result, ct);

        return result;
    }

    private async Task FetchCListAsync(string fs, string type, List<StockBasic> result, CancellationToken ct)
    {
        var url = $"https://push2.eastmoney.com/api/qt/clist/get?pn=1&pz=7000&po=1&np=1&fltt=2&invt=2&fid=f12&fs={Uri.EscapeDataString(fs)}&fields=f12,f13,f14";
        using var client = NewClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            return;
        if (!data.TryGetProperty("diff", out var diff) || diff.ValueKind != JsonValueKind.Array)
            return;

        foreach (var row in diff.EnumerateArray())
        {
            var symbol = GetString(row, "f12");
            if (string.IsNullOrWhiteSpace(symbol)) continue;
            result.Add(new StockBasic
            {
                Symbol = symbol,
                Name = GetString(row, "f14") ?? symbol,
                SecMarket = GetInt(row, "f13"),
                Type = type,
                UpdatedAt = DateTime.Now,
            });
        }
    }

    public async Task<List<KLineItem>> FetchKLineAsync(string symbol, int secMarket, int klt, int limit, CancellationToken ct = default)
    {
        var url = $"https://push2his.eastmoney.com/api/qt/stock/kline/get?secid={secMarket}.{symbol}&klt={klt}&fqt=1&lmt={limit}&end=20500101&fields1=f1,f2,f3,f4,f5,f6&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61";
        using var client = NewClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
        var list = new List<KLineItem>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            return list;
        if (!data.TryGetProperty("klines", out var klines) || klines.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in klines.EnumerateArray())
        {
            // 日期,开,收,高,低,量(手),额(元),振幅,涨跌幅,涨跌额,换手率
            var parts = el.GetString()!.Split(',');
            if (parts.Length < 7) continue;
            if (!DateTime.TryParse(parts[0], Cn, DateTimeStyles.None, out var time)) continue;
            list.Add(new KLineItem(
                Time: time,
                Open: Num(parts[1]),
                High: Num(parts[3]),
                Low: Num(parts[4]),
                Close: Num(parts[2]),
                Volume: Num(parts[5]),
                Amount: Num(parts[6]),
                ChangePct: parts.Length > 8 ? Num(parts[8]) : 0));
        }
        return list;
    }

    public async Task<List<MoneyFlowItem>> FetchMoneyFlowAsync(string symbol, int secMarket, int days, CancellationToken ct = default)
    {
        var url = $"https://push2.eastmoney.com/api/qt/stock/fflow/kline/get?lmt={days}&klt=101&secid={secMarket}.{symbol}&fields1=f1,f2,f3,f7&fields2=f51,f52,f53,f54,f55,f56,f57,f58,f59,f60,f61,f62,f63,f64,f65";
        using var client = NewClient();
        using var doc = JsonDocument.Parse(await client.GetStringAsync(url, ct));
        var list = new List<MoneyFlowItem>();
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            return list;
        if (!data.TryGetProperty("klines", out var klines) || klines.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var el in klines.EnumerateArray())
        {
            // 日期,主力净额,小单净额,中单净额,大单净额,...
            var parts = el.GetString()!.Split(',');
            if (parts.Length < 5) continue;
            if (!DateTime.TryParse(parts[0], Cn, DateTimeStyles.None, out var date)) continue;
            list.Add(new MoneyFlowItem(
                Date: date,
                MainNet: Num(parts[1]),
                SmallNet: Num(parts[2]),
                MidNet: Num(parts[3]),
                LargeNet: Num(parts[4])));
        }
        return list;
    }

    public async Task<List<FundNavItem>> FetchFundNavAsync(string fundCode, int pageSize, CancellationToken ct = default)
    {
        var url = $"https://api.fund.eastmoney.com/f10/lsjz?fundCode={fundCode}&pageIndex=1&pageSize={pageSize}&startDate=&endDate=";
        using var client = NewClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        // 该接口要求携带 Referer，否则返回空
        request.Headers.Referrer = new Uri($"https://fundf10.eastmoney.com/jjjz_{fundCode}.html");
        using var response = await client.SendAsync(request, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        var list = new List<FundNavItem>();
        if (!doc.RootElement.TryGetProperty("Data", out var data) || data.ValueKind == JsonValueKind.Null)
            return list;
        if (!data.TryGetProperty("LSJZList", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var row in rows.EnumerateArray())
        {
            var dateStr = GetString(row, "FSRQ");
            if (string.IsNullOrWhiteSpace(dateStr)) continue;
            if (!DateTime.TryParse(dateStr, Cn, DateTimeStyles.None, out var date)) continue;
            list.Add(new FundNavItem(
                Date: date,
                Nav: Num(GetString(row, "DWJZ")),
                AccNav: Num(GetString(row, "LJJZ")),
                ChangePct: Num(GetString(row, "JZZZL"))));
        }
        return list;
    }

    private static string? GetString(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static double Num(string? s)
        => double.TryParse(s, NumberStyles.Any, Cn, out var v) ? v : 0;
}
