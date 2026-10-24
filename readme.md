# 基金/股票资金流分析系统

分析 A 股股票、ETF 与场外基金的**资金流入流出**、**近期收益亏损**、**小时/日/周/月趋势图**，内置**用户注册登录与角色权限管理**。

## 功能一览

| 模块 | 说明 |
|------|------|
| 首页 Dashboard | 六大市场板块指数快照、今日两市成交量（趋势图）、北向/南向资金（双轴趋势图）、利率汇率三列（人民币汇率 / 十年期美债 / 中国国债 5·10·30 年期 + 中美利差）、自选概览、今日涨跌榜、央行流动性速览 |
| 指数页 | 15 个内置指数（A股宽基 / 风格与主题 / 跨境恒生·纳指·道指·标普500）：分组网格快照 + 30日迷你走势，点击查看详情；详情区 Tab 切换：**日K + MA5/10/20/30**（涨红跌绿）、**大数据量K线**（500 根可缩放）、**股市矩阵图**（treemap，色块=成交额、颜色=涨跌幅，点击切换指数）、**日内分时走势**（价格/均价/成交量，昨收基准线，昨收不在当日区间时价格轴自动断轴，每 60 秒自动刷新）；首页指数卡片点击直达 |
| 个股页 | 全市场 A 股浏览：沪市主板 / 科创板 / 深市主板 / 创业板 / 北交所 板块切换（带数量统计），代码/名称搜索 + 行业筛选，最新价/涨跌幅/估值/ROE/成长 列排序，点击进标的分析 |
| 成交量分析 | 两市成交量日/周/月/季度/年五种粒度（最近一年~全部历史），最新期/上期/环比/均值统计；首页成交量板块点击直达 |
| 选股器 | 全市场 A 股基本面筛选：ROE / 市盈率 / 市净率 / 净利率 / 毛利率 / 营收同比 / 净利同比 / 市值 / 行业 多条件组合 + 排序 + 分页，内置高ROE低估/高成长/消费白马/低估值蓝筹四个快速策略 |
| 自选提醒 | 价格上破/下破、日涨/跌超阈值四类规则，交易时段每 5 分钟检查，触发后自动停用并记录时间与价格；自选页创建与管理 |
| 自选分析 | 搜索添加自选、组合统计（涨跌分布/近1月平均收益/主力净流入合计）、近1月收益排行图、自选列表 |
| 标的分析 | K 线多周期（小时 VIP/日/周/月，涨红跌绿）、收益统计（近1周~今年以来+最大回撤）、资金流明细（主力/大单/中单/小单，VIP） |
| 场外基金 | 单位净值走势（日/周/月），无小时线与资金流（基金特性） |
| 央行流动性 | Shibor 走势（隔夜/1周/2周/1月）、货币供应 M0/M1/M2（同比与总量）、LPR 与存款准备金率历史 |
| 账号体系 | 注册 / 登录 / JWT / 刷新令牌 / 修改密码；Admin（管理用户）/ VIP（小时线+资金流）/ User（日线）三级权限 |
| 独立采集器 | FundAnalysis.Collector Docker 部署，once/daemon 双模式，服务器 cron 定时采集 |

## 技术栈

- **后端**：C# / .NET 10 · ASP.NET Core Web API · EF Core + SQLite · ASP.NET Core Identity + JWT · Hangfire
- **前端**：Vue 3 + TypeScript · Element Plus · ECharts · Pinia · Vite
- **数据源**：东方财富公开接口（K线 / 资金流 / 基金净值 / 搜索），Tushare 适配器已留接口位

## 架构

```
前端 Vue3 + ECharts (localhost:5173)
   │ /api 代理
Web API：FundAnalysis（认证 / 行情查询 / 分析 / 管理后台，自带单机采集调度）
   │ 引用
共享类库：FundAnalysis.Core（实体 / DbContext / 采集与分析服务 / 迁移）
   │ 引用
独立采集器：FundAnalysis.Collector（Docker 部署，once/daemon 双模式）
   │
数据库：SQLite（本机默认）/ MySQL / PostgreSQL / Oracle / SQL Server
```

Web 端与采集器**共用同一数据库与同一套代码**（Core 类库），两种部署形态：

- **单机模式（默认）**：只跑 Web，内置 Hangfire 定时采集，开箱即用
- **分离模式**：采集器部署在 Docker 服务器上（连 MySQL/PostgreSQL），Web 端设置 `"Collector:ScheduleEnabled": false` 关闭自带调度，避免重复采集

## 独立采集器部署（Docker 服务器）

采集器 [FundAnalysis.Collector](backend/FundAnalysis.Collector/) 支持两种运行模式：

| 模式 | 命令 | 说明 |
|------|------|------|
| once（默认） | `--jobs=daily` / `minutes` / `liquidity` / `initial` | 执行一次采集即退出（退出码 0/1），**配合服务器 crontab 定时调度** |
| daemon | `--mode=daemon` | 常驻进程，内部定时：交易日 16:35 全量、交易时段每 5 分钟分钟线 |

**第一步：构建镜像**（在 backend/ 目录）：

```bash
cd backend
docker build -f FundAnalysis.Collector/Dockerfile -t fund-collector .
```

**方式 A：docker compose 常驻部署**（MySQL + 采集器一键起）：

```bash
cd backend/FundAnalysis.Collector
# 修改 docker-compose.yml 里的数据库密码后
docker compose up -d
docker logs -f fund-collector-collector-1   # 查看采集日志
```

**方式 B：服务器 crontab 调度**（once 模式，最贴合"服务器定时任务"的用法）：

```bash
crontab -e   # 粘贴 backend/FundAnalysis.Collector/deploy/crontab.example 中的内容
# 交易日 16:35 全量采集；交易时段每 5 分钟分钟线；日志写入 /var/log/fund-collector.log
```

**Web 端配套修改**（分离模式必做）：

```jsonc
// Web 的 appsettings.json —— 连同一个数据库，并关闭 Web 自带采集调度
{
  "Database": { "Provider": "mysql" },
  "ConnectionStrings": { "Default": "Server=数据库地址;Port=3306;Database=fund_analysis;User=fund;Password=xxx;" },
  "Collector": { "ScheduleEnabled": false }
}
```

> 注意：分离部署必须用 MySQL/PostgreSQL 等网络数据库（SQLite 是本地文件，无法跨机器共享）；
> 切换数据库后需重新生成迁移（见"多数据库支持"）。
> 采集器首次运行会自动建表（与 Web 共用迁移），Web 首次启动会自动创建 admin 账号。

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
| GET | /api/quote/index-kline?code=&period=&limit= | 指数 K 线（day/week/month） | 登录 |
| GET | /api/quote/index-trend?code= | 指数日内分时走势（东财实时，1 分钟缓存） | 登录 |
| GET | /api/quote/performance?symbol= | 收益统计 | 登录 |
| GET | /api/quote/moneyflow?symbol=&days= | 资金流明细 | VIP |
| GET/POST/DELETE | /api/watchlist | 自选管理 | 登录 |
| GET | /api/liquidity/overview · shibor · money-supply · reserve-ratio · lpr | 央行流动性数据 | 登录 |
| GET | /api/admin/users 等 | 用户/角色/采集触发 | Admin |

## 多数据库支持

后端通过配置切换数据库，支持 **SQLite（默认）/ MySQL / PostgreSQL / Oracle / SQL Server**，对应 EF Core 官方提供程序包（`MySql.EntityFrameworkCore`、`Npgsql.EntityFrameworkCore.PostgreSQL`、`Oracle.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.SqlServer`）。

**切换步骤**（迁移文件与数据库绑定，切换后必须重新生成；迁移已位于 FundAnalysis.Core 项目）：

```bash
cd backend/FundAnalysis
rm -rf ../FundAnalysis.Core/Migrations               # 1. 删除旧迁移
dotnet ef migrations add Init --project ../FundAnalysis.Core   # 2. 以 Web 为启动项目按当前数据库重新生成
dotnet run                                           # 3. 启动时自动建表 + 初始化
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

- **数据源**：东方财富公开接口（默认），支持切换 Tushare Pro（见下文）；Tushare 适配器已完整实现
- **东财 IP 频率风控**：push2his 对同 IP 连续高频请求会触发断连惩罚（单次请求也会被断，持续数分钟到数小时）。行情/指数等批量数据一律走**低频定时采集入库**（指数同步逐个请求间隔 2 秒，连续失败 3 个自动熔断等 Hangfire 重试），页面查询全部走库。若首页/指数页显示"暂无数据"，多为风控期内采集尚未完成，等待自动重试即可；也可在管理后台手动触发"同步指数"。

## 数据源切换

默认使用东方财富。如东财风控频繁或需要更规范的数据，切换 Tushare Pro（需在 [tushare.pro](https://tushare.pro) 注册获取 token）：

```jsonc
// appsettings.json（或环境变量 Quote__Provider / Tushare__Token）
{
  "Quote": { "Provider": "tushare" },
  "Tushare": { "Token": "你的token" }
}
```

Tushare 适配器支持：标的搜索（本地缓存清单过滤）、日线（不复权）、资金流、基金净值。局限：分钟线/小时线仍需东财（Tushare 的分钟接口需较高积分），建议混合使用——tushare 为主、东财补分钟线，或反之。

## 生产部署

**一键打包（宝塔/任意 Linux）**：Windows 开发机 Git Bash 运行 `bash deploy/pack.sh`，产出 `publish/fundanalysis-*.tar.gz`（含 Linux 版 Web 后端 + 独立采集器 + 前端 + nginx 配置 + 宝塔部署说明，默认自包含免装运行时；`bash deploy/pack.sh slim` 为小包需服务器装 .NET 10）。宝塔面板具体操作见 `deploy/baota.md`。

后端 `deploy/` 目录另提供了直接部署的配置示例：

| 文件 | 用途 |
|------|------|
| `nginx.conf` | 前端静态托管 + `/api` 反代 + Hangfire 面板内网限制 |
| `fundanalysis.service` | 后端 systemd 服务（含全部生产环境变量） |

部署要点：

1. **前端**：`npm run build` 后将 `dist/` 放到 nginx root（SPA 路由已配 try_files 回退）
2. **后端**：`dotnet publish -c Release` 后按 `fundanalysis.service` 跑 systemd；**生产配置全部用环境变量覆盖**（`Jwt__Key`、`Database__Provider`、`ConnectionStrings__Default`、`Cors__Origins__0` 等）
3. **安全项（已内置强制）**：生产环境若 JWT 密钥仍是默认值会**拒绝启动**；Swagger 默认仅开发环境开放（可用 `Swagger:Enabled` 强制开启）；CORS 生产必须配置 `Cors:Origins` 白名单（同域部署可不配）
4. **采集**：分离部署见上文"独立采集器"章节（Web 端 `Collector:ScheduleEnabled=false`）
5. HTTPS：`sudo certbot --nginx -d 你的域名`
- **央行流动性**：东财数据中心未提供公开市场操作（OMO 逆回购/MLF 明细）报表，中国货币网接口也无法稳定访问，故以 Shibor（资金面松紧的直接市场指标）+ 货币供应 + 政策利率（LPR/存准率）刻画流动性现状；后续如需 OMO 净投放明细可接入央行官网公告解析。
- **数据库**：开发默认 SQLite（零配置）；上线多用户建议 MySQL/PostgreSQL，见上文"多数据库支持"。
- **Hangfire 存储**：按数据库自动选择——MySQL（Hangfire.MySqlStorage）/ PostgreSQL（Hangfire.PostgreSql）持久化任务历史；SQLite 开发环境用内存存储（定时任务每次启动重新注册）。
- **测试**：`dotnet test backend/FundAnalysis.Tests` —— 覆盖周/月聚合、区间收益、最大回撤、日期粒度 key 等 17 项。
- **场外基金**：一天一个净值，没有小时线/资金流，前端已自动隐藏对应入口。
- **分钟线**：仅采集自选标的（控制体积），小时线由 5 分钟线聚合。
- Hangfire 用内存存储（定时任务每次启动重新注册）；需要任务历史持久化时换 MySQL 存储包。

## 目录结构

```
backend/FundAnalysis.Core/       共享类库：Models / Data(实体+DbContext+迁移) / Services(采集+分析) / Jobs
backend/FundAnalysis/            Web API：Controllers + Program.cs（认证/查询/管理后台）
backend/FundAnalysis.Collector/  独立采集器：once/daemon 双模式 + Dockerfile + docker-compose + cron 示例
frontend/src/                    前端：api / stores / router / views / components
```
