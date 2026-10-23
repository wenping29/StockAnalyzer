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

// ---------- 央行流动性 ----------

public record LiquidityCardDto(string Label, string Value, string? Sub, double? TrendPct);

public record ShiborPointDto(string Date, double On, double W1, double W2, double M1);

public record ShiborResponse(List<ShiborPointDto> Points);

public record MoneySupplyPointDto(string Month, double M2, double M1, double M0, double M2YoY, double M1YoY, double M0YoY);

public record MoneySupplyResponse(List<MoneySupplyPointDto> Points);

public record ReserveRatioItemDto(string Date, double Large, double Small, double? Change);

public record ReserveRatioResponse(List<ReserveRatioItemDto> Items);

public record LprItemDto(string Date, double Lpr1Y, double Lpr5Y);

public record LprResponse(List<LprItemDto> Items);

public record LiquidityOverviewDto(
    string LatestShiborDate,
    double ShiborOn, double ShiborOnChangeBp,
    double Shibor1W, double Shibor1WChangeBp,
    string LatestSupplyMonth,
    double M2YoY, double M1YoY, double M0YoY,
    double Lpr1Y, double Lpr5Y, string LatestLprDate,
    double ReserveLarge, double ReserveSmall, string LatestReserveDate);
