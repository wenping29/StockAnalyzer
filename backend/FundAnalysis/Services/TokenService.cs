using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FundAnalysis.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace FundAnalysis.Services;

/// <summary>签发 / 校验 JWT。刷新令牌同样是 JWT（7 天有效、带 tokentype=refresh 标记），无需落库。</summary>
public class TokenService(IConfiguration config, UserManager<AppUser> userManager)
{
    public async Task<TokenResponse> IssueAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var userInfo = new UserInfoDto(user.Id, user.UserName!, user.Email, roles);

        var expireMinutes = config.GetValue("Jwt:ExpireMinutes", 120);
        var refreshDays = config.GetValue("Jwt:RefreshDays", 7);
        var accessToken = CreateToken(user, roles, TimeSpan.FromMinutes(expireMinutes), "access");
        var refreshToken = CreateToken(user, roles, TimeSpan.FromDays(refreshDays), "refresh");

        return new TokenResponse(accessToken, refreshToken, expireMinutes, userInfo);
    }

    /// <summary>校验刷新令牌，返回用户 Id；不合法或过期返回 null。</summary>
    public string? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        try
        {
            var principal = handler.ValidateToken(refreshToken, parameters, out _);
            if (principal.FindFirst("tokentype")?.Value != "refresh") return null;
            return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }
        catch
        {
            return null;
        }
    }

    private string CreateToken(AppUser user, IList<string> roles, TimeSpan lifetime, string tokenType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new("tokentype", tokenType),
        };
        if (!string.IsNullOrEmpty(user.Email))
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
