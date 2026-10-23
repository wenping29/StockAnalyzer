using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(
    UserManager<AppUser> userManager,
    AppDbContext db,
    IBackgroundJobClient backgroundJobs) : ControllerBase
{
    /// <summary>用户列表（含角色与启用状态）。</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await db.Users.OrderBy(u => u.CreatedAt).ToListAsync();
        var result = new List<AdminUserDto>();
        foreach (var u in users)
        {
            var roles = await userManager.GetRolesAsync(u);
            var disabled = u.LockoutEnd is { } end && end > DateTimeOffset.UtcNow.AddYears(10);
            result.Add(new AdminUserDto(u.Id, u.UserName!, u.Email,
                roles.OrderByDescending(r => r == "Admin" ? 1 : r == "VIP" ? 2 : 3).FirstOrDefault() ?? "User",
                !disabled, u.CreatedAt));
        }
        return Ok(result);
    }

    /// <summary>设置用户角色（单角色模型：User / VIP / Admin）。</summary>
    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleRequest request)
    {
        if (!DbSeeder.AllRoles.Contains(request.Role))
            return BadRequest(new { message = $"角色只能是：{string.Join(" / ", DbSeeder.AllRoles)}" });

        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "用户不存在" });

        foreach (var role in DbSeeder.AllRoles)
        {
            if (await userManager.IsInRoleAsync(user, role) && role != request.Role)
                await userManager.RemoveFromRoleAsync(user, role);
        }
        if (!await userManager.IsInRoleAsync(user, request.Role))
            await userManager.AddToRoleAsync(user, request.Role);

        return Ok(new { message = $"已将 {user.UserName} 的角色设为 {request.Role}" });
    }

    /// <summary>启用 / 禁用用户。</summary>
    [HttpPut("users/{id}/enabled")]
    public async Task<IActionResult> UpdateEnabled(string id, [FromBody] UpdateEnabledRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null) return NotFound(new { message = "用户不存在" });

        // 用一个极大的 LockoutEnd 表示"禁用"，登录时会被拦截
        await userManager.SetLockoutEndDateAsync(user, request.Enabled
            ? null
            : DateTimeOffset.MaxValue.AddYears(-10));
        return Ok(new { message = request.Enabled ? $"已启用 {user.UserName}" : $"已禁用 {user.UserName}" });
    }

    /// <summary>手动触发：注册示例标的并拉取全部初始数据（搜索接口注册，无需全市场列表）。</summary>
    [HttpPost("collect/initial")]
    public IActionResult TriggerInitial()
    {
        backgroundJobs.Enqueue<CollectorService>(s => s.InitialSyncAsync());
        return Ok(new { message = "已提交：初始化示例标的数据" });
    }

    /// <summary>手动触发：同步全市场标的列表（部分网络环境不可用，失败不影响其他功能）。</summary>
    [HttpPost("collect/stock-list")]
    public IActionResult TriggerStockList()
    {
        backgroundJobs.Enqueue<CollectorService>(s => s.SyncStockListAsync());
        return Ok(new { message = "已提交：同步标的列表" });
    }

    /// <summary>手动触发：立即同步自选 + 示例标的的全部数据。</summary>
    [HttpPost("collect/daily")]
    public IActionResult TriggerDaily()
    {
        backgroundJobs.Enqueue<CollectorService>(s => s.SyncWatchlistDailyAsync());
        return Ok(new { message = "已提交：同步日线与资金流" });
    }

    /// <summary>手动触发：立即同步分钟线。</summary>
    [HttpPost("collect/minutes")]
    public IActionResult TriggerMinutes()
    {
        backgroundJobs.Enqueue<CollectorService>(s => s.SyncWatchlistMinutesAsync());
        return Ok(new { message = "已提交：同步分钟线" });
    }
}
