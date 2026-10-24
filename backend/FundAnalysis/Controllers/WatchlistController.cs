using System.Security.Claims;
using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/watchlist")]
[Authorize]
public class WatchlistController(AppDbContext db, AnalysisService analysis, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    private const int MaxItems = 50;

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>自选列表（含最新价/涨跌幅/近1月收益/主力净流入概览）。</summary>
    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await analysis.GetWatchlistOverviewAsync(UserId));

    /// <summary>添加自选，并立即后台采集该标的的数据。</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] WatchlistAddRequest request)
    {
        var basic = await db.StockBasics.FirstOrDefaultAsync(x => x.Symbol == request.Symbol);
        if (basic is null)
        {
            // 个股页/选股页的标的来自全市场基本面表（StockFundamental），
            // 未在 StockBasic 注册时自动补注册（市场前缀按代码段推断）
            var fund = await db.StockFundamentals.FirstOrDefaultAsync(x => x.Symbol == request.Symbol);
            if (fund is null)
                return NotFound(new { message = $"标的 {request.Symbol} 不存在" });

            basic = new StockBasic
            {
                Symbol = fund.Symbol,
                Name = fund.Name,
                SecMarket = fund.Symbol.StartsWith("60") || fund.Symbol.StartsWith("68") ? 1 : 0,
                Type = SymbolTypes.Stock,
            };
            db.StockBasics.Add(basic);
            await db.SaveChangesAsync();
        }

        var exists = await db.WatchlistItems.AnyAsync(x => x.UserId == UserId && x.Symbol == request.Symbol);
        if (exists)
            return Ok(new { message = "已在自选中" });

        var count = await db.WatchlistItems.CountAsync(x => x.UserId == UserId);
        if (count >= MaxItems)
            return BadRequest(new { message = $"自选最多 {MaxItems} 个" });

        db.WatchlistItems.Add(new WatchlistItem { UserId = UserId, Symbol = request.Symbol });
        await db.SaveChangesAsync();

        // 后台立即采集，几秒后数据即可查询
        backgroundJobs.Enqueue<CollectorService>(s => s.SyncSymbolAsync(request.Symbol));
        return Ok(new { message = $"已添加 {basic.Name}，数据采集中" });
    }

    /// <summary>移除自选。</summary>
    [HttpDelete("{symbol}")]
    public async Task<IActionResult> Remove(string symbol)
    {
        var item = await db.WatchlistItems.FirstOrDefaultAsync(x => x.UserId == UserId && x.Symbol == symbol);
        if (item is null) return Ok(new { message = "不在自选中" });

        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync();
        return Ok(new { message = "已移除" });
    }
}
