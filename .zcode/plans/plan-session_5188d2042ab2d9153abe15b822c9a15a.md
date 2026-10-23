# 基金/股票资金流分析系统 — 实施计划

技术栈（已确认）：**C# / .NET 10 + ASP.NET Core Web API + EF Core + SQLite（后续可换 MySQL）+ Identity/JWT + Hangfire + Vue3/TS + Element Plus + ECharts**，数据源东方财富公开接口先行、Tushare 留适配位。

## 一、目录结构

```
F:\Project\Zcode\
├─ README.md                       # 中文说明：环境准备、启动步骤、账号、架构图
├─ backend\
│  ├─ FundAnalysis.sln
│  └─ FundAnalysis\                # net10.0 Web API 单项目（文件夹分层，新手友好）
│     ├─ Program.cs                # DI 装配：EF Core + Identity + JWT + Hangfire + CORS
│     ├─ appsettings.json          # SQLite 连接串、JWT 密钥、东财/Tushare 配置
│     ├─ Controllers\  Auth / Quote / Watchlist / Admin
│     ├─ Models\       实体类（见下）
│     ├─ Data\         AppDbContext、DbSeeder（初始管理员+角色）
│     ├─ Services\
│     │   ├─ Providers\ IQuoteProvider 接口 + EastmoneyProvider(实现) + TushareProvider(预留)
│     │   ├─ CollectorService.cs   # 拉数入库，幂等 upsert
│     │   └─ AnalysisService.cs    # 周期聚合、区间收益、最大回撤、资金流统计
│     └─ Jobs\         CollectorJobs.cs（Hangfire 任务注册）
└─ frontend\                       # Vite + Vue3 + TS + Element Plus + ECharts + Pinia
   └─ src\ views\ Login / Dashboard / Analysis / Admin
          components\ KLineChart / MoneyFlowChart / TrendChart
```

## 二、数据模型（EF Core 实体 → SQLite 表）

| 实体 | 关键字段 | 唯一索引 |
|------|---------|---------|
| AppUser | Identity 用户 | — |
| StockBasic | Symbol、Name、Market、Type(股票/ETF/场外基金) | Symbol |
| StockDaily | OHLC、Volume、Amount、ChangePct | Symbol+Date |
| StockMinute | OHLC、Volume（仅自选标的，控制体积） | Symbol+Time |
| FundNav | FundCode、单位净值、累计净值、ChangePct | Code+Date |
| MoneyFlowDaily | 主力/大单/中单/小单 净流入额 | Symbol+Date |
| WatchlistItem | UserId、Symbol | UserId+Symbol |

角色三级：**Admin（管理用户）/ VIP（分钟线+资金流明细）/ User（日线级）**，用 Identity Role + `[Authorize(Roles=...)]` 控制。

## 三、后端要点

1. **认证**：Identity + JWT（access token + refresh token），注册/登录接口，Swagger 开启 Bearer 认证方便调试；DbSeeder 初始 `admin` 账号和三个角色。
2. **采集（EastmoneyProvider，HttpClient 直调）**：
   - 全市场标的列表：`push2.eastmoney.com/api/qt/clist/get` → 初始化 StockBasic
   - 日K/分钟K：`push2his.eastmoney.com/api/qt/stock/kline/get`（klt=101/5）
   - 资金流：`push2.eastmoney.com/api/qt/stock/fflow/kline/get`
   - 基金历史净值：`api.fund.eastmoney.com/f10/lsjz`（带 Referer 头）
   - TushareProvider 只建骨架和 token 配置位，不实现
3. **Hangfire 任务**：每交易日 16:30 拉日线/净值/日资金流；交易时段每 5 分钟拉自选 5 分钟线；Dashboard（`/hangfire`，仅 Admin）支持手动触发，首次跑数据不用等定时。
4. **聚合与计算（查询时实时算，SQLite 单机量级足够；量大后再加预聚合）**：分钟线→小时线；日线→周线/月线（GROUP BY 周/月）；区间收益（近1周/1月/3月/6月/YTD）、最大回撤、累计资金流净额。
5. **主要 API**：
   - `POST /api/auth/register|login`、`GET /api/auth/me`
   - `GET /api/quote/search?keyword=` 标的搜索
   - `GET /api/quote/kline?symbol=&period=hour|day|week|month&limit=`
   - `GET /api/quote/moneyflow?symbol=&days=`（VIP+）
   - `GET /api/quote/performance?symbol=` 收益统计卡片数据
   - `GET|POST|DELETE /api/watchlist` 自选管理
   - `GET /api/admin/users`、`PUT /api/admin/users/{id}/role`（Admin）

## 四、前端要点

- 登录/注册页（tab 切换），路由守卫 + Pinia 存 token
- **Dashboard**：自选列表表格（最新价、涨跌幅、近1月收益、主力净流入），顶部搜索添加标的
- **AnalysisView（核心页）**：收益统计卡片行 + 周期 tab（小时|日|周|月）K线图（涨红跌绿 + 成交量）+ 资金流柱状图（主力/大/中/小单，近5/20/60日切换）；场外基金自动隐藏"小时"tab（基金无小时数据）
- **AdminView**：用户列表、改角色
- vite proxy `/api` → 后端，避免 CORS

## 五、实施顺序（5 步，每步可验证）

1. 后端骨架：解决方案 + Identity + JWT + Swagger + 建库迁移 + seed → 注册登录可跑通
2. 采集：EastmoneyProvider + Collector + Hangfire → 手动触发，库里有真实日K/资金流/净值
3. 分析 API：kline 聚合 / performance / moneyflow / watchlist → Swagger 逐一验证
4. 前端：脚手架 + 登录 + Dashboard + AnalysisView 三组件接真实接口
5. AdminView + README + 端到端验证（注册→登录→加自选→采集→看图）

## 六、验证方式

- `dotnet build` 零警告通过；启动后 Swagger 手测全部接口返回真实东财数据
- 前端 `npm run dev`，浏览器走完整流程；K线/资金流/趋势图正常渲染
- Hangfire 面板可见任务与执行历史

## 七、完成后你需要做的

```
后端: cd backend/FundAnalysis && dotnet run     (https://localhost:5xxx, Swagger 见控制台地址)
前端: cd frontend && npm install && npm run dev (http://localhost:5173)
初始账号: admin / Admin@123 (登录后请改密)
```
