@echo off
chcp 65001 >nul

echo 正在清理 Flutter 构建缓存...
echo.

cd /d "%~dp0"

REM 清理 Flutter 构建缓存
flutter clean

echo.
echo 清理完成！
echo.
echo 现在可以重新运行 start-edge.bat 启动应用
echo.

pause

