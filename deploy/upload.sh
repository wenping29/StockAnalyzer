#!/bin/bash
# upload.sh - 交互式选择全量/增量上传，并重启服务

set -euo pipefail

# ==================== 配置区 ====================
SERVER="root@43.142.147.244"
PORT="22"
SSH_KEY="$HOME/.ssh/uploaddata.pem"

# 1. 前端静态项目配置
LOCAL_FRONTEND="./dist/admin/"
REMOTE_FRONTEND="/www/wwwroot/fund/web"

# 2. .NET 后端项目配置
LOCAL_DOTNET="./dist/sp-server/"
REMOTE_DOTNET="/www/wwwroot/fund/api"
EXECUTABLE_NAME="FundAnalysis"  # ⚠️ 你的独立包主程序名称（无后缀）
# ===============================================

# 👇 核心交互：询问是否全量上传
read -p "是否首次部署或升级了 .NET 环境依赖？(输入 y 全量上传，直接回车默认增量上传): " IS_FULL_DEPLOY
echo ""

# 检查本地目录是否存在
for dir in "$LOCAL_FRONTEND" "$LOCAL_DOTNET"; do
  if [ ! -d "$dir" ]; then
    echo "错误: 本地目录不存在 -> $dir"
    exit 1
  fi
done

# ==================== 1. 上传前端 ====================
echo "========== 开始上传前端静态文件 =========="
ssh -i "$SSH_KEY" -p "$PORT" "$SERVER" "mkdir -p '$REMOTE_FRONTEND'"
scp -i "$SSH_KEY" -P "$PORT" -r "$LOCAL_FRONTEND"* "$SERVER:$REMOTE_FRONTEND/"
echo "前端静态文件上传完成"

# ==================== 2. 上传 .NET 后端 ====================
echo "2. 正在重启 .NET 项目..."

# 找进程 -> 杀掉 -> 切换到 www 用户启动
ssh -i "$SSH_KEY" -p "$PORT" "$SERVER" bash -s << EOF
    echo "正在查找并停止旧进程..."
    # 查找包含 SpServer 的 dotnet 进程并杀掉
    pkill -u www -f "$EXECUTABLE_NAME" || true
    sleep 2
  
    echo "完成关闭旧进程"
EOF
echo "========== 开始上传 .NET 项目 =========="
ssh -i "$SSH_KEY" -p "$PORT" "$SERVER" "mkdir -p '$REMOTE_DOTNET'"

if [[ "$IS_FULL_DEPLOY" == "y" || "$IS_FULL_DEPLOY" == "Y" ]]; then
  echo ">>> 模式：全量上传（包含环境依赖）..."
  scp -i "$SSH_KEY" -P "$PORT" -r "$LOCAL_DOTNET"* "$SERVER:$REMOTE_DOTNET/"
else
  echo ">>> 模式：增量上传（仅上传指定文件）..."
  # 只上传核心代码文件
  scp -i "$SSH_KEY" -P "$PORT" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.dll" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.deps.json" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.runtimeconfig.json" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.pdb" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.Core.dll" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.Core.pdb" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME.xml" \
    "$LOCAL_DOTNET$EXECUTABLE_NAME" \
    "$SERVER:$REMOTE_DOTNET/" || echo "提示：部分文件（如 .pdb 或 .xml）可能不存在，已跳过。"
fi

echo ".NET 项目上传完成"

# ==================== 3. 重启服务 ====================
echo "========== 开始重启 .NET 项目 =========="
ssh -i "$SSH_KEY" -p "$PORT" "$SERVER" bash -s << EOF
    echo "正在停止旧进程..."
    pkill -u www -f "$EXECUTABLE_NAME" || true
    sleep 2

    echo "正在修复权限..."
    chown -R www:www "$REMOTE_DOTNET"
    rm -f "$REMOTE_DOTNET/app.log"

    echo "正在启动新进程（独立包模式）..."
    # 进入目录，赋予可执行权限，并以 www 用户启动
    cd "$REMOTE_DOTNET"
    chmod +x "$EXECUTABLE_NAME"
    su -s /bin/bash www -c "nohup ./$EXECUTABLE_NAME > app.log 2>&1 &"
    
    echo "重启完成！"
EOF

echo "========== 全部任务结束 =========="