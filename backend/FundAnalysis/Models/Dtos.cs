namespace FundAnalysis.Models;

// ---------- 认证相关 ----------

public record RegisterRequest(string UserName, string Password, string? Email);

public record LoginRequest(string UserName, string Password);

public record RefreshRequest(string RefreshToken);

public record ChangePasswordRequest(string OldPassword, string NewPassword);

public record UserInfoDto(string Id, string UserName, string? Email, IList<string> Roles);

public record TokenResponse(string AccessToken, string RefreshToken, int ExpiresInMinutes, UserInfoDto User);

// ---------- 行情相关 ----------

public record SearchItemDto(string Symbol, string Name, string Type, string MarketName);

public record KLinePointDto(string Time, double Open, double High, double Low, double Close, double Volume, double ChangePct);

public record KLineResponse(string Symbol, string Name, string Type, string Period, List<KLinePointDto> Points);

public record PerfWindowDto(string Key, string Label, double? ReturnPct);

public record PerformanceResponse(
    string Symbol, string Name, string Type,
    double LatestPrice, string LatestDate,
    List<PerfWindowDto> Windows,
    double MaxDrawdownPct);

public record MoneyFlowItemDto(string Date, double MainNet, double LargeNet, double MidNet, double SmallNet);

public record MoneyFlowResponse(string Symbol, List<MoneyFlowItemDto> Items, double CumulativeMainNet);

public record WatchlistAddRequest(string Symbol);

public record WatchlistItemDto(
    string Symbol, string Name, string Type,
    double? LatestPrice, double? ChangePct, double? MonthReturnPct, double? MainNetIn5d);

// ---------- 管理后台相关 ----------

public record AdminUserDto(string Id, string UserName, string? Email, string Role, bool Enabled, DateTime CreatedAt);

public record UpdateRoleRequest(string Role);

public record UpdateEnabledRequest(bool Enabled);
