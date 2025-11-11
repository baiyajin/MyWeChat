@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "web" (
    echo 正在配置Web平台支持...
    flutter create . --platforms=web --no-overwrite >nul 2>&1
)

echo 正在清理构建缓存...
call flutter clean >nul 2>&1

echo 正在获取依赖...
call flutter pub get

echo 正在启动Chrome浏览器（将自动构建Web应用）...
REM 启动Chrome浏览器（flutter run会自动构建，确保assets正确打包）
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

