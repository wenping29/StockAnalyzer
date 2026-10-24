#!/usr/bin/env bash
# ============================================================
# FundAnalysis 宝塔部署打包脚本（在 Windows 开发机的 Git Bash 中运行）
#
# 用法：
#   bash deploy/pack.sh              # 自包含发布（服务器免装 .NET，包较大约 70MB+）
#   bash deploy/pack.sh -- slim      # 框架依赖发布（服务器需装 .NET 10 运行时，包约 10MB）
#
# 产物：publish/fundanalysis-<时间戳>.tar.gz
#   ├── web/           Web API（dotnet FundAnalysis.dll，宝塔 .NET 项目/进程守护托管）
#   ├── collector/     独立采集器（可选部署，--mode=daemon 常驻或宝塔计划任务跑 once）
#   ├── frontend/      前端静态文件（宝塔 nginx 站点根目录）
#   ├── nginx.conf     nginx 站点配置（宝塔"配置文件"里参考/粘贴）
#   └── 宝塔部署说明.md
# ============================================================
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT=$(pwd)

# 参数：-- slim 用框架依赖（小包）
SELF_CONTAINED=true
if [ "${2:-${1:-}}" = "slim" ]; then SELF_CONTAINED=false; fi
RID=linux-x64

VERSION=$(date +%Y%m%d-%H%M)
OUT="$ROOT/publish/fundanalysis-$VERSION"
rm -rf "$OUT"
mkdir -p "$OUT"

echo "==> [1/5] 发布 Web API（linux-x64, self-contained=$SELF_CONTAINED）"
if $SELF_CONTAINED; then
  dotnet publish backend/FundAnalysis -c Release -r "$RID" --self-contained true -o "$OUT/web"
else
  dotnet publish backend/FundAnalysis -c Release -o "$OUT/web"
fi

echo "==> [2/5] 发布采集器"
if $SELF_CONTAINED; then
  dotnet publish backend/FundAnalysis.Collector -c Release -r "$RID" --self-contained true -o "$OUT/collector"
else
  dotnet publish backend/FundAnalysis.Collector -c Release -o "$OUT/collector"
fi

echo "==> [3/5] 构建前端（提示：若 vite dev server 正在运行请先停止，避免文件锁冲突）"
cd frontend
# npm ci 失败（如 dev server 锁文件）时回退 npm install（增量，不删除现有 node_modules）
if ! npm ci > /tmp/pack-npm.log 2>&1; then
  echo "    npm ci 失败（$(tail -1 /tmp/pack-npm.log | head -c 80)），回退 npm install"
  npm install > /tmp/pack-npm.log 2>&1 || { cat /tmp/pack-npm.log; exit 1; }
fi
npm run build 2>&1 | tail -2
cd "$ROOT"
mkdir -p "$OUT/frontend"
cp -r frontend/dist/* "$OUT/frontend/"

echo "==> [4/5] 复制部署配置"
cp backend/FundAnalysis/deploy/nginx.conf "$OUT/nginx.conf" 2>/dev/null || true
cp deploy/baota.md "$OUT/宝塔部署说明.md" 2>/dev/null || true
cp deploy/createdb.sql "$OUT/createdb.sql" 2>/dev/null || true

echo "==> [5/5] 打包 tar.gz"
cd "$ROOT/publish"
tar -czf "fundanalysis-$VERSION.tar.gz" "fundanalysis-$VERSION"
cd "$ROOT"

SIZE=$(du -h "publish/fundanalysis-$VERSION.tar.gz" | cut -f1)
echo ""
echo "============================================"
echo "打包完成: publish/fundanalysis-$VERSION.tar.gz ($SIZE)"
echo "上传到宝塔服务器后，解压并按包内《宝塔部署说明.md》操作"
echo "============================================"
