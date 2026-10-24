using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundAnalysis.Controllers;

/// <summary>
/// 按分类手动触发数据采集。第三方调用入队到 Hangfire 后台作业执行，请求立即返回作业 ID，
/// 不阻塞、不影响 API 可用性（延续"Collector 入库 + API 读库"的解耦：请求路径绝不现调第三方）。
///
/// 前端"刷新"用法：POST /api/collect/{category} 拿到 jobId →
/// 轮询 GET /api/collect/status/{jobId} 直到 State 为 Succeeded / Failed。
/// </summary>
[ApiController]
[Route("api/collect")]
[Authorize]
public class CollectController(
    IBackgroundJobClient backgroundJobs,
    IMonitoringApi monitoringApi) : ControllerBase
{
    /// <summary>触发某分类采集，返回 Hangfire 作业 ID 供前端轮询状态。</summary>
    /// <remarks>
    /// 分类：indices(指数日K) / indicators(汇率·美债·中债) / intraday(指数日内分时) /
    /// watchlist(自选日线) / minutes(自选分钟) / stock-list(标的清单) /
    /// liquidity(央行流动性) / fundamentals(基本面选股) / fund-list(基金清单) / all(全部)。
    /// </remarks>
    [HttpPost("{category}")]
    public IActionResult Trigger(string category)
    {
        var key = (category ?? string.Empty).Trim().ToLowerInvariant();
        var jobId = key switch
        {
            "indices"      => backgroundJobs.Enqueue<CollectorService>(s => s.SyncIndicesAsync()),
            "indicators"   => backgroundJobs.Enqueue<CollectorService>(s => s.SyncMarketIndicatorsAsync()),
            "intraday"     => backgroundJobs.Enqueue<CollectorService>(s => s.SyncIndexIntradayAsync()),
            "watchlist"    => backgroundJobs.Enqueue<CollectorService>(s => s.SyncWatchlistDailyAsync()),
            "minutes"      => backgroundJobs.Enqueue<CollectorService>(s => s.SyncWatchlistMinutesAsync()),
            "stock-list"   => backgroundJobs.Enqueue<CollectorService>(s => s.SyncStockListAsync()),
            "liquidity"    => backgroundJobs.Enqueue<LiquidityService>(s => s.SyncAllAsync()),
            "fundamentals" => backgroundJobs.Enqueue<FundamentalService>(s => s.SyncAllAsync()),
            "fund-list"    => backgroundJobs.Enqueue<FundService>(s => s.SyncFundListAsync()),
            "all"          => backgroundJobs.Enqueue<CollectorService>(s => s.SyncAllCategoriesAsync()),
            _              => null,
        };
        if (jobId is null)
            return BadRequest(new { message = $"未知采集分类：{category}" });
        return Ok(new CollectJobDto(jobId, key, "Enqueued", DateTime.UtcNow));
    }

    /// <summary>查询采集作业状态（Enqueued / Processing / Succeeded / Failed 等）。</summary>
    [HttpGet("status/{jobId}")]
    public IActionResult Status(string jobId)
    {
        var job = monitoringApi.JobDetails(jobId);
        if (job is null) return NotFound(new { message = "作业不存在或已过期" });

        // History 最新一条即当前状态；CreatedAt 为作业创建（入队）时间
        var state = job.History.OrderByDescending(h => h.CreatedAt).FirstOrDefault()?.StateName ?? "Unknown";
        var name = job.Job?.Method.Name ?? string.Empty;
        return Ok(new CollectJobDto(jobId, name, state, job.CreatedAt));
    }
}
