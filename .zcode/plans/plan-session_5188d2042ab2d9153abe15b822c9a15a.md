# 续接计划（已完成约 60%，继续执行已批准的原计划）

## 当前进度
- ✅ 后端骨架：.NET 10 + Identity/JWT + Swagger + EF 迁移 + seed（admin 可登录，注册可用）
- ✅ 采集改造：东财全市场列表接口在当前网络被风控拦截，已改为"搜索即注册"（searchapi suggest 接口，可用）
- ✅ 4 只股票示例标的日线各 500 根已入库；K线/资金流接口实测可用
- ❌ 基金净值接口（api.fund.eastmoney.com/f10/lsjz）返回 0 条，待修复

## 剩余步骤

1. **修复基金净值采集**：直接请求 lsjz 接口对比真实响应结构（可能是 Referer/字段解析问题），必要时换用东财基金 pingzhongdata 数据源；修好后验证 110022 净值入库。
2. **验证分析 API**：kline（日/周/月/小时聚合）、performance（区间收益+最大回撤）、moneyflow（VIP 权限）、watchlist（添加自选触发后台采集）逐一用 curl + admin token 实测。
3. **前端**（目录已建，代码未写）：Vue3+Vite+TS+Element Plus+ECharts+Pinia
   - 登录/注册页、路由守卫
   - Dashboard：搜索添加自选 + 概览表格（最新价/涨跌幅/近1月收益/主力净流入）
   - AnalysisView：收益统计卡片 + 周期 tab（小时[VIP]/日/周/月）+ K线图（涨红跌绿）+ 资金流柱状图（亿元）；场外基金自动隐藏小时 tab 和资金流
   - AdminView：用户列表/改角色/启停 + 手动采集按钮
   - vite proxy /api → localhost:5200
4. **README**：架构图、启动步骤、初始账号 admin/Admin@123、权限说明、换 MySQL 指引
5. **端到端验证**：后端起服务 → 前端 dev → 浏览器走通 注册→登录→搜索→加自选→看图表；K 线渲染正常

## 验证标准
- `dotnet build` 0 错误；`npm run build` 0 错误
- 浏览器端实际登录并看到真实数据的 K 线图、资金流图、收益卡片
- VIP 权限分级生效（普通用户小时线 403，提升 VIP 后可用）