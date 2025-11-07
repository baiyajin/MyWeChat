@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

echo 正在启动Flutter应用（Windows平台）...
echo.

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "windows" (
    echo 正在配置Windows平台支持...
    flutter create . --platforms=windows --no-overwrite
    echo.
)

REM 检查并修复符号链接问题
if exist "windows\flutter\ephemeral\.plugin_symlinks" (
    echo 检测到符号链接目录，检查是否有问题...
    REM 如果符号链接有问题，清理 ephemeral 目录
    call flutter clean >nul 2>&1
    call flutter pub get >nul 2>&1
)

echo 正在启动应用...
echo.
flutter run -d windows

pause

