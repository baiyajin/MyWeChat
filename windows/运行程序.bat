@echo off
chcp 65001 >nul
set "PROJECT_DIR=%~dp0SalesChampion.Windows"

:main_menu
cls
echo ========================================
echo     MyWeChat Windows端 - 主菜单
echo ========================================
echo.
echo 请选择功能：
echo.
echo   [1] 清理编译文件
echo   [2] 编译项目
echo   [3] 关闭程序
echo   [4] 运行程序
echo   [5] 全部执行 (1-2-3-4)
echo   [6] 退出
echo.
echo ========================================
set /p choice=请输入选项 (1-6): 

if "%choice%"=="1" goto clean
if "%choice%"=="2" goto build
if "%choice%"=="3" goto close
if "%choice%"=="4" goto run
if "%choice%"=="5" goto all
if "%choice%"=="6" goto exit

echo.
echo 无效的选项，请重新选择
timeout /t 2 /nobreak >nul
goto main_menu

:clean
cls
echo ========================================
echo     功能1: 清理编译文件
echo ========================================
echo.
cd /d "%PROJECT_DIR%"
echo 正在清理编译文件...
if exist "bin" (
    echo 删除 bin 目录...
    rmdir /s /q "bin" 2>nul
)
if exist "obj" (
    echo 删除 obj 目录...
    rmdir /s /q "obj" 2>nul
)
echo 清理完成！
pause
goto main_menu

:build
cls
echo ========================================
echo     功能2: 编译项目
echo ========================================
echo.
cd /d "%PROJECT_DIR%"

echo [1/4] 检查.NET SDK版本...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未安装.NET SDK，请先安装.NET 9.0 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
    pause
    goto main_menu
)

echo [2/4] 检查并关闭已运行的程序...
set "PROCESS_CLOSED=0"
for /L %%i in (1,1,5) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        if %%i equ 1 echo 检测到程序正在运行，正在关闭...
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        timeout /t 1 /nobreak >NUL
    ) else (
        set "PROCESS_CLOSED=1"
        goto :build_process_closed
    )
)
:build_process_closed
if "%PROCESS_CLOSED%"=="1" (
    echo 程序已关闭
    timeout /t 2 /nobreak >NUL
)

echo [3/4] 还原NuGet包...
dotnet restore
if %errorlevel% neq 0 (
    echo 错误: NuGet包还原失败
    pause
    goto main_menu
)

echo [4/4] 编译项目...
dotnet build -c Debug
if %errorlevel% neq 0 (
    echo 错误: 编译失败
    pause
    goto main_menu
)

echo.
echo ========================================
echo 编译成功！
echo ========================================
pause
goto main_menu

:close
cls
echo ========================================
echo     功能3: 关闭程序
echo ========================================
echo.
set "PROCESS_FOUND=0"
for /L %%i in (1,1,5) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        set "PROCESS_FOUND=1"
        if %%i equ 1 (
            echo 检测到程序正在运行，正在关闭...
        )
        echo 尝试关闭进程 (第 %%i 次)...
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        if %%i lss 5 timeout /t 1 /nobreak >NUL
    ) else (
        if "%PROCESS_FOUND%"=="1" (
            echo 程序已成功关闭
        ) else (
            echo 程序未运行
        )
        goto :close_end
    )
)
if "%PROCESS_FOUND%"=="1" (
    echo.
    echo 警告: 无法彻底关闭程序
    echo 可能需要管理员权限，或手动在任务管理器中结束进程
)
:close_end
echo.
echo 等待文件句柄释放...
timeout /t 2 /nobreak >NUL
echo 完成
pause
goto main_menu

:run
cls
echo ========================================
echo     功能4: 运行程序
echo ========================================
echo.
cd /d "%PROJECT_DIR%"

REM 查找exe文件
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
    echo 请先编译项目（选项2）
    pause
    goto main_menu
)

echo 找到可执行文件: %EXE_PATH%
echo.
echo ========================================
echo 重要提示: .NET Desktop Runtime 检查
echo ========================================
echo.
echo 程序需要 .NET Desktop Runtime 9.0.10 (x86)
echo.
echo 如果启动时出现错误提示：
echo "You must install .NET Desktop Runtime to run this application"
echo.
echo 请先安装 .NET Desktop Runtime：
echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
echo 选择: .NET Desktop Runtime 9.0.10 (x86)
echo.
echo 是否现在运行程序？(Y/N)
set /p run_choice=
if /i not "%run_choice%"=="Y" (
    goto main_menu
)

echo.
echo 正在以管理员权限运行程序...
powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"

timeout /t 2 /nobreak >nul
goto main_menu

:all
cls
echo ========================================
echo     功能5: 全部执行 (1-2-3-4)
echo ========================================
echo.
echo 将按顺序执行：清理 -> 编译 -> 关闭 -> 运行
echo.
pause

REM 1. 清理
echo.
echo [步骤1/4] 清理编译文件...
cd /d "%PROJECT_DIR%"
if exist "bin" rmdir /s /q "bin" 2>nul
if exist "obj" rmdir /s /q "obj" 2>nul
echo 清理完成

REM 2. 编译
echo.
echo [步骤2/4] 编译项目...
echo 检查.NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未安装.NET SDK
    pause
    goto main_menu
)

echo 关闭已运行的程序...
for /L %%i in (1,1,3) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        timeout /t 1 /nobreak >NUL
    )
)
timeout /t 2 /nobreak >NUL

echo 还原NuGet包...
dotnet restore >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: NuGet包还原失败
    pause
    goto main_menu
)

echo 编译项目...
dotnet build -c Debug
if %errorlevel% neq 0 (
    echo 错误: 编译失败
    pause
    goto main_menu
)
echo 编译成功

REM 3. 关闭
echo.
echo [步骤3/4] 关闭程序...
for /L %%i in (1,1,3) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        timeout /t 1 /nobreak >NUL
    )
)
timeout /t 2 /nobreak >NUL
echo 关闭完成

REM 4. 运行
echo.
echo [步骤4/4] 运行程序...
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
    pause
    goto main_menu
)

echo 找到可执行文件: %EXE_PATH%
echo.
echo 注意: 程序需要 .NET Desktop Runtime 9.0.10 (x86)
echo 如果启动失败，请先安装 .NET Desktop Runtime
echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
echo.
echo 正在以管理员权限运行程序...
powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"

echo.
echo ========================================
echo 全部执行完成！
echo ========================================
pause
goto main_menu

:exit
cls
echo 感谢使用！
timeout /t 1 /nobreak >nul
exit /b 0
