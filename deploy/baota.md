# 宝塔面板部署说明

适用宝塔 Linux 面板（7.x/8.x/9.x）。共三种部署形态，按需选择：

- **最简**：只部署 Web（SQLite 单机，全功能含内置定时采集）
- **标准**：Web + 宝塔 MySQL（多用户生产推荐）
- **完整**：Web + MySQL + 独立采集器（采集与 Web 分离）

---

## 一、Web 后端（必做）

### 1. 上传解压

宝塔文件管理上传 `fundanalysis-*.tar.gz` 到如 `/opt/`，解压：

```bash
cd /opt && tar -xzf fundanalysis-*.tar.gz && mv fundanalysis-*/ web
# 目录结构：/opt/web（含 FundAnalysis.dll）
```

### 2. 安装 .NET 运行环境（仅 slim 包需要）

自包含包（默认）跳过本步。slim 包需安装 .NET 10 运行时：
宝塔软件商店搜 ".NET"，或手动：

```bash
curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 10.0 --runtime dotnet --install-dir /usr/share/dotnet
ln -s /usr/share/dotnet/dotnet /usr/local/bin/dotnet
```

### 3. 生产配置（重要）

**先建库**：在数据库服务器上执行包内 `createdb.sql`（PostgreSQL/MySQL/SQL Server/Oracle 的建库语句都在里面，应用启动会自动建表，无需手动建表）。

编辑 `/opt/web/appsettings.json`，或在进程守护里加环境变量（推荐）：

| 环境变量 | 值 |
|---|---|
| ASPNETCORE_ENVIRONMENT | Production |
| ASPNETCORE_URLS | http://localhost:5200 |
| Jwt__Key | 至少32位随机串（`openssl rand -hex 32` 生成） |
| Database__Provider | sqlite / mysql / postgresql |
| ConnectionStrings__Default | 见下 |
| Cors__Origins__0 | https://你的域名（跨域部署时） |
| Collector__ScheduleEnabled | false（部署独立采集器时才关，否则保持默认） |

连接串示例：
```
sqlite（默认）: Data Source=fundanalysis.db
mysql（宝塔装MySQL后建库 fund_analysis 建用户授权）:
  Server=127.0.0.1;Port=3306;Database=fund_analysis;User=fund;Password=你设置的密码;
postgresql:
  Host=127.0.0.1;Port=5432;Database=fund_analysis;Username=fund;Password=你的密码
```

注意：默认 SQLite 数据库文件生成在**工作目录**（即启动目录 /opt/web），记得加入宝塔备份计划。

### 4. 启动（进程守护）

**方式 A：宝塔 .NET 项目部署插件**（软件商店安装".NET项目部署"）：
- 项目路径 /opt/web、启动程序 dotnet、参数 `FundAnalysis.dll`、端口 5200

**方式 B：宝塔进程守护管理器（Supervisor）**：
- 名称 fundanalysis，启动用户 www 或 root
- 启动命令：`dotnet /opt/web/FundAnalysis.dll`（slim）或 `/opt/web/FundAnalysis`（自包含可直接执行）
- 运行目录 /opt/web

首次启动自动建表 + 创建 admin 账号（admin / Admin@123，**登录后立即改密码**）。
验证：`curl http://localhost:5200/api/auth/roles` 返回角色数组即成功。

---

## 二、前端 + 域名（宝塔站点）

1. 宝塔新建站点（绑定你的域名，申请 SSL 证书）
2. 站点根目录上传/解压 `frontend/` 内全部文件（index.html 所在层）
3. 站点设置 → 配置文件，参考包内 `nginx.conf`：
   - 关闭"防跨站攻击(open_basedir)"（或把 /opt/web 加入白名单）
   - 添加 `location /api/ { proxy_pass http://127.0.0.1:5200; ... }`
   - 添加 `location / { try_files $uri $uri/ /index.html; }`（SPA 路由回退）
4. 若用了 Hangfire 面板，按 nginx.conf 中的 allow/deny 限制内网访问

---

## 三、独立采集器（可选）

采集与 Web 共用同一数据库时部署：

```bash
cd /opt && tar -xzf fundanalysis-*.tar.gz && mv fundanalysis-*/ collector
```

**常驻模式**（宝塔进程守护）：启动命令 `dotnet /opt/collector/FundAnalysis.Collector.dll --mode=daemon`，
环境变量与 Web 相同（Database__Provider / ConnectionStrings__Default），并给 Web 设 `Collector__ScheduleEnabled=false` 避免重复采集。

**once 模式**（宝塔计划任务，推荐简单）：
- 任务1（交易日 16:35 全量）：`dotnet /opt/collector/FundAnalysis.Collector.dll --jobs=daily`
- 任务2（交易时段分钟线，每5分钟）：`dotnet /opt/collector/FundAnalysis.Collector.dll --jobs=minutes`
- 计划任务里配置环境变量，或写进 /opt/collector/appsettings.json

---

## 四、常用排查

- 看日志：进程守护里查看实时输出；或 `journalctl -u fundanalysis`
- 500/连不上：检查 5200 端口监听 `ss -tlnp | grep 5200`
- 数据库连不上：宝塔 MySQL 默认禁止远程，本机连接用 127.0.0.1
- 东财风控：指数/行情数据缺失多为服务器 IP 被东财限流，采集任务自动重试，无需处理
