namespace FundAnalysis.Services;

/// <summary>时间粒度分组 key（成交量趋势/指数周月线共用）。</summary>
public static class DateGroupKeys
{
    public static string Key(DateTime date, string granularity) => granularity switch
    {
        "week" => Week(date),
        "month" => Month(date),
        "quarter" => Quarter(date),
        "year" => Year(date),
        _ => Day(date),
    };

    /// <summary>日：yyyy-MM-dd</summary>
    public static string Day(DateTime date) => date.ToString("yyyy-MM-dd");

    /// <summary>周：本周一的日期（避免跨年周计算歧义）</summary>
    public static string Week(DateTime date)
        => date.AddDays(-((int)date.DayOfWeek + 6) % 7).ToString("yyyy-MM-dd");

    /// <summary>月：yyyy-MM</summary>
    public static string Month(DateTime date) => date.ToString("yyyy-MM");

    /// <summary>季度：yyyy-Qn</summary>
    public static string Quarter(DateTime date) => $"{date:yyyy}-Q{(date.Month - 1) / 3 + 1}";

    /// <summary>年：yyyy</summary>
    public static string Year(DateTime date) => date.ToString("yyyy");
}
