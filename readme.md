# 基金/股票资金流分析系统

分析 A 股股票、ETF 与场外基金的**资金流入流出**、**近期收益亏损**、**小时/日/周/月趋势图**，内置**用户注册登录与角色权限管理**。

## 功能一览

| 模块 | 说明 |
|------|------|
| 标的搜索 | 代码 / 拼音 / 名称搜索（东方财富搜索接口，搜索即入库） |
| 多周期 K 线 | 小时（VIP）/ 日 / 周 / 月，涨红跌绿 + 成交量 |
| 资金流分析 | 主力 / 大单 / 中单 / 小单每日净流入，区间合计（VIP 图表，所有用户可见近5日主力汇总） |
| 收益统计 | 近1周 / 1月 / 3月 / 6月 / 今年以来收益率 + 最大回撤 |
| 场外基金 | 单位净值走势（日/周/月），无小时线与资金流（基金特性） |
| 账号体系 | 注册 / 登录 / JWT / 刷新令牌 / 修改密码 |
| 权限分级 | Admin（管理用户）/ VIP（小时线+资金流明细）/ User（日线） |
| 数据采集 | Hangfire 定时任务 + 后台队列，管理后台可手动触发 |

## 技术栈

- **后端**：C# / .NET 10 · ASP.NET Core Web API · EF Core + SQLite · ASP.NET Core Identity + JWT · Hangfire
- **前端**：Vue 3 + TypeScript · Element Plus · ECharts · Pinia · Vite
- **数据源**：东方财富公开接口（K线 / 资金流 / 基金净值 / 搜索），Tushare 适配器已留接口位

## 架构

```
前端 Vue3 + ECharts (localhost:5173)
   │ /api 代理
后端 ASP.NET Core (localhost:5200)
   ├─ Controllers   Auth / Quote / Watchlist / Admin
   ├─ Services      TokenService · AnalysisService(聚合/收益/回撤) · CollectorService(采集入库)
   ├─ Providers     IQuoteProvider → EastmoneyProvider(实现) / TushareProvider(预留)
   └─ Jobs          Hangfire 定时任务（也可在 /hangfire 面板手动触发）
   │
SQLite：fundanalysis.db（行情/用户/自选）
```

## 快速启动

环境要求：.NET SDK 10+、Node.js 18+。

```bash
# 1. 后端（首次启动自动建库、创建 admin 账号、后台采集示例标的数据）
cd backend/FundAnalysis
dotnet run
# Swagger:  http://localhost:5200/swagger
# Hangfire: http://localhost:5200/hangfire

# 2. 前端（另开一个终端）
cd frontend
npm install
npm run dev
# 打开 http://localhost:5173
```

初始账号：**admin / Admin@123**（登录后请在右上角修改密码）。

首次启动后示例标的（贵州茅台、平安银行、宁德时代、沪深300ETF、易方达消费行业基金）约 1 分钟内完成采集；自选列表为空属正常，等采集完成或点行进入分析页刷新即可。

## 使用指引

1. 注册一个普通账号体验权限分级，或直接用 admin 登录
2. 顶部搜索框输入代码/拼音/名称 → 选中即加入自选并自动采集
3. 点击自选行进入分析页：收益卡片、周期切换（小时/日/周/月）、K 线、资金流
4. 管理后台：修改用户角色（如把普通用户设为 VIP 解锁小时线与资金流图表）、启用/禁用用户、手动触发采集

## 常用接口

| 方法 | 路径 | 说明 | 权限 |
|------|------|------|------|
| POST | /api/auth/register · /api/auth/login | 注册 / 登录 | 匿名 |
| GET | /api/quote/search?keyword= | 标的搜索 | 匿名 |
| GET | /api/quote/kline?symbol=&period=&limit= | 多周期 K 线 | 登录（hour 需 VIP） |
| GET | /api/quote/performance?symbol= | 收益统计 | 登录 |
| GET | /api/quote/moneyflow?symbol=&days= | 资金流明细 | VIP |
| GET/POST/DELETE | /api/watchlist | 自选管理 | 登录 |
| GET | /api/admin/users 等 | 用户/角色/采集触发 | Admin |

## 多数据库支持

后端通过配置切换数据库，支持 **SQLite（默认）/ MySQL / PostgreSQL / Oracle / SQL Server**，对应 EF Core 官方提供程序包（`MySql.EntityFrameworkCore`、`Npgsql.EntityFrameworkCore.PostgreSQL`、`Oracle.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.SqlServer`）。

**切换步骤**（迁移文件与数据库绑定，切换后必须重新生成）：

```bash
cd backend/FundAnalysis
rm -rf Migrations                                  # 1. 删除旧迁移
dotnet ef migrations add Init                      # 2. 按当前配置的数据库重新生成
dotnet run                                         # 3. 启动时自动建表 + 初始化
```

`appsettings.json` 配置示例（改 `Database:Provider` 和连接串即可）：

```jsonc
{
  "Database": { "Provider": "mysql" },
  "ConnectionStrings": {
    // mysql:      "Server=localhost;Port=3306;Database=fund_analysis;User=root;Password=xxx;"
    // postgresql: "Host=localhost;Port=5432;Database=fund_analysis;Username=postgres;Password=xxx"
    // oracle:     "User Id=fund;Password=xxx;Data Source=//localhost:1521/XEPDB1"  （要求 Oracle 19c+）
    // sqlserver:  "Server=localhost;Database=FundAnalysis;User Id=sa;Password=xxx;TrustServerCertificate=True"
    // sqlite:     "Data Source=fundanalysis.db"
    "Default": "Server=localhost;Port=3306;Database=fund_analysis;User=root;Password=xxx;"
  }
}
```

也可以不改正配置文件，用命令行临时指定：`dotnet run -- --Database:Provider postgresql`。

已针对四种服务器数据库验证过模型到 DDL 的翻译（`dotnet ef dbcontext script`），包括各库的自增主键差异、PostgreSQL 的本地时间列（`timestamp without time zone`）适配、Oracle 的类型映射（`NUMBER`/`NVARCHAR2`，要求 19c+）。

## 说明与已知限制

- **数据源**：东方财富非官方公开接口，字段解析基于当前返回格式；全市场列表接口在部分网络环境不可用，系统已改为"搜索即注册"模式，不影响使用。接入 Tushare 时实现 `TushareProvider` 即可。
- **数据库**：开发默认 SQLite（零配置）；上线多用户建议 MySQL/PostgreSQL，见上文"多数据库支持"。
- **场外基金**：一天一个净值，没有小时线/资金流，前端已自动隐藏对应入口。
- **分钟线**：仅采集自选标的（控制体积），小时线由 5 分钟线聚合。
- Hangfire 用内存存储（定时任务每次启动重新注册）；需要任务历史持久化时换 MySQL 存储包。

## 目录结构

```
backend/FundAnalysis/     后端（Controllers / Services / Models / Data / Jobs / Migrations）
frontend/src/             前端（api / stores / router / views / components）
```
