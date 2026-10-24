using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundAnalysis.Controllers;

/// <summary>选股器：按 ROE / 市盈率 / 市净率 / 净利率 / 营收同比 / 市值等维度筛选全市场 A 股。登录即可使用。</summary>
[ApiController]
[Route("api/screener")]
public class ScreenerController(FundamentalService fundamental, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    /// <summary>多条件筛选，支持排序与分页。</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Screen(
        [FromQuery] string? keyword,
        [FromQuery] string? industry,
        [FromQuery] double? minRoe,
        [FromQuery] double? maxRoe,
        [FromQuery] double? minPe,
        [FromQuery] double? maxPe,
        [FromQuery] double? maxPb,
        [FromQuery] double? minNetMargin,
        [FromQuery] double? minGrossMargin,
        [FromQuery] double? minRevenueYoy,
        [FromQuery] double? minNetProfitYoY,
        [FromQuery] double? minMarketCap,
        [FromQuery] string sort = "marketCap",
        [FromQuery] bool desc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var (total, items) = await fundamental.ScreenAsync(
            keyword, industry, minRoe, maxRoe, minPe, maxPe, maxPb,
            minNetMargin, minGrossMargin, minRevenueYoy, minNetProfitYoY, minMarketCap,
            sort, desc, page, pageSize);

        return Ok(new ScreenerResponse(total, page, pageSize, items.Select(x => new ScreenerItemDto(
            x.Symbol, x.Name, x.Industry,
            x.ClosePrice, x.PeTtm, x.PbMrq, x.MarketCap,
            x.ReportDate, x.Roe, x.NetMargin, x.GrossMargin,
            x.RevenueYoY, x.NetProfitYoY)).ToList()));
    }

    /// <summary>行业列表（筛选下拉）。</summary>
    [Authorize]
    [HttpGet("industries")]
    public async Task<IActionResult> Industries()
        => Ok(await fundamental.GetIndustriesAsync());

    /// <summary>手动触发基本面数据同步（估值 + 业绩，翻页采集约 1-2 分钟）。</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("sync")]
    public IActionResult Sync()
    {
        backgroundJobs.Enqueue<FundamentalService>(s => s.SyncAllAsync());
        return Ok(new { message = "已提交：同步全市场基本面数据（约 1-2 分钟）" });
    }
}
