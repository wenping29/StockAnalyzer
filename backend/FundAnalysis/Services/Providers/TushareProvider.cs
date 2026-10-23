using FundAnalysis.Models;

namespace FundAnalysis.Services.Providers;

/// <summary>
/// Tushare Pro 适配器（预留骨架）。接入时在 appsettings 的 Tushare:Token 填入令牌，
/// 再把各方法替换为对 http://api.tushare.pro 的 HTTP 调用即可，采集层无需改动。
/// </summary>
public class TushareProvider : IQuoteProvider
{
    public Task<List<StockBasic>> FetchStockListAsync(CancellationToken ct = default)
        => throw new NotImplementedException("Tushare 适配器尚未接入，请使用东方财富数据源。");

    public Task<List<KLineItem>> FetchKLineAsync(string symbol, int secMarket, int klt, int limit, CancellationToken ct = default)
        => throw new NotImplementedException("Tushare 适配器尚未接入，请使用东方财富数据源。");

    public Task<List<MoneyFlowItem>> FetchMoneyFlowAsync(string symbol, int secMarket, int days, CancellationToken ct = default)
        => throw new NotImplementedException("Tushare 适配器尚未接入，请使用东方财富数据源。");

    public Task<List<FundNavItem>> FetchFundNavAsync(string fundCode, int pageSize, CancellationToken ct = default)
        => throw new NotImplementedException("Tushare 适配器尚未接入，请使用东方财富数据源。");
}
