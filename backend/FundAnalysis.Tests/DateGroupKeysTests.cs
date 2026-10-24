using FundAnalysis.Services;
using Xunit;

namespace FundAnalysis.Tests;

/// <summary>DateGroupKeys 五种粒度 key 测试（成交量趋势/指数聚合共用）。</summary>
public class DateGroupKeysTests
{
    [Theory]
    [InlineData("2026-10-23", "day", "2026-10-23")]
    [InlineData("2026-10-23", "month", "2026-10")]
    [InlineData("2026-10-23", "year", "2026")]
    [InlineData("2026-10-23", "quarter", "2026-Q4")]
    [InlineData("2026-01-15", "quarter", "2026-Q1")]
    [InlineData("2026-06-30", "quarter", "2026-Q2")]
    [InlineData("2026-08-01", "quarter", "2026-Q3")]
    public void Key_ReturnsExpectedFormat(string date, string granularity, string expected)
    {
        Assert.Equal(expected, DateGroupKeys.Key(DateTime.Parse(date), granularity));
    }

    [Theory]
    // 周一 → 本周一自身
    [InlineData("2026-10-19", "2026-10-19")] // 周一
    // 周中 → 本周一
    [InlineData("2026-10-21", "2026-10-19")] // 周三
    [InlineData("2026-10-23", "2026-10-19")] // 周五
    // 周日 → 上周一
    [InlineData("2026-10-25", "2026-10-19")] // 周日
    // 跨年周：2026-01-01 是周四 → 本周一 2025-12-29
    [InlineData("2026-01-01", "2025-12-29")]
    public void Week_GroupsByMonday(string date, string expectedMonday)
    {
        Assert.Equal(expectedMonday, DateGroupKeys.Week(DateTime.Parse(date)));
    }
}
