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

REM 启动Edge浏览器（静默模式，减少输出）
flutter run -d edge 2>&1 | findstr /V /C:"Flutter assets" /C:"Resolving dependencies" /C:"Downloading packages" /C:"Got dependencies" /C:"packages have newer versions" /C:"Try `flutter pub" /C:"Launching" /C:"Waiting for connection" /C:"This app is linked" /C:"Debug service listening" /C:"Flutter run key commands" /C:"r Hot reload" /C:"R Hot restart" /C:"h List all" /C:"d Detach" /C:"c Clear" /C:"q Quit" /C:"A Dart VM Service" /C:"Starting application" /C:"The Flutter DevTools"

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

