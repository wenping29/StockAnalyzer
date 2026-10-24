using FundAnalysis.Data;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Controllers;

/// <summary>基金页面：全市场场外基金浏览（类型筛选/搜索/最新净值/加自选）。登录即可使用。</summary>
[ApiController]
[Route("api/funds")]
public class FundsController(FundService funds, AppDbContext db, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    public record FundItemDto(
        string Symbol, string Name, string? FundType,
        double? Nav, double? NavChgPct, DateTime? NavDate, bool InWatchlistHintIgnored = false);

    /// <summary>基金列表（类型/关键字筛选 + 排序分页）。inWatchlist 由前端用自选集合判断。</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string? fundType,
        [FromQuery] string sort = "code",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var (total, items) = await funds.GetFundsAsync(keyword, fundType, sort, page, pageSize);
        return Ok(new
        {
            total,
            page,
            pageSize,
            items = items.Select(x => new
            {
                symbol = x.Fund.Symbol,
                name = x.Fund.Name,
                fundType = x.Fund.Industry,
                nav = x.Nav,
                navChgPct = x.NavChgPct,
                navDate = x.NavDate,
            }),
        });
    }

    /// <summary>基金类型列表（筛选下拉）。</summary>
    [Authorize]
    [HttpGet("types")]
    public async Task<IActionResult> Types()
        => Ok(await funds.GetFundTypesAsync());

    /// <summary>手动触发：同步全量基金清单。</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("sync")]
    public IActionResult Sync()
    {
        backgroundJobs.Enqueue<FundService>(s => s.SyncFundListAsync());
        return Ok(new { message = "已提交：同步全量基金清单（约 30 秒）" });
    }
}
