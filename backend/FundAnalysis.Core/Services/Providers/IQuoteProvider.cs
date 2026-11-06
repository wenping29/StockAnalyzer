using FundAnalysis.Models;

namespace FundAnalysis.Services.Providers;

/// <summary>外部行情数据提供者返回的原始条目。</summary>
public record KLineItem(DateTime Time, double Open, double High, double Low, double Close, double Volume, double Amount, double ChangePct);

/// <summary>日内分时点。Price=最新价，Avg=当日均价，Volume 单位：手。</summary>
public record TrendPoint(DateTime Time, double Price, double Volume, double Avg);

public record MoneyFlowItem(DateTime Date, double MainNet, double LargeNet, double MidNet, double SmallNet);

public record FundNavItem(DateTime Date, double Nav, double AccNav, double ChangePct);

/// <summary>基金重仓股条目。SecMarket 为东财 secid 市场前缀（1=沪，0=深/北，116=港）。</summary>
public record FundHoldingItem(int Rank, string Symbol, string Name, int SecMarket, double WeightPct)
{
    /// <summary>东财 secid 唯一键，用于匹配实时行情。</summary>
    public string SecId => $"{SecMarket}.{Symbol}";
}

/// <summary>基金重仓股快照：ReportDate 为季报截止日（上游未返回时为 null）。</summary>
public record FundHoldingsSnapshot(DateTime? ReportDate, List<FundHoldingItem> Items);

/// <summary>实时行情快照：最新价与当日涨跌幅（%）。</summary>
public record RealtimeQuote(double Price, double ChangePct);

/// <summary>批量实时行情 + 实际生效的数据源。键为 "市场.代码"（与 FundHoldingItem.SecId 一致）。</summary>
public record RealtimeQuoteBatch(Dictionary<string, RealtimeQuote> Quotes, string Source);

/// <summary>实时行情源标识：前端据此提示"东财被阻断、已切换备用源"。</summary>
public static class QuoteSourceNames
{
    public const string Eastmoney = "Eastmoney";
    public const string Tencent = "Tencent";
    /// <summary>两个行情源都不可用（本次拿不到任何实时价）。</summary>
    public const string None = "None";
}

/// <summary>
/// 行情数据源统一接口。当前实现为东方财富公开接口（EastmoneyProvider），
/// 之后接入 Tushare（TushareProvider）只需再实现一份，采集层代码不用动。
/// </summary>
public interface IQuoteProvider
{
    /// <summary>关键词搜索标的（代码/拼音/名称），结果即时入库由调用方负责。</summary>
    Task<List<StockBasic>> FetchSuggestAsync(string keyword, CancellationToken ct = default);

    /// <summary>拉取全市场股票 + ETF 基础列表（部分网络环境下东财列表接口不可用，失败不影响主流程）。</summary>
    Task<List<StockBasic>> FetchStockListAsync(CancellationToken ct = default);

    /// <summary>拉取 K 线。klt：101=日线，5=5分钟线，15/30/60 分钟线；fqt：1=前复权（股票/ETF），0=不复权（指数/债券收益率）。</summary>
    Task<List<KLineItem>> FetchKLineAsync(string symbol, int secMarket, int klt, int limit, int fqt = 1, CancellationToken ct = default);

    /// <summary>拉取每日资金流（主力/大单/中单/小单净流入，单位元）。</summary>
    Task<List<MoneyFlowItem>> FetchMoneyFlowAsync(string symbol, int secMarket, int days, CancellationToken ct = default);

    /// <summary>拉取日内分时走势（trends2，ndays=1 = 最近一个交易日的全部分钟点）。</summary>
    Task<List<TrendPoint>> FetchTrendAsync(string symbol, int secMarket, CancellationToken ct = default);

    /// <summary>拉取在岸人民币中间价（央行每日公布，push2 单点；f43=中间价×10000，f170=日变动%）。</summary>
    Task<(double Value, double ChangePct)> FetchParityAsync(CancellationToken ct = default);

    /// <summary>拉取场外基金历史净值。</summary>
    Task<List<FundNavItem>> FetchFundNavAsync(string fundCode, int pageSize, CancellationToken ct = default);

    /// <summary>拉取场外基金重仓股（季报前十大，含占净值比例）。无持仓的基金返回空列表。</summary>
    Task<FundHoldingsSnapshot?> FetchFundHoldingsAsync(string fundCode, CancellationToken ct = default);

    /// <summary>批量拉取实时行情（最新价/涨跌幅），并返回实际使用的数据源（东财 / 备用源）。</summary>
    Task<RealtimeQuoteBatch> FetchRealtimeQuotesAsync(IReadOnlyCollection<(string Symbol, int SecMarket)> items, CancellationToken ct = default);
}
