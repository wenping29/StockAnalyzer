// SQLite → PostgreSQL 数据搬迁工具
// 用法（在 backend/DataMigrator 下）:
//   dotnet run -- --source "Data Source=../FundAnalysis/fundanalysis.db" \
//                --target "Host=...;Port=5454;Database=fund_analysis;Username=postgres;Password=xxx"
//   dotnet run -- --diag   （参数绑定诊断）
using System.Data;
using System.Text;
using Microsoft.Data.Sqlite;
using Npgsql;
using NpgsqlTypes;

// 与主应用一致的 PG 时间行为（本地时间直写）
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// --syncseq：迁移后重置全部 IDENTITY 序列到当前最大 Id（否则应用插入新行会主键冲突）
if (args.Contains("--syncseq"))
{
    var seqConn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await seqConn.OpenAsync();
    await using var findCmd = new NpgsqlCommand(
        @"SELECT c.table_name, c.column_name
          FROM information_schema.columns c
          JOIN information_schema.tables t ON t.table_name = c.table_name AND t.table_schema='public'
          WHERE c.table_schema='public' AND c.is_identity = 'YES'", seqConn);
    await using var fr = await findCmd.ExecuteReaderAsync();
    var idCols = new List<(string Table, string Col)>();
    while (await fr.ReadAsync()) idCols.Add((fr.GetString(0), fr.GetString(1)));
    fr.Close();

    foreach (var (t, c) in idCols)
    {
        try
        {
            var sql = $"SELECT setval(pg_get_serial_sequence('\"{t}\"', '{c}'), COALESCE((SELECT MAX(\"{c}\") FROM \"{t}\"), 0) + 1, false)";
            await using var fix = new NpgsqlCommand(sql, seqConn);
            var v = await fix.ExecuteScalarAsync();
            Console.WriteLine($"{t,-22} 序列已推进到 {v}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{t,-22} 序列推进失败: {ex.Message}");
        }
    }
    return 0;
}

// --codes：检查 IndexDailies 指数代码分布
if (args.Contains("--codes"))
{
    var cconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await cconn.OpenAsync();
    var ccmd = new NpgsqlCommand("SELECT \"Code\", COUNT(*) FROM \"IndexDailies\" GROUP BY \"Code\"", cconn);
    await using (var cr = await ccmd.ExecuteReaderAsync())
    {
        while (await cr.ReadAsync())
            Console.WriteLine($"{cr.GetString(0)}  {cr.GetInt64(1)} 根");
    }
    await using (var c2 = new NpgsqlCommand("SELECT COUNT(*) FROM \"IndexDailies\" WHERE \"Code\"='399106'", cconn))
        Console.WriteLine($"399106 精确查询: {await c2.ExecuteScalarAsync()} 行");
    // 模拟应用的成交量查询
    await using (var c3 = new NpgsqlCommand(
        "SELECT COUNT(*), COUNT(DISTINCT \"Date\") FROM \"IndexDailies\" WHERE \"Code\" IN ('000001', '399106')", cconn))
    await using (var r3 = await c3.ExecuteReaderAsync())
    {
        if (await r3.ReadAsync())
            Console.WriteLine($"应用查询(000001+399106): {r3.GetInt64(0)} 行 / {r3.GetInt64(1)} 个不同日期");
    }
    // 看日期边界
    await using (var c4 = new NpgsqlCommand(
        "SELECT \"Code\", \"Date\"::text FROM \"IndexDailies\" ORDER BY \"Date\" DESC LIMIT 3", cconn))
    await using (var r4 = await c4.ExecuteReaderAsync())
    {
        while (await r4.ReadAsync())
            Console.WriteLine($"最新: {r4.GetString(0)} @ {r4.GetString(1)}");
    }
    return 0;
}

// --seed399：向 fund_db 插入 399106 深证综指 30 根测试日线（成交量联调用）
if (args.Contains("--seed399"))
{
    var sconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await sconn.OpenAsync();
    await using var tx = await sconn.BeginTransactionAsync();
    var added = 0;
    for (var i = 0; i < 30; i++)
    {
        var date = DateTime.Parse("2026-09-10").AddBusinessDaysLike(i);
        await using var c = new NpgsqlCommand(
            @"INSERT INTO ""IndexDailies"" (""Code"",""Market"",""Date"",""Open"",""High"",""Low"",""Close"",""Volume"",""Amount"",""ChangePct"")
              VALUES ('399106',0,@d,2200,2260,2180,2230,@v,@a,0.5) ON CONFLICT DO NOTHING", sconn, tx);
        c.Parameters.AddWithValue("d", date);
        c.Parameters.AddWithValue("v", 400000000.0 + i * 10000000);
        c.Parameters.AddWithValue("a", 800000000000.0 + i * 20000000000);
        added += await c.ExecuteNonQueryAsync();
    }
    await using (var idx = new NpgsqlCommand(
        "SELECT indexname, indexdef FROM pg_indexes WHERE tablename='IndexDailies'", sconn))
    await using (var ir = await idx.ExecuteReaderAsync())
    {
        while (await ir.ReadAsync())
            Console.WriteLine($"索引: {ir.GetString(1)}");
    }

    await using (var test = new NpgsqlCommand(
        @"INSERT INTO ""IndexDailies"" (""Code"",""Market"",""Date"",""Open"",""High"",""Low"",""Close"",""Volume"",""Amount"",""ChangePct"")
          VALUES ('399106',0,'2026-10-23',2200,2260,2180,2230,400000000,800000000000,0.5)", sconn, tx))
    {
        var n = await test.ExecuteNonQueryAsync();
        Console.WriteLine($"直接插入(无ON CONFLICT): {n} 行");
    }
    await tx.RollbackAsync(); // 测试数据回滚，不污染
    Console.WriteLine("已回滚");
    return 0;
}

if (args.Contains("--list"))
{
    var lconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await lconn.OpenAsync();
    var lcmd = new NpgsqlCommand("SELECT table_name FROM information_schema.tables WHERE table_schema='public' ORDER BY table_name", lconn);
    await using var lr = await lcmd.ExecuteReaderAsync();
    var names = new List<string>();
    while (await lr.ReadAsync()) names.Add(lr.GetString(0));
    Console.WriteLine($"PG 库共 {names.Count} 张表:\n{string.Join(", ", names)}");
    return 0;
}

// --history：只补写迁移历史标记（表已由 SQL 建好时用）
if (args.Contains("--history"))
{
    var hconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await hconn.OpenAsync();
    await using var hc = new NpgsqlCommand(
        @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (""MigrationId"" text NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY, ""ProductVersion"" text NOT NULL);
          INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES ('20261024041644_Init', '10.0.13') ON CONFLICT DO NOTHING;", hconn);
    await hc.ExecuteNonQueryAsync();
    Console.WriteLine("迁移历史已写入");
    return 0;
}

// --init：执行建表 SQL（deploy/init-schema-postgresql.sql）在空库中创建全部表结构
if (args.Contains("--init"))
{
    var schemaPath = Path.Combine(Environment.CurrentDirectory, "../../FundAnalysis/deploy/init-schema-postgresql.sql");
    if (!File.Exists(schemaPath)) schemaPath = Path.Combine(Environment.CurrentDirectory, "init-schema-postgresql.sql");
    var iconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await iconn.OpenAsync();
    var script = await File.ReadAllTextAsync(schemaPath);
    var statements = script.Split(";\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var done = 0;
    foreach (var stmt in statements)
    {
        if (string.IsNullOrWhiteSpace(stmt)) continue;
        await using var c = new NpgsqlCommand(stmt, iconn);
        await c.ExecuteNonQueryAsync();
        done++;
    }
    Console.WriteLine($"建表 SQL 执行完成，共 {done} 条语句");

    // 标记迁移已应用（避免应用启动时重复建表冲突）
    var migrationId = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj").Length >= 0
        ? "20261024041644_Init" // 与 deploy/init-schema-postgresql.sql 对应的迁移版本
        : "";
    await using var hc = new NpgsqlCommand(
        @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (""MigrationId"" text NOT NULL CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY, ""ProductVersion"" text NOT NULL);
          INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"") VALUES (@mid, '10.0.13') ON CONFLICT DO NOTHING;", iconn);
    hc.Parameters.AddWithValue("mid", migrationId);
    await hc.ExecuteNonQueryAsync();
    Console.WriteLine($"已写入迁移历史: {migrationId}");
    return 0;
}

if (args.Contains("--diag"))
{
    var dconn = new NpgsqlConnection("Host=43.142.147.244;Port=5454;Database=fund_db;Username=fundmanager;Password=admin123456");
    await dconn.OpenAsync();
    var c1 = new NpgsqlCommand("SELECT $1::text AS v", dconn);
    c1.Parameters.Add(new NpgsqlParameter { ParameterName = "1", Value = "hello" });
    try { Console.WriteLine("位置参数($1): " + await c1.ExecuteScalarAsync()); }
    catch (Exception ex) { Console.WriteLine("位置参数失败: " + ex.Message); }
    var c2 = new NpgsqlCommand("SELECT @p::text AS v", dconn);
    c2.Parameters.AddWithValue("p", "world");
    try { Console.WriteLine("命名参数(@p): " + await c2.ExecuteScalarAsync()); }
    catch (Exception ex) { Console.WriteLine("命名参数失败: " + ex.Message); }
    return 0;
}

var argsMap = new Dictionary<string, string>();
for (var i = 0; i < args.Length - 1; i += 2)
    argsMap[args[i].TrimStart('-')] = args[i + 1];

var source = argsMap.GetValueOrDefault("source", "Data Source=../FundAnalysis/fundanalysis.db");
var target = argsMap.GetValueOrDefault("target")
    ?? throw new ArgumentException("缺少 --target 连接串");

// 迁移顺序：先主/引用表后子表（表名 = EF 建的实际复数表名）
var tables = new[]
{
    "AspNetRoles", "AspNetUsers", "AspNetUserRoles", "AspNetUserClaims",
    "StockBasics", "AspNetUserLogins", "AspNetUserTokens",
    "StockDailies", "StockMinutes", "FundNavs", "MoneyFlowDailies",
    "WatchlistItems", "ShiborRates", "MoneySupplies", "ReserveRatios",
    "LprRates", "IndexDailies", "StockFundamentals", "AlertRules",
};

await using var src = new SqliteConnection(source);
await using var dst = new NpgsqlConnection(target);
await src.OpenAsync();
await dst.OpenAsync();

var totalRows = 0L;
foreach (var table in tables)
{
    var rows = await CopyTableAsync(src, dst, table);
    totalRows += rows;
    Console.WriteLine($"{table,-22} {rows,7} 行");
}

Console.WriteLine($"搬迁完成，共 {totalRows} 行");

static async Task<long> CopyTableAsync(SqliteConnection src, NpgsqlConnection dst, string table)
{
    // 目标列定义（名称+类型，按序）
    var cols = new List<(string Name, string DataType)>();
    await using (var cmd = new NpgsqlCommand(
        @"SELECT column_name, data_type FROM information_schema.columns
          WHERE table_schema='public' AND table_name=@t ORDER BY ordinal_position", dst))
    {
        cmd.Parameters.AddWithValue("t", table);
        await using var colReader = await cmd.ExecuteReaderAsync();
        while (await colReader.ReadAsync())
            cols.Add((colReader.GetString(0), colReader.GetString(1)));
    }
    if (cols.Count == 0)
    {
        Console.WriteLine($"{table,-22} 目标表不存在，跳过");
        return 0;
    }

    // 源数据
    await using var select = new SqliteCommand($"SELECT * FROM \"{table}\"", src);
    await using var reader = await select.ExecuteReaderAsync();
    if (!reader.HasRows) return 0;

    var colNames = cols.Select(c => $"\"{c.Name}\"").ToList();
    var placeholders = cols.Select((c, i) => $"@c{i}").ToList();
    var insertSql = new StringBuilder($"INSERT INTO \"{table}\" ({string.Join(",", colNames)}) VALUES ({string.Join(",", placeholders)})");

    await using var tx = await dst.BeginTransactionAsync();
    long count = 0;
    try
    {
        await using (var del = new NpgsqlCommand($"DELETE FROM \"{table}\"", dst, tx))
            await del.ExecuteNonQueryAsync();

        await using var insert = new NpgsqlCommand(insertSql.ToString(), dst, tx);
        var parameters = cols.Select((c, i) =>
        {
            var p = new NpgsqlParameter { ParameterName = $"c{i}", NpgsqlDbType = MapDbType(c.DataType) };
            insert.Parameters.Add(p);
            return p;
        }).ToArray();

        while (await reader.ReadAsync())
        {
            for (var i = 0; i < cols.Count; i++)
            {
                var (name, dataType) = cols[i];
                var raw = reader[name] is DBNull ? null : reader[name];
                parameters[i].Value = ConvertFor(raw, dataType);
            }
            await insert.ExecuteNonQueryAsync();
            count++;
        }

        await tx.CommitAsync();
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
    return count;
}

// SQLite 动态类型 → PG 目标列类型
static object? ConvertFor(object? value, string pgType) => value switch
{
    null or DBNull => DBNull.Value,
    long n => pgType switch
    {
        "integer" => (int)n,
        "smallint" => (short)n,
        "bigint" => n,
        "double precision" or "real" => (double)n,
        "boolean" => n != 0,
        "timestamp with time zone" or "timestamp without time zone" or "date" => DateTime.FromBinary(n),
        "text" or "character varying" => n.ToString(),
        _ => n,
    },
    double d => pgType switch
    {
        "integer" => (int)d,
        "bigint" => (long)d,
        "double precision" or "real" => d,
        "text" => d.ToString(),
        _ => d,
    },
    string str => pgType.StartsWith("timestamp") || pgType == "date"
        ? DateTime.Parse(str)
        : str,
    _ => value,
};

// PG 列类型 → NpgsqlDbType（显式指定，null 值也可绑定）
static NpgsqlTypes.NpgsqlDbType MapDbType(string pgType) => pgType switch
{
    "bigint" => NpgsqlTypes.NpgsqlDbType.Bigint,
    "integer" => NpgsqlTypes.NpgsqlDbType.Integer,
    "smallint" => NpgsqlTypes.NpgsqlDbType.Smallint,
    "double precision" or "real" => NpgsqlTypes.NpgsqlDbType.Double,
    "numeric" => NpgsqlTypes.NpgsqlDbType.Numeric,
    "boolean" => NpgsqlTypes.NpgsqlDbType.Boolean,
    "timestamp with time zone" => NpgsqlTypes.NpgsqlDbType.TimestampTz,
    "timestamp without time zone" => NpgsqlTypes.NpgsqlDbType.Timestamp,
    "date" => NpgsqlTypes.NpgsqlDbType.Date,
    "jsonb" => NpgsqlTypes.NpgsqlDbType.Jsonb,
    _ => NpgsqlTypes.NpgsqlDbType.Text,
};

return 0;

internal static class BusinessDayExt
{
    public static DateTime AddBusinessDaysLike(this DateTime start, int days)
    {
        var d = start;
        var left = days;
        while (left > 0) { d = d.AddDays(1); if (d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) left--; }
        return d;
    }
}
