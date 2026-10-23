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

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

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
    }
}
