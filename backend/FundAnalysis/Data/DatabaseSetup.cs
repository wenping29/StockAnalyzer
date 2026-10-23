using Microsoft.EntityFrameworkCore;
using MySql.EntityFrameworkCore.Extensions;
using Oracle.EntityFrameworkCore;

namespace FundAnalysis.Data;

/// <summary>
/// 数据库提供程序切换。在 appsettings.json 配置：
///   "Database": { "Provider": "sqlite | mysql | postgresql | oracle | sqlserver" }
/// 并把 ConnectionStrings:Default 换成对应数据库的连接串。
/// 注意：迁移文件与提供程序绑定，切换数据库后需删除 Migrations 目录重新生成（见 README）。
/// </summary>
public static class DatabaseSetup
{
    public static readonly string[] SupportedProviders =
        ["sqlite", "mysql", "postgresql", "oracle", "sqlserver"];

    public static void UseDatabase(this DbContextOptionsBuilder options, IConfiguration config)
    {
        var provider = (config["Database:Provider"] ?? "sqlite").Trim().ToLowerInvariant();
        var connectionString = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("缺少连接串：请在 appsettings.json 配置 ConnectionStrings:Default");

        switch (provider)
        {
            case "sqlite":
                options.UseSqlite(connectionString);
                break;

            case "mysql":
                // MySQL 官方包（MySql.EntityFrameworkCore），8.0+ 均可
                options.UseMySQL(connectionString);
                break;

            case "postgresql":
            case "npgsql":
                options.UseNpgsql(connectionString);
                break;

            case "oracle":
                // Oracle 官方 EF Core 10 提供程序要求 Oracle Database 19c 及以上
                options.UseOracle(connectionString, o => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19));
                break;

            case "sqlserver":
                options.UseSqlServer(connectionString);
                break;

            default:
                throw new InvalidOperationException(
                    $"不支持的数据库提供程序 \"{provider}\"，可选：{string.Join(" / ", SupportedProviders)}");
        }
    }
}
