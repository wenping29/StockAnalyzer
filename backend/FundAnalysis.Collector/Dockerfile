# 独立数据采集器镜像
# 构建上下文为 backend/ 目录：
#   cd backend && docker build -f FundAnalysis.Collector/Dockerfile -t fund-collector .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 先复制 csproj 单独还原，利用 Docker 层缓存
COPY ["FundAnalysis.Core/FundAnalysis.Core.csproj", "FundAnalysis.Core/"]
COPY ["FundAnalysis.Collector/FundAnalysis.Collector.csproj", "FundAnalysis.Collector/"]
RUN dotnet restore "FundAnalysis.Collector/FundAnalysis.Collector.csproj"

COPY . .
RUN dotnet publish "FundAnalysis.Collector/FundAnalysis.Collector.csproj" \
    -c Release -o /app/publish /p:UseAppHost=false

# 运行时镜像（Core 引用 ASP.NET Core 共享框架，用 aspnet 基础镜像）
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

# 默认 once 模式：执行一次采集退出（服务器 cron 调度）；常驻模式加 --mode=daemon
ENTRYPOINT ["dotnet", "FundAnalysis.Collector.dll"]
