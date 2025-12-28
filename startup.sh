#!/bin/bash

# ==============================================================================
# DynamicDbApi 启动脚本 (Linux/macOS)
# ==============================================================================
#
# 功能：启动nginx和DynamicDbApi应用程序，支持处理多个.NET Runtime版本的情况
#
# 参数：
#   -e, --environment    指定运行环境 (Development/Production)，默认：Development
#   -n, --nginx-path     nginx可执行文件路径，默认：./nginx-1.28.0/nginx
#   -a, --app-path       应用程序目录路径，默认：当前脚本所在目录
#   -h, --help           显示帮助信息
#
# 示例：
#   ./startup.sh
#   ./startup.sh -e Production
#   ./startup.sh -n /usr/local/nginx/sbin/nginx
# ==============================================================================

set -e

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 默认参数
ENVIRONMENT="Development"
NGINX_PATH="./nginx-1.28.0/nginx"
APP_PATH="$(pwd)"

# 日志函数
log() {
    local level="$1"
    local message="$2"
    local timestamp=$(date "+%Y-%m-%d %H:%M:%S")
    
    case $level in
        "INFO") echo -e "[${timestamp}] [${BLUE}INFO${NC}] ${message}" ;;
        "ERROR") echo -e "[${timestamp}] [${RED}ERROR${NC}] ${message}" ;;
        "WARNING") echo -e "[${timestamp}] [${YELLOW}WARNING${NC}] ${message}" ;;
        "SUCCESS") echo -e "[${timestamp}] [${GREEN}SUCCESS${NC}] ${message}" ;;
        *) echo -e "[${timestamp}] [${NC}${level}${NC}] ${message}" ;;
    esac
}

# 错误处理函数
handle_error() {
    local error_message="$1"
    log "ERROR" "${error_message}"
    log "ERROR" "脚本执行失败，请检查上述错误信息。"
    exit 1
}

# 显示帮助信息
show_help() {
    echo -e "${GREEN}DynamicDbApi 启动脚本 (Linux/macOS)${NC}"
    echo ""
    echo "用法: $0 [选项]"
    echo ""
    echo "选项:"
    echo "  -e, --environment    指定运行环境 (Development/Production)，默认：Development"
    echo "  -n, --nginx-path     nginx可执行文件路径，默认：./nginx-1.28.0/nginx"
    echo "  -a, --app-path       应用程序目录路径，默认：当前脚本所在目录"
    echo "  -h, --help           显示帮助信息"
    echo ""
    echo "示例:"
    echo "  $0"
    echo "  $0 -e Production"
    echo "  $0 -n /usr/local/nginx/sbin/nginx"
}

# 解析命令行参数
while [[ $# -gt 0 ]]; do
    case $1 in
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        -n|--nginx-path)
            NGINX_PATH="$2"
            shift 2
            ;;
        -a|--app-path)
            APP_PATH="$2"
            shift 2
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            log "ERROR" "未知参数: $1"
            show_help
            exit 1
            ;;
    esac
done

# 检查参数有效性
if [[ "$ENVIRONMENT" != "Development" && "$ENVIRONMENT" != "Production" ]]; then
    handle_error "无效的环境参数: $ENVIRONMENT，可选值：Development, Production"
fi

log "INFO" "开始启动项目..."
log "INFO" "环境: $ENVIRONMENT"
log "INFO" "nginx路径: $NGINX_PATH"
log "INFO" "应用程序路径: $APP_PATH"

# 检查nginx路径是否存在
if [ ! -f "$NGINX_PATH" ]; then
    handle_error "nginx可执行文件不存在: $NGINX_PATH"
fi

# 检查应用程序目录是否存在
if [ ! -d "$APP_PATH" ]; then
    handle_error "应用程序目录不存在: $APP_PATH"
fi

# 检查是否具有执行权限
if [ ! -x "$NGINX_PATH" ]; then
    log "WARNING" "nginx可执行文件缺少执行权限，正在尝试添加..."
    chmod +x "$NGINX_PATH"
fi

# 定位.NET 8 Runtime
log "INFO" "正在查找.NET 8 Runtime..."

# 使用dotnet --list-runtimes命令查找已安装的.NET Runtime版本
DOTNET_RUNTIMES=$(dotnet --list-runtimes 2>&1)

if [ $? -ne 0 ]; then
    handle_error "无法执行dotnet命令，请确保已安装.NET SDK或Runtime。"
fi

# 查找.NET 8 Runtime
NET8_RUNTIME=$(echo "$DOTNET_RUNTIMES" | grep "Microsoft.NETCore.App 8\.")

if [ -z "$NET8_RUNTIME" ]; then
    handle_error "未找到.NET 8 Runtime，请安装.NET 8 Runtime或SDK。"
fi

log "SUCCESS" "找到.NET 8 Runtime: $NET8_RUNTIME"

# 启动nginx
log "INFO" "正在启动nginx..."

try {
    # 获取nginx目录
    NGINX_DIR=$(dirname "$NGINX_PATH")
    
    # 在nginx目录下启动nginx
    cd "$NGINX_DIR" && "./$(basename "$NGINX_PATH")" -c "$NGINX_DIR/conf/nginx.conf"
    
    log "SUCCESS" "nginx启动成功。"
} catch {
    handle_error "nginx启动失败: $!"
}

# 回到应用程序目录
cd "$APP_PATH"

# 启动应用程序
log "INFO" "正在启动DynamicDbApi应用程序..."

# 构建启动命令
APP_DLL="$APP_PATH/bin/$ENVIRONMENT/net8.0/DynamicDbApi.dll"

if [ ! -f "$APP_DLL" ]; then
    handle_error "应用程序DLL不存在: $APP_DLL，请先构建项目。"
fi

# 启动应用程序（后台运行）
dotnet "$APP_DLL" --environment "$ENVIRONMENT" &

APP_PID=$!

log "SUCCESS" "DynamicDbApi应用程序启动成功，进程ID: $APP_PID"
log "SUCCESS" "访问地址: http://localhost:5182"
log "SUCCESS" "Swagger文档地址: https://localhost:7042/swagger/index.html"
log "INFO" "按 Ctrl+C 停止服务..."

# 等待用户输入或应用程序退出
trap "log 'INFO' '正在停止服务...' && kill $APP_PID 2>/dev/null; exit 0" INT

wait $APP_PID

# 检查应用程序退出状态
if [ $? -ne 0 ]; then
    log "ERROR" "应用程序异常退出，请查看日志获取详细信息。"
    exit 1
fi
