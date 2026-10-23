using System.Globalization;
using System.Text.Json;
using FundAnalysis.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FundAnalysis.Services;

/// <summary>
/// 沪深港通（北上/南下）资金数据。
/// 数据源：东财数据中心 RPT_MUTUAL_DEAL_HISTORY，MUTUAL_TYPE 005=北向合计（沪股通+深股通）、006=南向合计（港股通）。
/// 2024-08 起北向净流入停止披露（只有成交额）；南向净流入正常。金额单位：百万元 → 转亿元。
/// </summary>
public class HsgtService(IHttpClientFactory factory, IMemoryCache cache, ILogger<HsgtService> logger)
{
    private const string BaseUrl = "https://datacenter-web.eastmoney.com/api/data/v1/get";
    private static readonly CultureInfo Cn = CultureInfo.InvariantCulture;

    /// <summary>近 N 个交易日的北向成交额与南向净流入序列。</summary>
    public async Task<HsgtFlowDto> GetFlowAsync(int days)
    {
        days = Math.Clamp(days, 10, 120);
        const string cacheKey = "hsgt-flow";
        if (cache.TryGetValue(cacheKey, out HsgtFlowDto? cached) && cached is not null)
            return cached;

        var north = await FetchAsync("005", days); // 北向合计
        var south = await FetchAsync("006", days); // 南向合计
        if (north.Count == 0 || south.Count == 0)
            return new HsgtFlowDto(0, 0, null, 0, 0, []);

        var southByDate = south.ToDictionary(
            s => s.Date, s => (Net: s.NetInflow, Amount: s.Amount));

        var points = north
            .Where(n => southByDate.ContainsKey(n.Date))
            .OrderBy(n => n.Date)
            .Select(n => new HsgtPointDto(
                n.Date.ToString("yyyy-MM-dd"),
                Math.Round(n.Amount, 1),
                Math.Round(southByDate[n.Date].Net, 1),
                Math.Round(southByDate[n.Date].Amount, 1)))
            .ToList();

        var dto = new HsgtFlowDto(
            NorthTodayAmount: points.Count > 0 ? points[^1].NorthAmount : 0,
            NorthYesterdayAmount: points.Count > 1 ? points[^2].NorthAmount : 0,
            NorthAmountChangePct: points.Count > 1 && points[^2].NorthAmount > 0
                ? Math.Round((points[^1].NorthAmount / points[^2].NorthAmount - 1) * 100, 1)
                : null,
            SouthTodayNetInflow: points.Count > 0 ? points[^1].SouthNetInflow : 0,
            SouthYesterdayNetInflow: points.Count > 1 ? points[^2].SouthNetInflow : 0,
            Points: points);

        cache.Set(cacheKey, dto, TimeSpan.FromMinutes(5));
        return dto;
    }

    private async Task<List<(DateTime Date, double Amount, double NetInflow)>> FetchAsync(string mutualType, int days)
    {
        var result = new List<(DateTime, double, double)>();
        try
        {
            var client = factory.CreateClient("eastmoney");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
            var url = $"{BaseUrl}?reportName=RPT_MUTUAL_DEAL_HISTORY&columns=ALL" +
                      $"&filter=(MUTUAL_TYPE%3D%22{mutualType}%22)" +
                      $"&sortColumns=TRADE_DATE&sortTypes=-1&pageSize={days}&pageNumber=1";
            using var doc = JsonDocument.Parse(await client.GetStringAsync(url));

            if (!doc.RootElement.TryGetProperty("result", out var res) || res.ValueKind == JsonValueKind.Null)
                return result;
            if (!res.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var row in data.EnumerateArray())
            {
                if (!row.TryGetProperty("TRADE_DATE", out var d) || d.ValueKind != JsonValueKind.String) continue;
                if (!DateTime.TryParse(d.GetString(), Cn, DateTimeStyles.None, out var date)) continue;

                // DEAL_AMT 成交额（百万 → 亿）；NET_DEAL_AMT 净买入（百万 → 亿，北向可能为 null）
                var amount = row.TryGetProperty("DEAL_AMT", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDouble() / 100 : 0;
                var net = row.TryGetProperty("NET_DEAL_AMT", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetDouble() / 100 : 0;
                result.Add((date, amount, net));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "沪深港通数据拉取失败 MUTUAL_TYPE={Type}", mutualType);
        }
        return result;
    }
}
