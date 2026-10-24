using FundAnalysis.Models;
using FundAnalysis.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundAnalysis.Controllers;

/// <summary>个股浏览：全市场 A 股按板块（沪市主板/科创板/深市主板/创业板/北交所）浏览。登录即可使用。</summary>
[ApiController]
[Route("api/stocks")]
public class StocksController(FundamentalService fundamental) : ControllerBase
{
    private static string BoardOf(string symbol) =>
        FundamentalService.Boards
            .FirstOrDefault(b => b.Prefixes.Any(p => symbol.StartsWith(p)))
            is { Key: var k } ? k : "other";

    /// <summary>各板块股票数量。</summary>
    [Authorize]
    [HttpGet("boards")]
    public async Task<IActionResult> Boards()
    {
        var stats = await fundamental.GetBoardStatsAsync();
        var boards = new List<BoardStatDto> { new("all", "全部", stats.GetValueOrDefault("all")) };
        boards.AddRange(FundamentalService.Boards.Select(b => new BoardStatDto(b.Key, b.Name, stats.GetValueOrDefault(b.Key))));
        return Ok(boards);
    }

    /// <summary>个股列表：板块 + 关键字 + 行业 + 排序分页。</summary>
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string board = "all",
        [FromQuery] string? keyword = null,
        [FromQuery] string? industry = null,
        [FromQuery] string sort = "marketCap",
        [FromQuery] bool desc = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var (total, items) = await fundamental.GetStocksAsync(board, keyword, industry, sort, desc, page, pageSize);
        return Ok(new StockListResponse(total, page, pageSize, items.Select(x => new StockListItemDto(
            x.Symbol, x.Name, x.Industry, BoardOf(x.Symbol),
            x.ClosePrice, x.ChangePct, x.PeTtm, x.PbMrq,
            x.MarketCap, x.Roe, x.RevenueYoY, x.NetProfitYoY)).ToList()));
    }
}
