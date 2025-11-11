@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

echo 正在启动Flutter应用（Chrome浏览器）...
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

REM 启动Chrome浏览器（使用默认配置，让 Flutter 自动选择端口）
flutter run -d chrome

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Chrome browser startup failed!
    echo.
    echo Please check:
    echo   1. Is Google Chrome browser installed?
    echo   2. Is Web platform support configured?
    echo   3. Run flutter doctor to check environment configuration
    echo.
    pause
)

