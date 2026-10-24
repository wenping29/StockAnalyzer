using FundAnalysis.Data;
using FundAnalysis.Models;
using Hangfire;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Services;

/// <summary>
/// 自选价格提醒：检查所有启用规则的当前价格（优先分钟线最新价，回退日线收盘价），
/// 满足条件则记录触发时间并停用规则（一次性触发，避免重复打扰；用户可重新启用）。
/// </summary>
public class AlertService(AppDbContext db, ILogger<AlertService> logger)
{
    public static readonly string[] RuleTypes = ["PriceAbove", "PriceBelow", "ChangePctUp", "ChangePctDown"];

    /// <summary>用户的提醒规则列表（含标的名称）。</summary>
    public async Task<List<AlertRule>> GetAsync(string userId)
        => await db.AlertRules
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

    public async Task<AlertRule> AddAsync(string userId, string symbol, string ruleType, double threshold)
    {
        if (!RuleTypes.Contains(ruleType))
            throw new ArgumentException($"规则类型只能是 {string.Join("/", RuleTypes)}");

        var symbolExists = await db.StockBasics.AnyAsync(x => x.Symbol == symbol);
        if (!symbolExists)
            throw new ArgumentException($"标的 {symbol} 不存在");

        var rule = new AlertRule
        {
            UserId = userId,
            Symbol = symbol,
            RuleType = ruleType,
            Threshold = threshold,
        };
        db.AlertRules.Add(rule);
        await db.SaveChangesAsync();
        return rule;
    }

    public async Task<bool> ToggleAsync(string userId, long id)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (rule is null) return false;
        rule.Enabled = !rule.Enabled;
        if (rule.Enabled) rule.LastTriggeredAt = null; // 重新启用清空触发记录
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string userId, long id)
    {
        var rule = await db.AlertRules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (rule is null) return false;
        db.AlertRules.Remove(rule);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>检查全部启用规则（Hangfire 每 5 分钟，交易时段）。返回触发条数。</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    public async Task<int> CheckAllAsync()
    {
        var rules = await db.AlertRules.Where(x => x.Enabled).ToListAsync();
        if (rules.Count == 0) return 0;

        var symbols = rules.Select(r => r.Symbol).Distinct().ToList();

        // 最新价：分钟线最新 → 日线最新
        var minutePrices = await db.StockMinutes
            .Where(x => symbols.Contains(x.Symbol))
            .GroupBy(x => x.Symbol)
            .Select(g => new { Symbol = g.Key, Price = g.OrderByDescending(m => m.Time).First().Close })
            .ToDictionaryAsync(x => x.Symbol, x => (double?)x.Price);
        var dailyPrices = await db.StockDailies
            .Where(x => symbols.Contains(x.Symbol))
            .GroupBy(x => x.Symbol)
            .Select(g => new { Symbol = g.Key, Price = g.OrderByDescending(d => d.Date).First().Close, Chg = g.OrderByDescending(d => d.Date).First().ChangePct })
            .ToDictionaryAsync(x => x.Symbol, x => (Price: (double?)x.Price, x.Chg));

        var triggered = 0;
        foreach (var rule in rules)
        {
            var price = minutePrices.GetValueOrDefault(rule.Symbol) ?? dailyPrices.GetValueOrDefault(rule.Symbol).Price;
            if (price is not { } p) continue;
            var chg = dailyPrices.GetValueOrDefault(rule.Symbol).Chg;

            var hit = rule.RuleType switch
            {
                "PriceAbove" => p >= rule.Threshold,
                "PriceBelow" => p <= rule.Threshold,
                "ChangePctUp" => chg is { } c && c >= rule.Threshold,
                "ChangePctDown" => chg is { } c2 && c2 <= -rule.Threshold,
                _ => false,
            };
            if (!hit) continue;

            rule.LastTriggeredAt = DateTime.Now;
            rule.LastTriggeredPrice = p;
            rule.Enabled = false;
            triggered++;
            logger.LogInformation("提醒触发：用户 {UserId} {Symbol} {RuleType} 阈值 {Threshold}，现价 {Price}",
                rule.UserId, rule.Symbol, rule.RuleType, rule.Threshold, p);
        }

        if (triggered > 0)
            await db.SaveChangesAsync();
        return triggered;
    }
}
