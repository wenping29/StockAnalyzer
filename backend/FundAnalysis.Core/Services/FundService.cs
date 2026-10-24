using System.Text.Json;
using FundAnalysis.Data;
using FundAnalysis.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 场外基金清单：数据源为东财 fundcode_search.js（全市场约 2 万只，含类型分类），
/// 解析后写入 StockBasics（Type=Fund，Industry=基金类型）。
/// 净值数据仅对已加自选的基金采集（加入自选时自动触发）。
/// </summary>
public class FundService(AppDbContext db, IHttpClientFactory factory, ILogger<FundService> logger)
{
    private const string ListUrl = "https://fund.eastmoney.com/js/fundcode_search.js";

    /// <summary>同步全量基金清单到 StockBasics（幂等 upsert）。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 10)]
    public async Task SyncFundListAsync()
    {
        var client = factory.CreateClient("eastmoney");
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/126.0 Safari/537.36");
        client.DefaultRequestHeaders.Referrer = new Uri("https://fund.eastmoney.com/");

        var body = await client.GetStringAsync(ListUrl);
        // 文件格式：var r = [["代码","拼音","名称","类型","全拼"],...];
        // 取第一个 '[' 到最后一个 ']'（完整外层数组）
        var start = body.IndexOf('[');
        var end = body.LastIndexOf(']');
        if (start < 0 || end < 0)
        {
            logger.LogWarning("基金清单文件格式异常");
            return;
        }

        using var doc = JsonDocument.Parse(body[start..(end + 1)]);
        var existing = await db.StockBasics
            .Where(x => x.Type == SymbolTypes.Fund)
            .ToDictionaryAsync(x => x.Symbol);

        var added = 0;
        var updated = 0;
        var batch = new List<StockBasic>();
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (item.GetArrayLength() < 4) continue;
            var rawCode = item[0].GetString();
            var name = item[2].GetString();
            var fundType = item[3].GetString();
            if (string.IsNullOrWhiteSpace(rawCode) || string.IsNullOrWhiteSpace(name)) continue;

            // 基金代码与股票代码空间重叠（如 000001），加 ".F" 后缀避免唯一索引冲突
            var symbol = rawCode + ".F";

            if (existing.TryGetValue(symbol, out var row))
            {
                if (row.Name != name || row.Industry != fundType)
                {
                    row.Name = name;
                    row.Industry = fundType;
                    row.UpdatedAt = DateTime.Now;
                    updated++;
                }
            }
            else
            {
                db.StockBasics.Add(new StockBasic
                {
                    Symbol = symbol,
                    Name = name,
                    SecMarket = 0,
                    Type = SymbolTypes.Fund,
                    Industry = fundType,
                });
                existing[symbol] = new StockBasic { Symbol = symbol };
                added++;
                // 分批保存（全量约 2 万只，一次 SaveChanges 过重）
                if (added % 500 == 0)
                {
                    try
                    {
                        await db.SaveChangesAsync();
                    }
                    catch (DbUpdateException ex)
                    {
                        var dup = ex.Entries.FirstOrDefault()?.Entity as StockBasic;
                        logger.LogError("基金清单保存失败（冲突 Symbol: {Symbol}）：{Msg}", dup?.Symbol, ex.InnerException?.Message ?? ex.Message);
                        throw;
                    }
                }
            }
        }

        await db.SaveChangesAsync();
        logger.LogInformation("基金清单同步完成：新增 {Added} 只，更新 {Updated} 只，共 {Total} 只", added, updated, existing.Count);
    }

    /// <summary>基金类型列表（筛选下拉）。</summary>
    public async Task<List<string>> GetFundTypesAsync()
        => await db.StockBasics
            .Where(x => x.Type == SymbolTypes.Fund && x.Industry != null)
            .Select(x => x.Industry!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync();

    /// <summary>基金列表：类型/关键字筛选 + 最新净值联查（仅已采集的自选基金有净值）+ 分页。</summary>
    public async Task<(int Total, List<(StockBasic Fund, double? Nav, double? NavChgPct, DateTime? NavDate)> Items)> GetFundsAsync(
        string? keyword, string? fundType, string sort, int page, int pageSize)
    {
        var q = db.StockBasics.AsNoTracking().Where(x => x.Type == SymbolTypes.Fund);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            q = q.Where(x => x.Symbol.Contains(k) || x.Name.Contains(k));
        }
        if (!string.IsNullOrWhiteSpace(fundType))
            q = q.Where(x => x.Industry == fundType);

        // 排序：有净值的基金（即已采集的）优先排在前面，其余按代码
        if (sort == "navDate")
            q = q.OrderByDescending(x => x.UpdatedAt);
        else
            q = q.OrderBy(x => x.Symbol);

        var total = await q.CountAsync();
        var funds = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // 页内基金的最新净值（一次查询）
        var symbols = funds.Select(f => f.Symbol).ToList();
        var latestNavs = await db.FundNavs
            .Where(x => symbols.Contains(x.FundCode))
            .GroupBy(x => x.FundCode)
            .Select(g => new
            {
                Code = g.Key,
                Nav = g.OrderByDescending(n => n.Date).First().Nav,
                Chg = g.OrderByDescending(n => n.Date).First().ChangePct,
                Date = g.OrderByDescending(n => n.Date).First().Date,
            })
            .ToDictionaryAsync(x => x.Code);

        return (total, funds.Select(f =>
        {
            var nav = latestNavs.GetValueOrDefault(f.Symbol);
            return (f, (double?)nav?.Nav, nav?.Chg, (DateTime?)nav?.Date);
        }).ToList());
    }
}
