using System.Globalization;
using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;
using FundAnalysis.Services.Providers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FundAnalysis.Tests;

/// <summary>
/// 基金重仓股估算测试：F10 响应解析（只取最新季度）+ 按占净值比例加权的估算计算。
/// </summary>
public class FundEstimateTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public FundEstimateTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void 解析重仓股_只取最新季度并按行情链接取市场()
    {
        var snapshot = EastmoneyProvider.ParseFundHoldings(SampleResponse);

        Assert.NotNull(snapshot);
        Assert.Equal(new DateTime(2026, 9, 30), snapshot!.ReportDate);

        // 响应含最新季 + 上季度两张表，只应解析第一张
        Assert.Equal(2, snapshot.Items.Count);

        var first = snapshot.Items[0];
        Assert.Equal(1, first.Rank);
        Assert.Equal("600519", first.Symbol);
        Assert.Equal("贵州茅台", first.Name);
        Assert.Equal(1, first.SecMarket);
        Assert.Equal(9.96, first.WeightPct);

        // 港股持仓：链接市场前缀 116
        var hk = snapshot.Items[1];
        Assert.Equal("00883", hk.Symbol);
        Assert.Equal(116, hk.SecMarket);
    }

    [Fact]
    public async Task 估算涨幅_按覆盖持仓权重归一()
    {
        _db.StockBasics.Add(new StockBasic
        {
            Symbol = "110022.F", Name = "测试基金", Type = SymbolTypes.Fund,
        });
        await _db.SaveChangesAsync();

        var provider = new FakeProvider
        {
            Snapshot = new FundHoldingsSnapshot(new DateTime(2026, 9, 30),
            [
                new FundHoldingItem(1, "600519", "贵州茅台", 1, 10),
                new FundHoldingItem(2, "00883", "中国海洋石油", 116, 20),
            ]),
            Quotes = new Dictionary<string, RealtimeQuote>
            {
                ["1.600519"] = new(1441.56, 2.0),
                ["116.00883"] = new(23.5, -1.0),
            },
        };
        var service = new FundEstimateService(_db, provider, NullLogger<FundEstimateService>.Instance);

        var result = Assert.Single(await service.GetEstimatesAsync(["110022.F"]));

        // (10×2 + 20×(-1)) / (10+20) = 0
        Assert.Equal(0, result.EstimatedChangePct!.Value, 2);
        Assert.Equal(30, result.CoveredWeightPct, 2);
        Assert.Equal(new DateTime(2026, 9, 30), result.ReportDate);
        Assert.Equal(2, result.Holdings.Count);
        Assert.Equal(2.0, result.Holdings[0].ChangePct!.Value, 2);
        Assert.Equal(QuoteSourceNames.Eastmoney, result.QuoteSource);

        // 第二次查看命中缓存，不再请求上游重仓股
        await service.GetEstimatesAsync(["110022.F"]);
        Assert.Equal(1, provider.HoldingsCalls);
    }

    [Fact]
    public async Task 无行情持仓_不计入覆盖权重()
    {
        _db.StockBasics.Add(new StockBasic
        {
            Symbol = "161725.F", Name = "测试基金2", Type = SymbolTypes.Fund,
        });
        await _db.SaveChangesAsync();

        var provider = new FakeProvider
        {
            Snapshot = new FundHoldingsSnapshot(new DateTime(2026, 9, 30),
            [
                new FundHoldingItem(1, "600519", "贵州茅台", 1, 10),
                new FundHoldingItem(2, "00883", "中国海洋石油", 116, 20),
            ]),
            // 港股无行情（例如当日休市未返回）
            Quotes = new Dictionary<string, RealtimeQuote>
            {
                ["1.600519"] = new(1441.56, 2.0),
            },
        };
        var service = new FundEstimateService(_db, provider, NullLogger<FundEstimateService>.Instance);

        var result = Assert.Single(await service.GetEstimatesAsync(["161725.F"]));

        Assert.Equal(2.0, result.EstimatedChangePct!.Value, 2);
        Assert.Equal(10, result.CoveredWeightPct, 2);
        Assert.Null(result.Holdings[1].ChangePct);
    }

    [Fact]
    public async Task 空持仓基金_返回空清单且估算为空()
    {
        _db.StockBasics.Add(new StockBasic
        {
            Symbol = "000001.F", Name = "债券基金", Type = SymbolTypes.Fund,
        });
        await _db.SaveChangesAsync();

        var provider = new FakeProvider
        {
            Snapshot = new FundHoldingsSnapshot(new DateTime(2026, 9, 30), []),
        };
        var service = new FundEstimateService(_db, provider, NullLogger<FundEstimateService>.Instance);

        var result = Assert.Single(await service.GetEstimatesAsync(["000001.F"]));

        Assert.Null(result.EstimatedChangePct);
        Assert.Equal(0, result.CoveredWeightPct);
        Assert.Empty(result.Holdings);
    }

    [Fact]
    public async Task 备用行情源_透传给前端用于提示()
    {
        _db.StockBasics.Add(new StockBasic
        {
            Symbol = "320007.F", Name = "测试基金3", Type = SymbolTypes.Fund,
        });
        await _db.SaveChangesAsync();

        var provider = new FakeProvider
        {
            Source = QuoteSourceNames.Tencent, // 东财被阻断，走了腾讯备用源
            Snapshot = new FundHoldingsSnapshot(new DateTime(2026, 9, 30),
            [
                new FundHoldingItem(1, "600519", "贵州茅台", 1, 10),
            ]),
            Quotes = new Dictionary<string, RealtimeQuote>
            {
                ["1.600519"] = new(1441.56, 2.0),
            },
        };
        var service = new FundEstimateService(_db, provider, NullLogger<FundEstimateService>.Instance);

        var result = Assert.Single(await service.GetEstimatesAsync(["320007.F"]));

        Assert.Equal(QuoteSourceNames.Tencent, result.QuoteSource);
        Assert.Equal(2.0, result.EstimatedChangePct!.Value, 2);
    }

    [Fact]
    public void 腾讯行情解析_按代码字段定位并跳过异常数据()
    {
        // 腾讯接口按 ~ 分隔；这里特意让第二条股票名含 ~（GBK 字节位移场景），验证仍按代码定位
        var body =
            TencentRow("sh600519", "600519", "贵州茅台", 1441.56, 1452.00, -0.72) +
            TencentRow("sh600000", "600000", "浦~东", 10.50, 10.00, 5.00) +
            TencentRow("hk00700", "00700", "腾讯控股", 421.20, 430.40, -2.14) +
            TencentRow("bj920819", "920819", "颖泰生物", 2.78, 2.76, 0.72) +
            TencentRow("sz000001", "000001", "停牌股", 0, 11.64, 0) +
            "v_pv_none_match=\"1\";";

        var secIdById = new Dictionary<string, string>
        {
            ["sh600519"] = "1.600519",
            ["sh600000"] = "1.600000",
            ["hk00700"] = "116.00700",
            ["bj920819"] = "0.920819",
            ["sz000001"] = "0.000001",
        };

        var quotes = EastmoneyProvider.ParseTencentQuotes(body, secIdById);

        Assert.Equal(4, quotes.Count); // 停牌（价格 0）与未知代码被跳过
        Assert.Equal(-0.72, quotes["1.600519"].ChangePct, 2);
        Assert.Equal(1441.56, quotes["1.600519"].Price, 2);
        Assert.Equal(5.00, quotes["1.600000"].ChangePct, 2); // 名称含 ~ 仍能取到正确字段
        Assert.Equal(-2.14, quotes["116.00700"].ChangePct, 2);
        Assert.Equal(0.72, quotes["0.920819"].ChangePct, 2);
        Assert.DoesNotContain("0.000001", quotes.Keys);
    }

    /// <summary>构造腾讯行情单行：代码后第 1 段现价、第 2 段昨收、第 30 段涨跌幅。</summary>
    private static string TencentRow(
        string id, string code, string name, double price, double prevClose, double changePct)
    {
        var parts = new string[40];
        Array.Fill(parts, "0");
        parts[0] = "1";
        parts[1] = name;
        parts[2] = code;
        parts[3] = price.ToString(CultureInfo.InvariantCulture);
        parts[4] = prevClose.ToString(CultureInfo.InvariantCulture);
        parts[30] = "20261106120539";
        parts[31] = "0";
        parts[32] = changePct.ToString(CultureInfo.InvariantCulture);
        return $"v_{id}=\"{string.Join('~', parts)}\";";
    }

    /// <summary>模拟东财 F10 响应：两张季度表（最新 + 上季度）。</summary>
    private const string SampleResponse =
        "var apidata={ content:\"<div class='box'><div class='boxitem w790'><h4 class='t'>" +
        "<label class='left'><a>测试基金</a>&nbsp;&nbsp;2026年3季度股票投资明细</label>" +
        "<label class='right'>&nbsp;&nbsp;来源：天天基金&nbsp;&nbsp;&nbsp;&nbsp;截止至：<font class='px12'>2026-09-30</font></label>" +
        "</h4><table class='w782 comm tzxq'><thead><tr><th class='first'>序号</th><th>股票代码</th>" +
        "<th>股票名称</th><th>最新价</th><th>涨跌幅</th><th class='xglj'>相关资讯</th><th>占净值<br />比例</th>" +
        "<th class='cgs'>持股数<br />（万股）</th><th class='last ccs'>持仓市值<br />（万元）</th></tr></thead><tbody>" +
        "<tr><td>1</td><td><a href='//quote.eastmoney.com/unify/r/1.600519'>600519</a></td>" +
        "<td class='tol'><a href='//quote.eastmoney.com/unify/r/1.600519'>贵州茅台</a></td>" +
        "<td class='tor'><span data-id='dq600519'></span></td><td class='tor'><span data-id='zd600519'></span></td>" +
        "<td class='xglj'><a href='ccbdxq_110022_600519.html' class='red'>变动详情</a>" +
        "<a href='//guba.eastmoney.com/interface/GetList.aspx?code=1.600519'>股吧</a>" +
        "<a href='//quote.eastmoney.com/unify/r/1.600519'>行情</a></td>" +
        "<td class='tor'>9.96%</td><td class='tor'>80.00</td><td class='tor'>100,689.35</td></tr>" +
        "<tr><td>2</td><td><a href='//quote.eastmoney.com/unify/r/116.00883'>00883</a></td>" +
        "<td class='tol'><a href='//quote.eastmoney.com/unify/r/116.00883'>中国海洋石油</a></td>" +
        "<td class='tor'><span data-id='dq00883'></span></td><td class='tor'><span data-id='zd00883'></span></td>" +
        "<td class='xglj'><a href='ccbdxq_110022_00883.html' class='red'>变动详情</a>" +
        "<a href='//guba.eastmoney.com/interface/GetList.aspx?code=116.00883'>股吧</a>" +
        "<a href='//quote.eastmoney.com/unify/r/116.00883'>行情</a></td>" +
        "<td class='tor'>6.22%</td><td class='tor'>5,730.00</td><td class='tor'>117,853.17</td></tr>" +
        "</tbody></table></div></div><div class='box'><div class='boxitem w790'><h4 class='t'>" +
        "<label class='left'><a>测试基金</a>&nbsp;&nbsp;2026年2季度股票投资明细</label>" +
        "<label class='right'>截止至：<font class='px12'>2026-06-30</font></label></h4>" +
        "<table class='w782 comm tzxq'><tbody>" +
        "<tr><td>1</td><td><a href='//quote.eastmoney.com/unify/r/1.600519'>600519</a></td>" +
        "<td class='tol'><a href='//quote.eastmoney.com/unify/r/1.600519'>贵州茅台</a></td>" +
        "<td class='tor'></td><td class='tor'></td><td class='xglj'></td>" +
        "<td class='tor'>9.77%</td><td class='tor'>80.00</td><td class='tor'>94,838.96</td></tr>" +
        "</tbody></table></div></div>\",arryear:[2026,2025],curyear:2026};";

    private sealed class FakeProvider : IQuoteProvider
    {
        public FundHoldingsSnapshot? Snapshot { get; init; }
        public Dictionary<string, RealtimeQuote> Quotes { get; init; } = new();
        public int HoldingsCalls { get; private set; }

        public Task<FundHoldingsSnapshot?> FetchFundHoldingsAsync(string fundCode, CancellationToken ct = default)
        {
            HoldingsCalls++;
            return Task.FromResult(Snapshot);
        }

        public string Source { get; init; } = QuoteSourceNames.Eastmoney;

        public Task<RealtimeQuoteBatch> FetchRealtimeQuotesAsync(
            IReadOnlyCollection<(string Symbol, int SecMarket)> items, CancellationToken ct = default)
            => Task.FromResult(new RealtimeQuoteBatch(
                new Dictionary<string, RealtimeQuote>(Quotes), Source));

        public Task<List<StockBasic>> FetchSuggestAsync(string keyword, CancellationToken ct = default)
            => Task.FromResult(new List<StockBasic>());

        public Task<List<StockBasic>> FetchStockListAsync(CancellationToken ct = default)
            => Task.FromResult(new List<StockBasic>());

        public Task<List<KLineItem>> FetchKLineAsync(string symbol, int secMarket, int klt, int limit, int fqt = 1, CancellationToken ct = default)
            => Task.FromResult(new List<KLineItem>());

        public Task<List<MoneyFlowItem>> FetchMoneyFlowAsync(string symbol, int secMarket, int days, CancellationToken ct = default)
            => Task.FromResult(new List<MoneyFlowItem>());

        public Task<List<TrendPoint>> FetchTrendAsync(string symbol, int secMarket, CancellationToken ct = default)
            => Task.FromResult(new List<TrendPoint>());

        public Task<(double Value, double ChangePct)> FetchParityAsync(CancellationToken ct = default)
            => Task.FromResult((0d, 0d));

        public Task<List<FundNavItem>> FetchFundNavAsync(string fundCode, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new List<FundNavItem>());
    }
}
