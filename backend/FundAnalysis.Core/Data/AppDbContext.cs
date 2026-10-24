using FundAnalysis.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FundAnalysis.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<StockBasic> StockBasics => Set<StockBasic>();
    public DbSet<StockDaily> StockDailies => Set<StockDaily>();
    public DbSet<StockMinute> StockMinutes => Set<StockMinute>();
    public DbSet<FundNav> FundNavs => Set<FundNav>();
    public DbSet<MoneyFlowDaily> MoneyFlowDailies => Set<MoneyFlowDaily>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<ShiborRate> ShiborRates => Set<ShiborRate>();
    public DbSet<MoneySupply> MoneySupplies => Set<MoneySupply>();
    public DbSet<ReserveRatio> ReserveRatios => Set<ReserveRatio>();
    public DbSet<LprRate> LprRates => Set<LprRate>();
    public DbSet<IndexDaily> IndexDailies => Set<IndexDaily>();
    public DbSet<StockFundamental> StockFundamentals => Set<StockFundamental>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Npgsql 默认把 DateTime 映射为 timestamp with time zone（要求 UTC 时间），
        // 本系统统一使用本地时间（DateTime.Now），改为 timestamp without time zone 保持行为与其他库一致
        if (Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL")
        {
            foreach (var entityType in b.Model.GetEntityTypes())
                foreach (var property in entityType.GetProperties())
                    if (property.ClrType == typeof(DateTime))
                        property.SetColumnType("timestamp without time zone");
        }

        b.Entity<StockBasic>(e =>
        {
            e.HasIndex(x => x.Symbol).IsUnique();
            e.Property(x => x.Symbol).HasMaxLength(12);
            e.Property(x => x.Name).HasMaxLength(40);
        });

        b.Entity<StockDaily>(e =>
        {
            e.HasIndex(x => new { x.Symbol, x.Date }).IsUnique();
            e.HasIndex(x => x.Symbol);
        });

        b.Entity<StockMinute>(e =>
        {
            e.HasIndex(x => new { x.Symbol, x.Time }).IsUnique();
            e.HasIndex(x => x.Symbol);
        });

        b.Entity<FundNav>(e =>
        {
            e.HasIndex(x => new { x.FundCode, x.Date }).IsUnique();
        });

        b.Entity<MoneyFlowDaily>(e =>
        {
            e.HasIndex(x => new { x.Symbol, x.Date }).IsUnique();
        });

        b.Entity<WatchlistItem>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Symbol }).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ShiborRate>(e =>
        {
            e.HasIndex(x => new { x.Period, x.Date }).IsUnique();
            e.HasIndex(x => x.Date);
        });

        b.Entity<MoneySupply>(e =>
        {
            e.HasIndex(x => x.Month).IsUnique();
        });

        b.Entity<ReserveRatio>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
        });

        b.Entity<LprRate>(e =>
        {
            e.HasIndex(x => x.Date).IsUnique();
        });

        b.Entity<IndexDaily>(e =>
        {
            e.HasIndex(x => new { x.Code, x.Date }).IsUnique();
            e.HasIndex(x => x.Code);
        });

        b.Entity<StockFundamental>(e =>
        {
            e.HasIndex(x => x.Symbol).IsUnique();
            e.HasIndex(x => x.Industry);
        });

        b.Entity<AlertRule>(e =>
        {
            e.HasIndex(x => x.UserId);
            e.HasIndex(x => new { x.UserId, x.Symbol });
        });
    }
}
