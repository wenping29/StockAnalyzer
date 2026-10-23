using FundAnalysis.Models;
using FundAnalysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FundAnalysis.Data;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/quote")]
public class QuoteController(AppDbContext db, AnalysisService analysis) : ControllerBase
{
    /// <summary>标的搜索（代码或名称模糊匹配）。</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string keyword, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(Array.Empty<SearchItemDto>());

        var k = keyword.Trim();
        var items = await db.StockBasics
            .Where(x => x.Symbol.Contains(k) || x.Name.Contains(k))
            .OrderBy(x => x.Symbol)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(x => new SearchItemDto(
                x.Symbol, x.Name, x.Type,
                x.SecMarket == 1 ? "沪" : x.Type == SymbolTypes.Fund ? "基金" : "深/北"))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>标的基础信息。</summary>
    [HttpGet("basics")]
    public async Task<IActionResult> Basics([FromQuery] string symbol)
    {
        var basic = await analysis.GetBasicAsync(symbol);
        if (basic is null) return NotFound(new { message = $"未找到标的 {symbol}" });
        return Ok(new { basic.Symbol, basic.Name, basic.Type, market = basic.SecMarket == 1 ? "沪" : "深/北" });
    }

    /// <summary>多周期 K 线。小时线（分钟数据）需要 VIP 或 Admin 角色。</summary>
    [Authorize]
    [HttpGet("kline")]
    public async Task<IActionResult> KLine(
        [FromQuery] string symbol,
        [FromQuery] string period = "day",
        [FromQuery] int limit = 120)
    {
        if (period is not ("hour" or "day" or "week" or "month"))
            return BadRequest(new { message = "period 只支持 hour/day/week/month" });

        if (period == "hour" && !User.IsInRole("VIP") && !User.IsInRole("Admin"))
            return Forbid();

        var result = await analysis.GetKLineAsync(symbol, period, Math.Clamp(limit, 1, 800));
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol} 或该周期无数据" });
        return Ok(result);
    }

    /// <summary>区间收益统计（近1周/1月/3月/6月/今年以来 + 最大回撤）。</summary>
    [Authorize]
    [HttpGet("performance")]
    public async Task<IActionResult> Performance([FromQuery] string symbol)
    {
        var result = await analysis.GetPerformanceAsync(symbol);
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol}" });
        return Ok(result);
    }

    /// <summary>每日资金流明细（主力/大单/中单/小单）。需要 VIP 或 Admin 角色。</summary>
    [Authorize(Roles = "VIP,Admin")]
    [HttpGet("moneyflow")]
    public async Task<IActionResult> MoneyFlow([FromQuery] string symbol, [FromQuery] int days = 30)
    {
        var result = await analysis.GetMoneyFlowAsync(symbol, Math.Clamp(days, 1, 120));
        if (result is null) return NotFound(new { message = $"未找到标的 {symbol} 的资金流数据（场外基金无资金流）" });
        return Ok(result);
    }
}
