@echo off
chcp 65001 >nul
echo 正在修复 Flutter 符号链接问题...
echo.

cd /d "%~dp0"

REM 清理构建缓存
echo [1/3] 清理构建缓存...
call flutter clean
echo.

REM 删除可能存在的 ephemeral 目录
if exist "windows\flutter\ephemeral" (
    echo [2/3] 删除 ephemeral 目录...
    rmdir /s /q "windows\flutter\ephemeral" 2>nul
    echo ephemeral 目录已删除
    echo.
)

REM 重新获取依赖
echo [3/3] 重新获取依赖...
call flutter pub get
echo.

echo 修复完成！现在可以重新运行应用了。
echo.
pause

