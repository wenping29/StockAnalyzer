#!/usr/bin/env bash
# 生成部署产物：后台静态页输出到 dist/admin，服务端发布到 dist/sp-server，扩展输出到 dist
# 用法：bash deploy/pack.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DIST="$ROOT/deploy/dist"
ADMIN_OUT="$DIST/admin"
STAGE="$DIST/sp-server"
STAMP="$(date +%Y%m%d-%H%M)"

if ! command -v npm >/dev/null 2>&1; then
  echo "缺少 npm，无法构建后台静态页" >&2
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1 && ! command -v python >/dev/null 2>&1; then
  echo "缺少 python3/python，无法打包浏览器扩展" >&2
  exit 1
fi

echo "== 清理 dist =="
rm -rf "$DIST"
mkdir -p "$DIST"

echo "== 构建后台静态页 =="
# 后台独立挂在 7789 根路径，必须用 standalone 配置（base '/'）；
# 默认配置的 base './' 会让路由 basename 变成 '.'，页面渲染空白且不报错
(cd "$ROOT/frontend" && npx tsc -b && npx vite build --config vite.config.ts --outDir "$ADMIN_OUT" --emptyOutDir)

echo "== 发布 linux-x64 =="
dotnet publish "$ROOT/backend/FundAnalysis/FundAnalysis.csproj" -c Release -r linux-x64 --self-contained true -o "$STAGE"

echo "== 附加部署文件 =="
# mkdir -p "$STAGE/deploy"
# cp "$ROOT/deploy/sp.service" "$ROOT/deploy/nginx-sp.conf" "$ROOT/deploy/nginx-admin.conf" "$ROOT/deploy/sp.env.example" "$STAGE/deploy/"
# cp "$ROOT/deploy/DEPLOY.md" "$STAGE/DEPLOY.md"
# chmod +x "$STAGE/SpServer"

# echo "== 打包浏览器扩展 =="
# if command -v python3 >/dev/null 2>&1; then
#   PYTHON=python3
# else
#   PYTHON=python
# fi
# "$PYTHON" "$ROOT/tools/pack-extension.py" --out-dir "$DIST"

# TARBALL="$DIST/sp-server-linux-x64-$STAMP.tar.gz"
# tar -czf "$TARBALL" -C "$DIST" sp-server

echo
echo "完成：$TARBALL"
echo "大小：$(du -h "$TARBALL" | cut -f1)"
