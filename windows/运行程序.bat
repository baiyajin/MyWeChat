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
echo 将按顺序执行以下步骤：
echo   1. 清理编译文件
echo   2. 编译项目
echo   3. 关闭程序
echo   4. 运行程序
echo.
echo ========================================
pause

cd /d "%PROJECT_DIR%"

REM ========================================
REM 步骤1: 清理编译文件
REM ========================================
cls
echo ========================================
echo   [步骤1/4] 清理编译文件
echo ========================================
echo.
echo 正在清理编译文件...
echo.

if exist "bin" (
    echo [1.1] 删除 bin 目录...
    rmdir /s /q "bin" 2>nul
    if %errorlevel% equ 0 (
        echo [✓] bin 目录已删除
    ) else (
        echo [✗] bin 目录删除失败（可能被占用）
    )
) else (
    echo [✓] bin 目录不存在，无需清理
)

if exist "obj" (
    echo [1.2] 删除 obj 目录...
    rmdir /s /q "obj" 2>nul
    if %errorlevel% equ 0 (
        echo [✓] obj 目录已删除
    ) else (
        echo [✗] obj 目录删除失败（可能被占用）
    )
) else (
    echo [✓] obj 目录不存在，无需清理
)

echo.
echo ========================================
echo [步骤1/4] 清理完成！
echo ========================================
echo.
echo 按任意键继续下一步（编译项目）...
pause >nul

REM ========================================
REM 步骤2: 编译项目
REM ========================================
cls
echo ========================================
echo   [步骤2/4] 编译项目
echo ========================================
echo.

echo [2.1] 检查.NET SDK版本...
dotnet --version
if %errorlevel% neq 0 (
    echo [✗] 错误: 未安装.NET SDK，请先安装.NET 9.0 SDK
    echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
    pause
    goto main_menu
)
echo [✓] .NET SDK 已安装

echo.
echo [2.2] 检查并关闭已运行的程序...
set "PROCESS_CLOSED=0"
for /L %%i in (1,1,5) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        if %%i equ 1 (
            echo 检测到程序正在运行，正在关闭...
        )
        echo 尝试关闭进程 (第 %%i 次)...
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        timeout /t 1 /nobreak >NUL
    ) else (
        echo [✓] 程序已关闭
        set "PROCESS_CLOSED=1"
        goto :all_build_process_closed
    )
)
:all_build_process_closed
if "%PROCESS_CLOSED%"=="1" (
    echo 等待文件句柄释放...
    timeout /t 2 /nobreak >NUL
) else (
    echo [✗] 警告: 无法彻底关闭程序，编译可能会失败
)

echo.
echo [2.3] 还原NuGet包...
dotnet restore
if %errorlevel% neq 0 (
    echo [✗] 错误: NuGet包还原失败
    pause
    goto main_menu
)
echo [✓] NuGet包还原成功

echo.
echo [2.4] 编译项目...
dotnet build -c Debug
if %errorlevel% neq 0 (
    echo [✗] 错误: 编译失败
    pause
    goto main_menu
)
echo [✓] 编译成功！

echo.
echo ========================================
echo [步骤2/4] 编译完成！
echo ========================================
echo.
echo 按任意键继续下一步（关闭程序）...
pause >nul

REM ========================================
REM 步骤3: 关闭程序
REM ========================================
cls
echo ========================================
echo   [步骤3/4] 关闭程序
echo ========================================
echo.

set "PROCESS_FOUND=0"
for /L %%i in (1,1,5) do (
    tasklist /FI "IMAGENAME eq SalesChampion.Windows.exe" 2>NUL | find /I /N "SalesChampion.Windows.exe">NUL
    if "%ERRORLEVEL%"=="0" (
        set "PROCESS_FOUND=1"
        if %%i equ 1 (
            echo [3.1] 检测到程序正在运行，正在关闭...
        )
        echo 尝试关闭进程 (第 %%i 次)...
        taskkill /F /IM "SalesChampion.Windows.exe" >NUL 2>&1
        if %%i lss 5 timeout /t 1 /nobreak >NUL
    ) else (
        if "%PROCESS_FOUND%"=="1" (
            echo [✓] 程序已成功关闭
        ) else (
            echo [✓] 程序未运行
        )
        goto :all_close_end
    )
)
if "%PROCESS_FOUND%"=="1" (
    echo [✗] 警告: 无法彻底关闭程序
    echo 可能需要管理员权限，或手动在任务管理器中结束进程
)
:all_close_end
echo.
echo 等待文件句柄释放...
timeout /t 2 /nobreak >NUL
echo [✓] 文件句柄已释放

echo.
echo ========================================
echo [步骤3/4] 关闭完成！
echo ========================================
echo.
echo 按任意键继续下一步（运行程序）...
pause >nul

REM ========================================
REM 步骤4: 运行程序
REM ========================================
cls
echo ========================================
echo   [步骤4/4] 运行程序
echo ========================================
echo.

echo [4.1] 查找可执行文件...
set "EXE_PATH="
if exist "bin\x86\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\x86\Debug\net9.0-windows\SalesChampion.Windows.exe"
    echo [✓] 找到: %EXE_PATH%
) else if exist "bin\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\Debug\net9.0-windows\SalesChampion.Windows.exe"
    echo [✓] 找到: %EXE_PATH%
) else if exist "bin\Debug\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\Debug\SalesChampion.Windows.exe"
    echo [✓] 找到: %EXE_PATH%
) else if exist "bin\x64\Debug\net9.0-windows\SalesChampion.Windows.exe" (
    set "EXE_PATH=bin\x64\Debug\net9.0-windows\SalesChampion.Windows.exe"
    echo [✓] 找到: %EXE_PATH%
) else (
    echo [✗] 错误: 找不到可执行文件！
    pause
    goto main_menu
)

echo.
echo [4.2] 检查 .NET Desktop Runtime...
echo 注意: 程序需要 .NET Desktop Runtime 9.0.10 (x86)
echo 如果启动失败，请先安装 .NET Desktop Runtime
echo 下载地址: https://dotnet.microsoft.com/download/dotnet/9.0
echo.

echo [4.3] 正在以管理员权限运行程序...
powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"
if %errorlevel% equ 0 (
    echo [✓] 程序已启动
) else (
    echo [✗] 程序启动失败，可能需要管理员权限
)

echo.
echo ========================================
echo [步骤4/4] 运行完成！
echo ========================================
echo.
echo ========================================
echo 全部执行完成！
echo ========================================
echo.
echo 所有步骤已按顺序执行完成：
echo   [✓] 步骤1: 清理编译文件
echo   [✓] 步骤2: 编译项目
echo   [✓] 步骤3: 关闭程序
echo   [✓] 步骤4: 运行程序
echo.
pause
goto main_menu

:exit
cls
echo 感谢使用！
timeout /t 1 /nobreak >nul
exit /b 0
