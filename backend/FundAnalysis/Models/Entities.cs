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
