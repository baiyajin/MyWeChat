@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

echo 正在启动Flutter应用...
echo.

REM 检查是否已配置平台支持
if not exist "android" (
    echo [1/3] 正在配置平台支持...
    flutter create . --platforms=android,windows,web --no-overwrite
    echo.
)

echo [2/3] 检查可用设备...
call flutter devices
echo.

echo [3/3] 正在启动应用...
echo.

REM 优先尝试Windows平台
echo 正在尝试Windows平台...
echo.
call flutter run -d windows
set RUN_RESULT=%ERRORLEVEL%
if %RUN_RESULT% EQU 0 (
    goto :end
)

echo.
echo Windows平台运行失败，尝试Chrome浏览器...
call flutter run -d chrome
set RUN_RESULT=%ERRORLEVEL%
if %RUN_RESULT% EQU 0 (
    goto :end
)

echo.
echo Chrome运行失败，尝试Edge浏览器...
call flutter run -d edge
set RUN_RESULT=%ERRORLEVEL%
if %RUN_RESULT% EQU 0 (
    goto :end
)

echo.
echo 所有平台运行失败！
echo.
echo 请手动选择设备运行：
echo   flutter run -d windows
echo   或
echo   flutter run -d chrome
echo   或
echo   flutter run -d edge
echo   或
echo   flutter run -d <device_id>
echo.

:end
if not "%1"=="nopause" pause

