using System.Security.Claims;
using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Controllers;

/// <summary>自选价格提醒：创建/管理规则，触发状态在列表中查看。登录即可使用。</summary>
[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController(AppDbContext db, AlertService alerts, IBackgroundJobClient backgroundJobs) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public record AlertCreateRequest(string Symbol, string RuleType, double Threshold);

    public record AlertDto(long Id, string Symbol, string? Name, string RuleType, double Threshold,
        bool Enabled, DateTime CreatedAt, DateTime? LastTriggeredAt, double? LastTriggeredPrice);

    /// <summary>我的提醒规则列表。</summary>
    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rules = await alerts.GetAsync(UserId);
        var symbols = rules.Select(r => r.Symbol).Distinct().ToList();
        var names = await db.StockBasics
            .Where(x => symbols.Contains(x.Symbol))
            .ToDictionaryAsync(x => x.Symbol, x => x.Name);

        return Ok(rules.Select(r => new AlertDto(
            r.Id, r.Symbol, names.GetValueOrDefault(r.Symbol), r.RuleType, r.Threshold,
            r.Enabled, r.CreatedAt, r.LastTriggeredAt, r.LastTriggeredPrice)));
    }

    /// <summary>新建提醒规则。</summary>
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AlertCreateRequest request)
    {
        try
        {
            var rule = await alerts.AddAsync(UserId, request.Symbol, request.RuleType, request.Threshold);
            return Ok(new { message = $"已创建提醒：{request.Symbol} {RuleLabel(request.RuleType)} {request.Threshold}", id = rule.Id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>启用/停用（重新启用会清空触发记录）。</summary>
    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Toggle(long id)
        => await alerts.ToggleAsync(UserId, id) ? Ok(new { message = "已更新" }) : NotFound();

    /// <summary>删除规则。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(long id)
        => await alerts.DeleteAsync(UserId, id) ? Ok(new { message = "已删除" }) : NotFound();

    /// <summary>立即执行一次提醒检查（管理后台/调试用）。</summary>
    [HttpPost("check")]
    public IActionResult CheckNow()
    {
        backgroundJobs.Enqueue<AlertService>(s => s.CheckAllAsync());
        return Ok(new { message = "已提交提醒检查" });
    }

    internal static string RuleLabel(string type) => type switch
    {
        "PriceAbove" => "价格高于",
        "PriceBelow" => "价格低于",
        "ChangePctUp" => "日涨幅超",
        "ChangePctDown" => "日跌幅超",
        _ => type,
    };
}
