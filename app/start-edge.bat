@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

REM 临时禁用 Android 设备检测，避免 ADB 错误影响 Web 启动
set ANDROID_HOME=
set ANDROID_SDK_ROOT=

echo 正在启动Flutter应用（Edge浏览器）...
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

echo 启动Edge浏览器...
echo 提示: 如果浏览器没有自动打开，请手动访问 %WEB_URL%
echo.

REM 启动 Flutter 应用，过滤 ADB 错误但保留其他输出
flutter run -d edge --web-port=%WEB_PORT% --web-hostname=127.0.0.1 --device-timeout=10 2>&1 | findstr /V /C:"Unable to run \"adb\"" /C:"Error details: Process exited abnormally" /C:"daemon not running" /C:"could not read ok from ADB Server" /C:"failed to start daemon" /C:"adb.exe: failed to check server version"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Edge浏览器启动失败！
    echo.
    echo 请检查：
    echo   1. 是否已安装Microsoft Edge浏览器
    echo   2. 是否已配置Web平台支持
    echo   3. 运行 flutter doctor 检查环境配置
    echo.
    pause
)

