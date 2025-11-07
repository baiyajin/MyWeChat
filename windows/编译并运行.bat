@echo off
chcp 65001 >nul
echo ========================================
echo MyWeChat Windows端 - 编译并运行
echo ========================================
echo.

cd /d "%~dp0SalesChampion.Windows"

echo [1/3] 检查.NET SDK版本...
dotnet --version
if %errorlevel% neq 0 (
    echo 错误: 未安装.NET SDK，请先安装.NET 9.0 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
    pause
    exit /b 1
)

echo.
echo [2/3] 还原NuGet包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误: NuGet包还原失败
    pause
    exit /b 1
)

echo.
echo [3/4] 清理旧的编译文件...
if exist "bin" (
    echo 删除 bin 目录...
    rmdir /s /q "bin" 2>nul
)
if exist "obj" (
    echo 删除 obj 目录...
    rmdir /s /q "obj" 2>nul
)
echo 清理完成

echo.
echo [4/4] 编译项目...
dotnet build -c Debug
if %errorlevel% neq 0 (
    echo 错误: 编译失败
    pause
    exit /b 1
)

echo.
echo ========================================
echo 编译成功！
echo ========================================
echo.

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
    echo 请检查编译是否成功
    pause
    exit /b 1
)

echo 可执行文件位置:
echo %EXE_PATH%
echo.
echo 注意: 必须以管理员权限运行！
echo.
echo 是否现在运行程序？(Y/N)
set /p choice=
if /i "%choice%"=="Y" (
    echo.
    echo 正在以管理员权限运行程序...
    powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"
) else (
    echo.
    echo 请手动以管理员权限运行: %EXE_PATH%
)

pause

