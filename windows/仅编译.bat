@echo off
chcp 65001 >nul
echo ========================================
echo MyWeChat Windows端 - 编译
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
echo [3/3] 编译项目...
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
echo 可执行文件位置:
echo bin\Debug\SalesChampion.Windows.exe
echo.
echo 请以管理员权限运行该文件
echo.

pause

