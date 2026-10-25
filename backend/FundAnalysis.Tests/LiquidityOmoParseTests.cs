using FundAnalysis.Models;
using FundAnalysis.Services;
using Xunit;

namespace FundAnalysis.Tests;

/// <summary>央行逆回购公告解析测试（甘特图数据源）。样本文本模拟 StripHtml 后的公告正文。</summary>
public class LiquidityOmoParseTests
{
    [Fact]
    public void 新版内联公告_隔夜操作()
    {
        var ops = LiquidityService.ParseOmoAnnouncement(
            "公开市场业务交易公告 2026年10月23日 第205号：人民银行以固定利率、数量招标方式" +
            "开展了2580亿元隔夜逆回购操作，操作利率为1.40%。");

        var op = Assert.Single(ops);
        Assert.Equal(new DateTime(2026, 10, 23), op.Date);
        Assert.Equal("隔夜", op.Term);
        Assert.Equal(1, op.TermDays);
        Assert.Equal(2580, op.Amount);
        Assert.Null(op.Rate);
    }

    [Theory]
    [InlineData("7天期", "7天", 7)]    // 新版表述带"期"字
    [InlineData("7天", "7天", 7)]      // 旧版表述无"期"字
    [InlineData("3个月", "3个月", 90)]
    [InlineData("6个月期", "6个月", 180)]
    public void 内联公告_带期限操作(string rawTerm, string term, int termDays)
    {
        var ops = LiquidityService.ParseOmoAnnouncement(
            $"2026年10月23日 人民银行开展了641亿元{rawTerm}逆回购操作。");

        var op = Assert.Single(ops);
        Assert.Equal(term, op.Term);
        Assert.Equal(termDays, op.TermDays);
        Assert.Equal(641, op.Amount);
    }

    [Fact]
    public void 旧版表格公告_多期限带利率()
    {
        var ops = LiquidityService.ParseOmoAnnouncement(
            "2026年10月20日 人民银行以利率招标方式开展了逆回购操作。具体情况如下：" +
            "7天 1.40% 1,000亿元 900亿元；14天 1.90% 300亿元 280亿元；3个月 2.00% 300亿元 300亿元。");

        Assert.Equal(3, ops.Count);
        Assert.All(ops, o => Assert.Equal(new DateTime(2026, 10, 20), o.Date));

        Assert.Equal(("7天", 7, 900, 1.40), (ops[0].Term, ops[0].TermDays, ops[0].Amount, ops[0].Rate));
        Assert.Equal(("14天", 14, 280, 1.90), (ops[1].Term, ops[1].TermDays, ops[1].Amount, ops[1].Rate));
        Assert.Equal(("3个月", 90, 300, 2.00), (ops[2].Term, ops[2].TermDays, ops[2].Amount, ops[2].Rate));
    }

    [Fact]
    public void 表格与内联同期限_表格优先且保留利率()
    {
        // 表格行先入列（带利率），内联 7天期 后到被去重，避免同期限重复计数
        var ops = LiquidityService.ParseOmoAnnouncement(
            "2026年10月23日 7天 1.40% 1,000亿元 900亿元 开展了900亿元7天期逆回购操作。");

        var op = Assert.Single(ops);
        Assert.Equal("7天", op.Term);
        Assert.Equal(1.40, op.Rate);
    }

    [Fact]
    public void 未开展逆回购_留墓碑行推进增量游标()
    {
        var ops = LiquidityService.ParseOmoAnnouncement(
            "2026年10月19日 因当日无流动性需求，人民银行未开展逆回购操作。");

        var op = Assert.Single(ops);
        Assert.Equal("未解析", op.Term);
        Assert.Equal(0, op.Amount);
    }

    [Fact]
    public void 无日期文本_返回空列表()
    {
        var ops = LiquidityService.ParseOmoAnnouncement("今日开展了100亿元逆回购操作");
        Assert.Empty(ops);
    }

    [Fact]
    public void 内联数字带逗号_正确解析()
    {
        var ops = LiquidityService.ParseOmoAnnouncement(
            "2026年10月23日 开展了2,580亿元隔夜逆回购操作。");

        var op = Assert.Single(ops);
        Assert.Equal(2580, op.Amount);
    }
}
