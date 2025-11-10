@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

echo 正在启动Flutter应用（Edge浏览器）...
echo.

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "web" (
    echo 正在配置Web平台支持...
    flutter create . --platforms=web --no-overwrite
    echo.
)

REM 检查可用设备
call flutter devices
echo.

echo 正在启动应用
echo.

REM 检查端口是否被占用
netstat -ano | findstr :5000 >nul
if %ERRORLEVEL% EQU 0 (
    echo 警告: 端口 5000 已被占用，尝试使用其他端口...
    flutter run -d edge --web-port=5001 --web-hostname=localhost
) else (
    REM 启动Edge浏览器
    REM 使用 --web-port 指定端口，避免端口冲突
    REM 使用 --web-hostname 指定主机名
    flutter run -d edge --web-port=5000 --web-hostname=localhost
)

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Edge浏览器启动失败！
    echo.
    echo 请检查：
    echo   1. 是否已安装Microsoft Edge浏览器
    echo   2. 是否已配置Web平台支持
    echo   3. 运行 flutter doctor 检查环境配置
    echo.
)

pause

