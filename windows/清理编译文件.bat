@echo off
chcp 65001 >nul
echo ========================================
echo MyWeChat Windows端 - 清理编译文件
echo ========================================
echo.

cd /d "%~dp0SalesChampion.Windows"

echo 正在清理编译文件...
echo.

if exist "bin" (
    echo 删除 bin 目录...
    rmdir /s /q "bin"
    echo bin 目录已删除
)

if exist "obj" (
    echo 删除 obj 目录...
    rmdir /s /q "obj"
    echo obj 目录已删除
)

echo.
echo ========================================
echo 清理完成！
echo ========================================
echo.
echo 现在可以重新编译项目了
echo.

pause

