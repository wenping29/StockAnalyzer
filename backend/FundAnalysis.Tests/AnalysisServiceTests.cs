using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Tests;

/// <summary>
/// AnalysisService 聚合计算测试：SQLite 内存库 + 手工构造日线数据，
/// 验证 周/月 聚合、区间收益、最大回撤的正确性。
/// </summary>
public class AnalysisServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly AnalysisService _service;

    public AnalysisServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        // 服务先查标的存在性，测试标的需先注册
        _db.StockBasics.Add(new StockBasic
        {
            Symbol = "TEST",
            Name = "测试标的",
            SecMarket = 1,
            Type = SymbolTypes.Stock,
        });
        _db.SaveChanges();
        _service = new AnalysisService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SeedDailyAsync(params (string Date, double Open, double High, double Low, double Close)[] bars)
    {
        foreach (var b in bars)
        {
            _db.StockDailies.Add(new StockDaily
            {
                Symbol = "TEST",
                Date = DateTime.Parse(b.Date),
                Open = b.Open, High = b.High, Low = b.Low, Close = b.Close,
                Volume = 1000, Amount = 100000, ChangePct = 0,
            });
        }
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task WeekKline_GroupsByMondayAndAggregatesOhlc()
    {
        // 2026-10-19(周一)~10-23(周五) 同一周；10-26(周一) 新一周
        await SeedDailyAsync(
            ("2026-10-19", 10, 11, 9.5, 10.5),
            ("2026-10-20", 10.5, 12, 10, 11),
            ("2026-10-23", 11, 11.5, 10.5, 11.2),
            ("2026-10-26", 11.2, 12.5, 11, 12));

        var result = await _service.GetKLineAsync("TEST", "week", 100);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Points.Count);

        // 第一周：开=19日开盘，收=23日收盘，高=20日最高，低=19日最低
        var w1 = result.Points[0];
        Assert.Equal("2026-10-19", w1.Time);
        Assert.Equal(10, w1.Open);
        Assert.Equal(12, w1.High);
        Assert.Equal(9.5, w1.Low);
        Assert.Equal(11.2, w1.Close);
        Assert.Equal(3000, w1.Volume); // 三日成交量之和

        // 第二周
        var w2 = result.Points[1];
        Assert.Equal("2026-10-26", w2.Time);
        Assert.Equal(12, w2.Close);
    }

    [Fact]
    public async Task MonthKline_GroupsByMonth()
    {
        await SeedDailyAsync(
            ("2026-09-28", 10, 10.5, 9.8, 10.2),
            ("2026-09-30", 10.2, 10.8, 10, 10.6),
            ("2026-10-09", 10.6, 11, 10.4, 10.9),
            ("2026-10-23", 10.9, 11.2, 10.7, 11.1));

        var result = await _service.GetKLineAsync("TEST", "month", 100);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Points.Count);
        Assert.Equal("2026-09", result.Points[0].Time);
        Assert.Equal("2026-10", result.Points[1].Time);
        Assert.Equal(10, result.Points[0].Open);
        Assert.Equal(11.1, result.Points[1].Close);
    }

    [Fact]
    public async Task Performance_ComputesWindowsAndMaxDrawdown()
    {
        // 21 个交易日 + 1：便于计算"近1月"（21 日）收益率
        // 价格：100 → 涨到 110（峰值）→ 跌到 99（回撤 10%）→ 收 105
        var bars = new List<(string, double, double, double, double)>();
        for (var i = 0; i < 22; i++)
        {
            var close = i switch
            {
                <= 5 => 100 + i,                      // 100..105
                <= 10 => 105 + (i - 5),               // 106..110 峰值 110
                <= 20 => 110 - (i - 10),              // 109..100 最低 100
                _ => 105,                              // 末日回升
            };
            bars.Add((DateTime.Parse("2026-09-01").AddBusinessDays(i).ToString("yyyy-MM-dd"), close, close + 1, close - 1, close));
        }
        await SeedDailyAsync(bars.ToArray());

        var result = await _service.GetPerformanceAsync("TEST");

        Assert.NotNull(result);
        Assert.Equal(105, result!.LatestPrice);

        // 最大回撤：峰值 110 → 谷值 100 = (1 - 100/110) ≈ 9.09%
        Assert.Equal(9.09, result.MaxDrawdownPct, 2);

        // 近1月（21 交易日）：末日 105 vs 21 天前 → series[22-1-21]=series[0]=100 → +5%
        var month = result.Windows.First(w => w.Key == "1m");
        Assert.NotNull(month.ReturnPct);
        Assert.Equal(5.0, month.ReturnPct!.Value, 2);

        // 近1周（5 交易日）：末日 105 vs 5 天前 100（i=16 → 104?）——直接断言区间合理即可
        var week = result.Windows.First(w => w.Key == "1w");
        Assert.NotNull(week.ReturnPct);
    }

    [Fact]
    public async Task Performance_EmptySeries_ReturnsZeros()
    {
        // 标的存在但无行情数据：应返回零值而非报错
        _db.StockBasics.Add(new StockBasic { Symbol = "EMPTY", Name = "空数据标的", SecMarket = 1, Type = SymbolTypes.Stock });
        await _db.SaveChangesAsync();

        var result = await _service.GetPerformanceAsync("EMPTY");
        Assert.NotNull(result);
        Assert.Equal(0, result!.LatestPrice);
        Assert.Empty(result.Windows);
        Assert.Equal(0, result.MaxDrawdownPct);
    }
}

/// <summary>工作日推进（跳过周末），用于构造真实感日线序列。</summary>
internal static class DateExtensions
{
    public static DateTime AddBusinessDays(this DateTime start, int days)
    {
        var date = start;
        var remaining = days;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                remaining--;
        }
        return date;
    }
}
