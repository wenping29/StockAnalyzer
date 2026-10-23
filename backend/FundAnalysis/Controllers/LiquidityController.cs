using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundAnalysis.Controllers;

/// <summary>央行流动性数据（Shibor / 货币供应 / 存准率 / LPR）。登录即可访问。</summary>
[ApiController]
[Route("api/liquidity")]
[Authorize]
public class LiquidityController(LiquidityService liquidity, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    /// <summary>概览卡片数据。</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> Overview()
        => Ok(await liquidity.GetOverviewAsync());

    /// <summary>Shibor 走势（隔夜/1周/2周/1个月）。</summary>
    [HttpGet("shibor")]
    public async Task<IActionResult> Shibor([FromQuery] int days = 90)
        => Ok(await liquidity.GetShiborAsync(Math.Clamp(days, 7, 720)));

    /// <summary>货币供应量 M0/M1/M2（总量与同比）。</summary>
    [HttpGet("money-supply")]
    public async Task<IActionResult> MoneySupply([FromQuery] int months = 24)
        => Ok(await liquidity.GetMoneySupplyAsync(Math.Clamp(months, 6, 120)));

    /// <summary>存款准备金率调整历史。</summary>
    [HttpGet("reserve-ratio")]
    public async Task<IActionResult> ReserveRatio()
        => Ok(await liquidity.GetReserveRatiosAsync());

    /// <summary>LPR 报价历史。</summary>
    [HttpGet("lpr")]
    public async Task<IActionResult> Lpr()
        => Ok(await liquidity.GetLprAsync());

    /// <summary>手动触发流动性数据同步。</summary>
    [HttpPost("sync")]
    public IActionResult Sync()
    {
        backgroundJobs.Enqueue<LiquidityService>(s => s.SyncAllAsync());
        return Ok(new { message = "已提交：同步央行流动性数据" });
    }
}
