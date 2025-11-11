@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "web" (
    flutter create . --platforms=web --no-overwrite >nul 2>&1
)

REM 启动Chrome浏览器（使用默认配置，让 Flutter 自动选择端口）
flutter run -d chrome

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Chrome browser startup failed!
    echo Please check:
    echo   1. Is Google Chrome browser installed?
    echo   2. Is Web platform support configured?
    echo   3. Run flutter doctor to check environment configuration
    echo.
    pause
)

