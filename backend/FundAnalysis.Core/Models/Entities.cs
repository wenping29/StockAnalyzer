using Microsoft.AspNetCore.Identity;

namespace FundAnalysis.Models;

/// <summary>应用用户。继承 Identity 自带的账号体系（密码哈希、锁定等由框架管理）。</summary>
public class AppUser : IdentityUser
{
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>标的分类：Stock=股票，Etf=场内基金，Fund=场外基金（只有日净值，无小时线/资金流）。</summary>
public static class SymbolTypes
{
    public const string Stock = "Stock";
    public const string Etf = "Etf";
    public const string Fund = "Fund";
}

/// <summary>标的基础信息（股票 / ETF / 场外基金）。Symbol 为 6 位代码，是业务主键。</summary>
public class StockBasic
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>东财 secid 的市场前缀：1=沪，0=深/北。</summary>
    public int SecMarket { get; set; }
    public string Type { get; set; } = SymbolTypes.Stock;
    /// <summary>分类描述：股票=行业，基金=基金类型（股票型/混合型/债券型等）。</summary>
    public string? Industry { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>日 K 线（股票与 ETF）。金额单位：元；成交量单位：手。</summary>
public class StockDaily
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime Date { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
    public double Amount { get; set; }
    public double ChangePct { get; set; }
}

/// <summary>分钟 K 线（仅自选标的采集，用于聚合小时线）。Volume 单位：手。</summary>
public class StockMinute
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime Time { get; set; }
    public double Open { get; set; }
    public double High { get; set; }
    public double Low { get; set; }
    public double Close { get; set; }
    public double Volume { get; set; }
}

/// <summary>场外基金历史净值。</summary>
public class FundNav
{
    public long Id { get; set; }
    public string FundCode { get; set; } = "";
    public DateTime Date { get; set; }
    /// <summary>单位净值。</summary>
    public double Nav { get; set; }
    /// <summary>累计净值。</summary>
    public double AccNav { get; set; }
    public double ChangePct { get; set; }
}

/// <summary>每日资金流（主力/大单/中单/小单净流入，单位：元）。</summary>
public class MoneyFlowDaily
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public DateTime Date { get; set; }
    public double MainNet { get; set; }
    public double LargeNet { get; set; }
    public double MidNet { get; set; }
    public double SmallNet { get; set; }
}

/// <summary>用户自选。</summary>
public class WatchlistItem
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string Symbol { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public AppUser? User { get; set; }
}

// ---------- 央行流动性 ----------

/// <summary>Shibor 日度利率。Period 如"隔夜(O/N)"、"1周(1W)"。</summary>
public class ShiborRate
{
    public long Id { get; set; }
    public string Period { get; set; } = "";
    public DateTime Date { get; set; }
    /// <summary>利率（%）。</summary>
    public double Rate { get; set; }
    /// <summary>较上日变动（基点 bp）。</summary>
    public double ChangeBp { get; set; }
}

/// <summary>货币供应量（月度）。单位：亿元。YoY 为同比增速（%）。</summary>
public class MoneySupply
{
    public long Id { get; set; }
    /// <summary>数据月份（取每月 1 日）。</summary>
    public DateTime Month { get; set; }
    public double M2 { get; set; }
    public double M1 { get; set; }
    public double M0 { get; set; }
    public double M2YoY { get; set; }
    public double M1YoY { get; set; }
    public double M0YoY { get; set; }
}

/// <summary>存款准备金率调整历史（大型/中小型存款类金融机构）。</summary>
public class ReserveRatio
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    /// <summary>大型机构调整后（%）。</summary>
    public double Large { get; set; }
    /// <summary>中小型机构调整后（%）。</summary>
    public double Small { get; set; }
}

/// <summary>LPR 报价历史（月度 20 日）。</summary>
public class LprRate
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public double Lpr1Y { get; set; }
    public double Lpr5Y { get; set; }
}

/// <summary>
/// 央行公开市场逆回购操作（含投放量为零的公告日，用于推进增量同步游标）。
/// 来源：人民银行官网公开市场业务交易公告，到期日按交易日历顺延计算。
/// </summary>
public class OmoOperation
{
    public long Id { get; set; }
    /// <summary>操作日期（投放开始日，公告发布当天）。</summary>
    public DateTime Date { get; set; }
    /// <summary>到期日期（投放结束日；操作日 + 期限自然日，顺延至交易日）。</summary>
    public DateTime MaturityDate { get; set; }
    /// <summary>期限描述（隔夜 / 7天 / 14天 / 3个月…）。</summary>
    public string Term { get; set; } = "";
    /// <summary>期限换算的自然日天数（隔夜=1，N个月≈30N），供排序与展示。</summary>
    public int TermDays { get; set; }
    /// <summary>中标量 / 投放量（亿元）。公告"操作量为零"时为 0。</summary>
    public double Amount { get; set; }
    /// <summary>操作利率（%）。公告未披露（新版格式）时为 null。</summary>
    public double? Rate { get; set; }
    /// <summary>公告标题（如 公开市场业务交易公告 [2026]第205号）。</summary>
    public string Announcement { get; set; } = "";
}

/// <summary>
/// 股票基本面快照（选股用）。估值列来自东财估值表（每日），业绩列来自业绩报表（最新报告期）。
/// 净利率 = 归母净利润 / 营业总收入（采集时计算）。
/// </summary>
public class StockFundamental
{
    public long Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>所属行业（东财板块名）。</summary>
    public string? Industry { get; set; }

    // ---------- 估值（元 / 倍 / 亿元） ----------
    public double ClosePrice { get; set; }
    /// <summary>当日涨跌幅（%）。</summary>
    public double? ChangePct { get; set; }
    public double? PeTtm { get; set; }
    public double? PbMrq { get; set; }
    public double? PsTtm { get; set; }
    /// <summary>总市值（亿元）。</summary>
    public double? MarketCap { get; set; }

    // ---------- 业绩（最新报告期） ----------
    public DateTime? ReportDate { get; set; }
    /// <summary>营业总收入（亿元）。</summary>
    public double Revenue { get; set; }
    /// <summary>营收同比增长（%）。</summary>
    public double? RevenueYoY { get; set; }
    /// <summary>归母净利润（亿元）。</summary>
    public double NetProfit { get; set; }
    /// <summary>净利润同比增长（%）。</summary>
    public double? NetProfitYoY { get; set; }
    /// <summary>加权净资产收益率（%）。</summary>
    public double? Roe { get; set; }
    /// <summary>销售毛利率（%）。</summary>
    public double? GrossMargin { get; set; }
    /// <summary>净利率（%）= 净利润 / 营收，采集时计算。</summary>
    public double? NetMargin { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

/// <summary>自选价格提醒规则。</summary>
public class AlertRule
{
    public long Id { get; set; }
    public string UserId { get; set; } = "";
    public string Symbol { get; set; } = "";

    /// <summary>规则类型：PriceAbove/PriceBelow（价格上破/下破）、ChangePctUp/ChangePctDown（日涨/跌超阈值%）。</summary>
    public string RuleType { get; set; } = "PriceAbove";
    /// <summary>阈值（价格元或百分比）。</summary>
    public double Threshold { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>最近一次触发时间与当时价格（null = 从未触发）。</summary>
    public DateTime? LastTriggeredAt { get; set; }
    public double? LastTriggeredPrice { get; set; }
}
