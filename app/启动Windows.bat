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

echo 正在启动应用...
echo.
flutter run -d windows

pause

