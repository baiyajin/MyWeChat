@echo off
chcp 65001 >nul 2>&1

REM ========================================
REM 自动请求管理员权限
REM ========================================
>nul 2>&1 "%SYSTEMROOT%\system32\cacls.exe" "%SYSTEMROOT%\system32\config\system"
if '%errorlevel%' NEQ '0' (
    echo "正在请求管理员权限..."
    echo.
    REM 使用PowerShell请求管理员权限并重新运行脚本
    powershell -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

REM 确认已获得管理员权限
echo "[管理员权限] 已获得管理员权限"
echo.

setlocal enabledelayedexpansion
set "PROJECT_DIR=%~dp0MyWeChat.Windows"
set "EXE_NAME=app.exe"
set "PROCESS_NAME=app.exe"
set "WECHAT_PROCESS_NAME=WeChat.exe"

REM 错误处理：确保窗口不会立即关闭
if errorlevel 1 (
    call :echo_red "[错误] 脚本初始化失败"
    pause
    exit /b 1
)

REM 直接执行全部步骤（1-2-3-4），不显示菜单
goto all

REM ========================================
REM 颜色输出辅助函数
REM ========================================
REM Output green text (success/OK)
:echo_green
powershell -NoProfile -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Host \"%~1\" -ForegroundColor Green; exit 0"
goto :eof

REM Output red text (error/failure)
:echo_red
powershell -NoProfile -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Host \"%~1\" -ForegroundColor Red; exit 0"
goto :eof

REM Output orange text (warning)
:echo_yellow
powershell -NoProfile -Command "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; Write-Host \"%~1\" -ForegroundColor DarkYellow; exit 0"
goto :eof

REM 查找可执行文件的函数
:find_exe
set "EXE_PATH="
if exist "bin\x86\Debug\net9.0-windows\%EXE_NAME%" (
    set "EXE_PATH=bin\x86\Debug\net9.0-windows\%EXE_NAME%"
) else if exist "bin\Debug\net9.0-windows\%EXE_NAME%" (
    set "EXE_PATH=bin\Debug\net9.0-windows\%EXE_NAME%"
) else if exist "bin\Debug\%EXE_NAME%" (
    set "EXE_PATH=bin\Debug\%EXE_NAME%"
) else if exist "bin\x64\Debug\net9.0-windows\%EXE_NAME%" (
    set "EXE_PATH=bin\x64\Debug\net9.0-windows\%EXE_NAME%"
)
goto :eof

REM 关闭程序的函数
:close_process
set "PROCESS_FOUND=0"
set "PROCESS_PID="
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
    set "PROCESS_PID=%%p"
    set "PROCESS_FOUND=1"
)
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行"
    goto :eof
)
echo "检测到程序正在运行 (PID: !PROCESS_PID!)，正在关闭"
for /L %%i in (1,1,10) do (
    if defined PROCESS_PID (
        taskkill /F /PID !PROCESS_PID! >NUL 2>&1
        timeout /t 1 /nobreak >nul 2>&1
        tasklist /FI "PID eq !PROCESS_PID!" 2>NUL | find /I /N "!PROCESS_PID!">NUL
        set "PROCESS_EXISTS=!ERRORLEVEL!"
        if !PROCESS_EXISTS! neq 0 (
            call :echo_green "[✓] 程序已成功关闭"
            echo [等待] 等待文件句柄释放（3秒）...
            timeout /t 3 /nobreak >nul 2>&1
            call :echo_green "[✓] 文件句柄已释放"
            set "PROCESS_PID="
            goto :eof
        )
    ) else (
        tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
        set "PROCESS_EXISTS=!ERRORLEVEL!"
        if !PROCESS_EXISTS! neq 0 (
            call :echo_green "[✓] 程序已成功关闭"
            echo [等待] 等待文件句柄释放（3秒）...
            timeout /t 3 /nobreak >nul 2>&1
            call :echo_green "[✓] 文件句柄已释放"
            goto :eof
        )
    )
)
call :echo_yellow "[X] 警告: 无法彻底关闭程序"
echo "进程名称: %PROCESS_NAME%"
if defined PROCESS_PID (
    echo "进程PID: !PROCESS_PID!"
)
echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
goto :eof

REM 主菜单已移除，直接执行全部步骤

:clean
cls
echo ========================================
echo "    功能1: 清理编译文件"
echo ========================================
echo.
cd /d "%PROJECT_DIR%"
echo "正在清理编译文件..."
if exist "bin" (
    echo "删除 bin 目录..."
    rmdir /s /q "bin" 2>nul
)
if exist "obj" (
    echo "删除 obj 目录..."
    rmdir /s /q "obj" 2>nul
)
echo "清理完成！"
pause
goto main_menu

:build
cls
echo ========================================
echo "    功能2: 编译项目"
echo ========================================
echo.
cd /d "%PROJECT_DIR%"

echo "[1/4] 检查.NET SDK版本..."
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    call :echo_red "错误: 未安装.NET SDK，请先安装.NET 9.0 SDK"
    echo "下载地址: https://dotnet.microsoft.com/download/dotnet/9.0"
    pause
    goto main_menu
)

echo "[2/4] 检查并关闭已运行的程序..."
set "PROCESS_FOUND=0"
set "PROCESS_PID="
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
    set "PROCESS_PID=%%p"
    set "PROCESS_FOUND=1"
)
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行"
) else (
    echo "检测到程序正在运行 (PID: !PROCESS_PID!)"
    echo "正在关闭程序..."
    for /L %%i in (1,1,10) do (
        if defined PROCESS_PID (
            taskkill /F /PID !PROCESS_PID! >NUL 2>&1
            timeout /t 1 /nobreak >nul 2>&1
            tasklist /FI "PID eq !PROCESS_PID!" 2>NUL | find /I /N "!PROCESS_PID!">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                set "PROCESS_PID="
                goto process_closed2
            )
        ) else (
            tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                goto process_closed2
            )
        )
    )
    call :echo_yellow "[X] 警告: 无法彻底关闭程序"
    echo "进程名称: %PROCESS_NAME%"
    if defined PROCESS_PID (
        echo "进程PID: !PROCESS_PID!"
    )
    echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
)
:process_closed2
tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
if errorlevel 1 (
    call :echo_green "[✓] 程序已关闭"
    echo "[等待] 等待文件句柄释放（3秒）..."
    timeout /t 3 /nobreak >nul 2>&1
    call :echo_green "[✓] 文件句柄已释放，可以继续编译"
) else (
    call :echo_yellow "[X] 警告: 无法彻底关闭程序，编译可能会失败"
    echo "进程名称: %PROCESS_NAME%"
    for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
        echo "进程PID: %%p"
    )
    echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
    echo.
    set /p "SKIP_CLOSE=是否跳过此步骤继续编译？(Y/N): "
    if /i not "!SKIP_CLOSE!"=="Y" (
        echo.
        call :echo_red "[X] 编译已取消，请手动关闭程序后重试"
        pause >nul 2>&1
        goto main_menu
    )
)

echo "[3/4] 诊断文件锁定问题..."
echo "正在检查可能占用文件的进程..."
echo.

REM 检查程序进程
tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
if errorlevel 1 (
    call :echo_green "[✓] %PROCESS_NAME% 未运行"
) else (
    call :echo_red "[X] 检测到进程正在运行"
    echo "   进程名称: %PROCESS_NAME%"
    echo "   可能占用编译输出文件"
    for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
        echo    进程PID: %%p
    )
)

REM 检查微信进程
tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" 2>NUL | find /I /N "%WECHAT_PROCESS_NAME%">NUL
if errorlevel 1 (
    echo [✓] 微信进程未运行
) else (
    call :echo_red "[✗] 微信进程正在运行，可能占用注入的DLL文件"
    for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
        echo    进程PID: %%p
    )
    echo.
    echo "说明: 如果微信正在使用注入的DLL，相关的PDB文件可能被锁定"
    echo "建议: 关闭微信进程后再编译，或者先关闭程序再关闭微信"
    echo.
    set /p "CLOSE_WECHAT=是否关闭微信进程？(Y/N): "
    if /i "!CLOSE_WECHAT!"=="Y" (
        echo "正在关闭微信进程..."
        taskkill /F /IM "%WECHAT_PROCESS_NAME%" >NUL 2>&1
        tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" 2>NUL | find /I /N "%WECHAT_PROCESS_NAME%">NUL
        if errorlevel 1 (
            echo "微信进程已关闭"
        ) else (
            call :echo_yellow "警告: 无法关闭微信进程，可能需要管理员权限"
        )
    ) else (
        call :echo_red "跳过关闭微信进程，编译可能会失败"
    )
)

echo "[4/4] 还原NuGet包..."
dotnet restore
if %errorlevel% neq 0 (
    call :echo_red "错误: NuGet包还原失败"
    pause
    goto main_menu
)

echo "[5/5] 编译项目..."
echo.
echo "注意: 如果编译失败，可能是文件被锁定"
echo "建议: 关闭Visual Studio或其他可能锁定文件的程序"
echo.
echo "[5.1] 强制删除可能被锁定的PDB文件..."
if exist "bin\x86\Debug\net9.0-windows\app.pdb" (
    del /f /q "bin\x86\Debug\net9.0-windows\app.pdb" >nul 2>&1
    if exist "bin\x86\Debug\net9.0-windows\app.pdb" (
        call :echo_yellow "[X] 警告: 无法删除bin目录中的PDB文件，可能被其他进程占用"
        echo "建议: 关闭Visual Studio或其他可能锁定文件的程序"
    ) else (
        call :echo_green "[✓] bin目录中的PDB文件已删除"
    )
)
if exist "obj\x86\Debug\net9.0-windows\app.pdb" (
    del /f /q "obj\x86\Debug\net9.0-windows\app.pdb" >nul 2>&1
    if exist "obj\x86\Debug\net9.0-windows\app.pdb" (
        call :echo_yellow "[X] 警告: 无法删除obj目录中的PDB文件"
    ) else (
        call :echo_green "[✓] obj目录中的PDB文件已删除"
    )
)
echo.
dotnet build -c Debug
if %errorlevel% neq 0 (
    echo.
    call :echo_red "[X] 错误: 编译失败"
    echo.
    echo "可能的原因:"
    echo "  1. PDB文件被锁定（最常见）"
    echo "  2. Visual Studio或其他调试器正在附加"
    echo "  3. 程序进程可能还在运行"
    echo "  4. 微信进程可能正在使用注入的DLL"
    echo.
    echo "解决方法:"
    echo "  1. 关闭Visual Studio或其他IDE"
    echo "  2. 关闭所有app.exe进程"
    echo "  3. 关闭微信进程（如果正在使用注入的DLL）"
    echo "  4. 重新运行编译"
    echo.
    pause
    goto main_menu
)

echo.
echo ========================================
echo "编译成功！"
echo ========================================
pause
goto main_menu

:close
cls
echo ========================================
echo "    功能3: 关闭程序"
echo ========================================
echo.
set "PROCESS_FOUND=0"
set "PROCESS_PID="
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
    set "PROCESS_PID=%%p"
    set "PROCESS_FOUND=1"
)
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行"
) else (
    echo "检测到程序正在运行 (PID: !PROCESS_PID!)"
    echo "正在关闭程序..."
    for /L %%i in (1,1,10) do (
        if defined PROCESS_PID (
            taskkill /F /PID !PROCESS_PID! >NUL 2>&1
            tasklist /FI "PID eq !PROCESS_PID!" 2>NUL | find /I /N "!PROCESS_PID!">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                set "PROCESS_PID="
                goto process_closed3
            )
        ) else (
            tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                goto process_closed3
            )
        )
    )
    call :echo_yellow "[X] 警告: 无法彻底关闭程序"
    echo "进程名称: %PROCESS_NAME%"
    if defined PROCESS_PID (
        echo "进程PID: !PROCESS_PID!"
    )
    echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
)
:process_closed3
echo.
echo "完成"
pause
goto main_menu

:run
cls
echo ========================================
echo "    功能4: 运行程序"
echo ========================================
echo.
cd /d "%PROJECT_DIR%"
call :find_exe

if "%EXE_PATH%"=="" (
    call :echo_red "错误: 找不到可执行文件！"
    echo "请先编译项目（选项2）"
    pause
    goto main_menu
)

echo "找到可执行文件: %EXE_PATH%"
echo.
echo ========================================
echo "重要提示: .NET Desktop Runtime 检查"
echo ========================================
echo.
echo "程序需要 .NET Desktop Runtime 9.0.10 (x86)"
echo.
echo "如果启动时出现错误提示："
echo "You must install .NET Desktop Runtime to run this application"
echo.
echo "请先安装 .NET Desktop Runtime："
echo "下载地址: https://dotnet.microsoft.com/download/dotnet/9.0"
echo "选择: .NET Desktop Runtime 9.0.10 (x86)"
echo.
echo "是否现在运行程序？(Y/N)"
set /p "run_choice= "
if /i not "%run_choice%"=="Y" (
    goto main_menu
)

echo.
echo "正在以管理员权限运行程序..."
powershell -Command "Start-Process '%CD%\%EXE_PATH%' -Verb RunAs"

goto main_menu

:all
cls

REM 错误捕获：确保能看到错误信息
set "ERROR_OCCURRED=0"

REM 检查项目目录
if not exist "%PROJECT_DIR%" (
    call :echo_red "[X] 错误: 项目目录不存在: %PROJECT_DIR%"
    echo "请检查脚本路径是否正确"
    pause >nul 2>&1
    exit /b 1
)

cd /d "%PROJECT_DIR%"
set "CD_ERROR=!errorlevel!"
if !CD_ERROR! neq 0 (
    call :echo_red "[X] 错误: 无法切换到项目目录: %PROJECT_DIR%"
    pause >nul 2>&1
    exit /b 1
)

REM ========================================
REM 步骤1: 清理编译文件
REM ========================================
echo.
echo ========================================
echo "  [步骤1/4] 清理编译文件"
echo ========================================
echo.
echo "正在清理编译文件..."
echo.

set "CLEAN_ERROR=0"
if exist "bin" (
    echo "[1.1] 删除 bin 目录..."
    rmdir /s /q "bin" 2>nul
    set "RMDIR_ERROR=!errorlevel!"
    if !RMDIR_ERROR! equ 0 (
        call :echo_green "[✓] bin 目录已删除"
    ) else (
        call :echo_red "[X] bin 目录删除失败（可能被占用）"
        set "CLEAN_ERROR=1"
    )
) else (
    call :echo_green "[✓] bin 目录不存在，无需清理"
)

if exist "obj" (
    echo "[1.2] 删除 obj 目录..."
    rmdir /s /q "obj" 2>nul
    set "RMDIR_ERROR=!errorlevel!"
    if !RMDIR_ERROR! equ 0 (
        call :echo_green "[✓] obj 目录已删除"
    ) else (
        call :echo_red "[X] obj 目录删除失败（可能被占用）"
        set "CLEAN_ERROR=1"
    )
) else (
    call :echo_green "[✓] obj 目录不存在，无需清理"
)

if !CLEAN_ERROR! equ 1 (
    echo.
    call :echo_yellow "[X] 警告: 清理过程中有错误，可能影响编译"
    echo "建议: 手动关闭可能占用文件的程序后重试"
    echo.
    set /p "CONTINUE_CLEAN=是否继续执行编译？(Y/N): "
    if /i not "!CONTINUE_CLEAN!"=="Y" (
        call :echo_red "[X] 用户取消，退出执行"
        pause
        exit /b 1
    )
)

echo.
echo ========================================
echo "[步骤1/4] 清理完成！"
echo ========================================
echo.

REM ========================================
REM 步骤2: 编译项目
REM ========================================
echo.
echo ========================================
echo "  [步骤2/4] 编译项目"
echo ========================================
echo.

echo "[2.1] 检查.NET SDK版本..."
dotnet --version
set "DOTNET_ERROR=!errorlevel!"
if !DOTNET_ERROR! neq 0 (
    call :echo_red "[X] 错误: 未安装.NET SDK，请先安装.NET 9.0 SDK"
    echo "下载地址: https://dotnet.microsoft.com/download/dotnet/9.0"
    pause
    exit /b 1
)
call :echo_green "[✓] .NET SDK 已安装"

echo.
echo "[2.2] 检查并关闭已运行的程序"
set "PROCESS_FOUND=0"
set "PROCESS_PID="
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
    set "PROCESS_PID=%%p"
    set "PROCESS_FOUND=1"
)
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行"
    set "PROCESS_CLOSED=1"
) else (
    echo "检测到程序正在运行 (PID: !PROCESS_PID!)"
    echo "正在关闭程序..."
    for /L %%i in (1,1,10) do (
        if defined PROCESS_PID (
            taskkill /F /PID !PROCESS_PID! >NUL 2>&1
            timeout /t 1 /nobreak >nul 2>&1
            tasklist /FI "PID eq !PROCESS_PID!" 2>NUL | find /I /N "!PROCESS_PID!">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                set "PROCESS_PID="
                goto process_closed_all
            )
        ) else (
            tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                goto process_closed_all
            )
        )
    )
    call :echo_yellow "[X] 警告: 无法彻底关闭程序"
    echo "进程名称: %PROCESS_NAME%"
    if defined PROCESS_PID (
        echo "进程PID: !PROCESS_PID!"
    )
    echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
    echo.
    echo "[*] 自动跳过进程关闭，继续编译..."
    set "PROCESS_CLOSED=1"
    goto process_closed_all
)
:process_closed_all
tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
if errorlevel 1 (
    if not defined PROCESS_CLOSED (
        call :echo_green "[✓] 程序已关闭"
        timeout /t 1 /nobreak >nul 2>&1
        set "PROCESS_CLOSED=1"
    )
)

echo.
echo "[2.3] 检查可能占用文件的进程..."
echo.

REM 检查程序进程
tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
if errorlevel 1 (
    call :echo_green "[✓] %PROCESS_NAME% 未运行"
) else (
    call :echo_red "[X] 检测到进程正在运行"
    echo "   进程名称: %PROCESS_NAME%"
    echo "   可能占用编译输出文件"
    for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
        echo    进程PID: %%p
    )
)

REM 检查微信进程
tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" 2>NUL | find /I /N "%WECHAT_PROCESS_NAME%">NUL
if errorlevel 1 (
    echo [✓] 微信进程未运行
) else (
    echo "[✗] 微信进程正在运行，可能占用注入的DLL文件"
    for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
        echo "   进程PID: %%p"
    )
    echo.
    echo "说明: 如果微信正在使用注入的DLL，相关的PDB文件可能被锁定"
    echo "建议: 关闭微信进程后再编译，或者先关闭程序再关闭微信"
    echo.
    echo "[*] 自动尝试关闭微信进程..."
    taskkill /F /IM "%WECHAT_PROCESS_NAME%" >NUL 2>&1
    tasklist /FI "IMAGENAME eq %WECHAT_PROCESS_NAME%" 2>NUL | find /I /N "%WECHAT_PROCESS_NAME%">NUL
    if errorlevel 1 (
        call :echo_green "[✓] 微信进程已关闭"
    ) else (
        call :echo_yellow "[*] 警告: 无法关闭微信进程，可能需要管理员权限，继续编译..."
    )
)

echo.
echo "[2.4] 还原NuGet包..."
dotnet restore
set "RESTORE_ERROR=!errorlevel!"
if !RESTORE_ERROR! neq 0 (
    call :echo_red "[X] 错误: NuGet包还原失败"
    pause
    exit /b 1
)
call :echo_green "[✓] NuGet包还原成功"

echo.
echo "[2.5] 编译项目..."
echo.
echo "[2.5.1] 强制删除可能被锁定的PDB文件..."
set "PDB_DELETED=0"
if exist "bin\x86\Debug\net9.0-windows\app.pdb" (
    del /f /q "bin\x86\Debug\net9.0-windows\app.pdb" >nul 2>&1
    if exist "bin\x86\Debug\net9.0-windows\app.pdb" (
        call :echo_yellow "[X] 警告: 无法删除bin目录中的PDB文件，可能被其他进程占用"
        echo "建议: 关闭Visual Studio或其他可能锁定文件的程序"
        set "PDB_DELETED=1"
    ) else (
        call :echo_green "[✓] bin目录中的PDB文件已删除"
    )
)
if exist "obj\x86\Debug\net9.0-windows\app.pdb" (
    del /f /q "obj\x86\Debug\net9.0-windows\app.pdb" >nul 2>&1
    if exist "obj\x86\Debug\net9.0-windows\app.pdb" (
        call :echo_yellow "[X] 警告: 无法删除obj目录中的PDB文件"
        set "PDB_DELETED=1"
    ) else (
        call :echo_green "[✓] obj目录中的PDB文件已删除"
    )
)

REM 如果PDB文件无法删除，等待一段时间让文件句柄释放
if !PDB_DELETED! equ 1 (
    echo.
    echo "[2.5.2] 等待文件句柄释放（2秒）..."
    timeout /t 2 /nobreak >nul 2>&1
    call :echo_green "[✓] 等待完成"
)

echo.
echo "[2.5.3] 开始编译主项目..."
REM 使用 --no-incremental 选项强制完全重新编译，避免文件锁定问题
dotnet build -c Debug --no-incremental
set "BUILD_ERROR=!errorlevel!"
if !BUILD_ERROR! neq 0 (
    echo.
    echo ========================================
    call :echo_red "[X] 错误: 编译失败，停止执行后续步骤"
    echo ========================================
    echo.
    echo "编译失败，不会执行后续步骤："
    echo "  [X] 步骤3: 关闭程序（已跳过）"
    echo "  [X] 步骤4: 运行程序（已跳过）"
    echo.
    echo "请修复编译错误后重新运行脚本"
    echo.
    pause
    exit /b 1
)

echo.
echo "[2.5.4] 编译启动器项目（uniapp）..."
cd /d "%~dp0uniapp"
if exist "uniapp.csproj" (
    dotnet build -c Debug --no-incremental
    set "UNIAPP_BUILD_ERROR=!errorlevel!"
    if !UNIAPP_BUILD_ERROR! neq 0 (
        call :echo_yellow "[X] 警告: 启动器项目编译失败，将使用app.exe直接启动"
    ) else (
        call :echo_green "[✓] 启动器项目编译成功"
    )
) else (
    call :echo_yellow "[X] 警告: 找不到启动器项目文件，将使用app.exe直接启动"
)
cd /d "%~dp0MyWeChat.Windows"

echo.
echo "[2.5.5] 复制启动器文件到输出目录..."
set "OUTPUT_DIR=bin\x86\Debug\net9.0-windows"
if not exist "%OUTPUT_DIR%" (
    set "OUTPUT_DIR=bin\Debug\net9.0-windows"
)
if not exist "%OUTPUT_DIR%" (
    set "OUTPUT_DIR=bin\Debug"
)

if exist "%~dp0uniapp\bin\x86\Debug\net9.0-windows\uniapp.exe" (
    copy /Y "%~dp0uniapp\bin\x86\Debug\net9.0-windows\uniapp.exe" "%OUTPUT_DIR%\" >nul 2>&1
    if exist "%OUTPUT_DIR%\uniapp.exe" (
        call :echo_green "[✓] uniapp.exe 已复制到输出目录"
    ) else (
        call :echo_yellow "[X] 警告: 无法复制 uniapp.exe"
    )
) else if exist "%~dp0uniapp\bin\Debug\net9.0-windows\uniapp.exe" (
    copy /Y "%~dp0uniapp\bin\Debug\net9.0-windows\uniapp.exe" "%OUTPUT_DIR%\" >nul 2>&1
    if exist "%OUTPUT_DIR%\uniapp.exe" (
        call :echo_green "[✓] uniapp.exe 已复制到输出目录"
    ) else (
        call :echo_yellow "[X] 警告: 无法复制 uniapp.exe"
    )
) else (
    call :echo_yellow "[X] 警告: 找不到 uniapp.exe，将使用 app.exe 直接启动"
)

if exist "%~dp0uniapp\process_names.txt" (
    copy /Y "%~dp0uniapp\process_names.txt" "%OUTPUT_DIR%\" >nul 2>&1
    if exist "%OUTPUT_DIR%\process_names.txt" (
        call :echo_green "[✓] process_names.txt 已复制到输出目录"
    ) else (
        call :echo_yellow "[X] 警告: 无法复制 process_names.txt"
    )
) else (
    call :echo_yellow "[X] 警告: 找不到 process_names.txt"
)

echo.
echo ========================================
echo "[步骤2/4] 编译完成！"
echo ========================================
echo.

REM ========================================
REM 步骤3: 关闭程序
REM ========================================
echo.
echo ========================================
echo "  [步骤3/4] 关闭程序"
echo ========================================
echo.

set "PROCESS_FOUND=0"
set "PROCESS_PID="
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq %PROCESS_NAME%" /FO LIST 2^>NUL ^| findstr /I "PID:"') do (
    set "PROCESS_PID=%%p"
    set "PROCESS_FOUND=1"
)
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行"
) else (
    echo "检测到程序正在运行 (PID: !PROCESS_PID!)"
    echo "正在关闭程序..."
    for /L %%i in (1,1,10) do (
        if defined PROCESS_PID (
            taskkill /F /PID !PROCESS_PID! >NUL 2>&1
            timeout /t 1 /nobreak >nul 2>&1
            tasklist /FI "PID eq !PROCESS_PID!" 2>NUL | find /I /N "!PROCESS_PID!">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                set "PROCESS_PID="
                goto process_closed4
            )
        ) else (
            tasklist /FI "IMAGENAME eq %PROCESS_NAME%" 2>NUL | find /I /N "%PROCESS_NAME%">NUL
            set "PROCESS_EXISTS=!ERRORLEVEL!"
            if !PROCESS_EXISTS! neq 0 (
                call :echo_green "[✓] 程序已成功关闭"
                goto process_closed4
            )
        )
    )
    call :echo_yellow "[X] 警告: 无法彻底关闭程序"
    echo "进程名称: %PROCESS_NAME%"
    if defined PROCESS_PID (
        echo "进程PID: !PROCESS_PID!"
    )
    echo "请手动在任务管理器中结束进程 "%PROCESS_NAME%" 后重试"
    echo.
    set /p "CONTINUE_CLOSE=是否继续执行运行程序？(Y/N): "
    if /i not "!CONTINUE_CLOSE!"=="Y" (
        call :echo_red "[X] 用户取消，退出执行"
        pause
        exit /b 1
    )
)
:process_closed4
if "!PROCESS_FOUND!"=="0" (
    call :echo_green "[✓] 程序未运行，无需关闭"
) else (
    call :echo_green "[✓] 程序已关闭"
    timeout /t 1 /nobreak >nul 2>&1
)

echo.
echo ========================================
echo "[步骤3/4] 关闭完成！"
echo ========================================
echo.

REM ========================================
REM 步骤4: 运行程序
REM ========================================
echo.
echo ========================================
echo "  [步骤4/4] 运行程序"
echo ========================================
echo.

echo "[4.1] 查找可执行文件..."
call :find_exe

if "%EXE_PATH%"=="" (
    call :echo_red "[✗] 错误: 找不到可执行文件！"
    pause
    exit /b 1
)
call :echo_green "[✓] 找到: %EXE_PATH%"

echo.
echo "[4.3] 检查启动器..."
set "LAUNCHER_PATH="
REM 从EXE_PATH提取目录路径并构建完整路径
REM Build full EXE path and extract directory
for %%F in ("%CD%\%EXE_PATH%") do (
    REM %%~dpF returns absolute path directory part (with backslash)
    set "LAUNCHER_PATH=%%~dpFuniapp.exe"
)
REM 检查启动器是否存在
if exist "!LAUNCHER_PATH!" (
    call :echo_green "[✓] 找到启动器: uniapp.exe"
    echo "[4.4] 正在以管理员权限运行启动器（将随机化进程名称）..."
    echo "[提示] 启动器窗口将显示随机进程名称，请查看弹出的窗口"
    echo.
    REM Note: -Verb RunAs will start new window, cannot use -NoNewWindow
    REM Launcher will display random process name in new window
    REM Set environment variable for PowerShell to use
    set "TEMP_LAUNCHER_PATH=!LAUNCHER_PATH!"
    powershell -Command "$path = $env:TEMP_LAUNCHER_PATH; Start-Process -FilePath $path -Verb RunAs"
    set "TEMP_LAUNCHER_PATH="
) else (
    echo "[*] 未找到启动器，将直接运行 app.exe"
    echo "[4.4] 正在以管理员权限运行程序..."
    powershell -Command "Start-Process -FilePath \"%CD%\%EXE_PATH%\" -Verb RunAs"
)
set "START_ERROR=!errorlevel!"
if !START_ERROR! equ 0 (
    call :echo_green "[✓] 程序已启动"
) else (
    call :echo_red "[X] 程序启动失败，可能需要管理员权限"
    call :echo_red "[X] 步骤4执行失败，退出"
    pause
    exit /b 1
)

echo.
echo ========================================
echo "[步骤4/4] 运行完成！"
echo ========================================
echo.
pause >nul 2>&1
exit /b 0
