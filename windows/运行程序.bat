@echo off
chcp 65001 >nul
echo ========================================
echo MyWeChat Windows端 - 运行程序
echo ========================================
echo.

cd /d "%~dp0SalesChampion.Windows"

REM 查找实际的exe文件路径（可能在不同平台目录下）
set "EXE_PATH="
if exist "bin\x86\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\x86\Debug\net9.0-windows\SalesChampion.Windows.exe"
) else if exist "bin\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\Debug\net9.0-windows\SalesChampion.Windows.exe"
) else if exist "bin\Debug\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\Debug\SalesChampion.Windows.exe"
) else if exist "bin\x64\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\x64\Debug\net9.0-windows\SalesChampion.Windows.exe"
)

if "%EXE_PATH%"=="" (
    echo 错误: 找不到可执行文件！
    echo 请先编译项目（运行"仅编译.bat"或使用Visual Studio）
    pause
    exit /b 1
)

echo 找到可执行文件: %EXE_PATH%
echo.
echo 正在以管理员权限运行程序...
echo.
powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"

