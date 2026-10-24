using FundAnalysis.Models;

namespace FundAnalysis.Services.Providers;

/// <summary>外部行情数据提供者返回的原始条目。</summary>
public record KLineItem(DateTime Time, double Open, double High, double Low, double Close, double Volume, double Amount, double ChangePct);

/// <summary>日内分时点。Price=最新价，Avg=当日均价，Volume 单位：手。</summary>
public record TrendPoint(DateTime Time, double Price, double Volume, double Avg);

public record MoneyFlowItem(DateTime Date, double MainNet, double LargeNet, double MidNet, double SmallNet);

public record FundNavItem(DateTime Date, double Nav, double AccNav, double ChangePct);

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
}
