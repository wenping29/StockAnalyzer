using FundAnalysis.Data;
using FundAnalysis.Models;
using FundAnalysis.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    TokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>注册新用户（默认 User 角色）。</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length < 3)
            return BadRequest(new { message = "用户名至少 3 个字符" });
        if (request.Password.Length < 6)
            return BadRequest(new { message = "密码至少 6 位" });

        var user = new AppUser
        {
            UserName = request.UserName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = $"注册失败：{error}" });
        }

        await userManager.AddToRoleAsync(user, "User");
        logger.LogInformation("新用户注册：{UserName}", user.UserName);
        return Ok(await tokenService.IssueAsync(user));
    }

    /// <summary>登录，返回访问令牌与刷新令牌。</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await userManager.FindByNameAsync(request.UserName);
        if (user is null)
            return Unauthorized(new { message = "用户名或密码错误" });

        if (await userManager.IsLockedOutAsync(user))
            return Unauthorized(new { message = "账号已锁定，请稍后再试" });

        // 被管理员禁用的账号（LockoutEnd 设为极大值视为禁用）
        if (user.LockoutEnd is { } end && end > DateTimeOffset.UtcNow.AddYears(10))
            return Unauthorized(new { message = "账号已被禁用，请联系管理员" });

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { message = "用户名或密码错误" });

        return Ok(await tokenService.IssueAsync(user));
    }

    /// <summary>用刷新令牌换取新的令牌对。</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        var userId = tokenService.ValidateRefreshToken(request.RefreshToken);
        if (userId is null)
            return Unauthorized(new { message = "刷新令牌无效或已过期" });

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return Unauthorized(new { message = "用户不存在" });

        return Ok(await tokenService.IssueAsync(user));
    }

    /// <summary>当前登录用户信息。</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new UserInfoDto(user.Id, user.UserName!, user.Email, roles));
    }

    /// <summary>修改自己的密码。</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await userManager.GetUserAsync(User)
            ?? throw new InvalidOperationException("用户不存在");
        var result = await userManager.ChangePasswordAsync(user, request.OldPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var error = string.Join("; ", result.Errors.Select(e => e.Description));
            return BadRequest(new { message = $"修改失败：{error}" });
        }
        return Ok(new { message = "密码已修改" });
    }

    /// <summary>全部用户角色（前端展示用）。</summary>
    [HttpGet("roles")]
    public IActionResult Roles() => Ok(DbSeeder.AllRoles);
}
