@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

REM 临时禁用 Android 设备检测，避免 ADB 错误影响 Web 启动
set ANDROID_HOME=
set ANDROID_SDK_ROOT=

echo 正在启动Flutter应用（Chrome浏览器）...
echo.

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "web" (
    echo 正在配置Web平台支持...
    flutter create . --platforms=web --no-overwrite
    echo.
)

echo 正在启动应用
echo.

REM 检查端口是否被占用
netstat -ano | findstr :5000 >nul
if %ERRORLEVEL% EQU 0 (
    echo 警告: 端口 5000 已被占用，尝试使用其他端口...
    set WEB_PORT=5001
    set WEB_URL=http://127.0.0.1:5001
) else (
    set WEB_PORT=5000
    set WEB_URL=http://127.0.0.1:5000
)

echo 启动Chrome浏览器...
echo 提示: 如果浏览器没有自动打开，请手动访问 %WEB_URL%
echo.

REM 启动 Flutter 应用
REM 注意: 不使用 findstr 过滤，以保留彩色输出和确保服务器正常启动
flutter run -d chrome --web-port=%WEB_PORT% --web-hostname=127.0.0.1 --device-timeout=10

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

