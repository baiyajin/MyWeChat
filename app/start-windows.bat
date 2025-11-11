@echo off
chcp 65001 >nul

REM 设置环境变量，使用UTF-8编码（解决路径中文问题）
set JAVA_TOOL_OPTIONS=-Dfile.encoding=UTF-8
set GRADLE_OPTS=-Dfile.encoding=UTF-8

cd /d "%~dp0"

REM 检查是否已配置平台支持
if not exist "windows" (
    flutter create . --platforms=windows --no-overwrite >nul 2>&1
)

REM 确保依赖已安装
call flutter pub get >nul 2>&1

REM 检查并修复符号链接问题
if exist "windows\flutter\ephemeral\.plugin_symlinks" (
    REM 如果符号链接有问题，清理 ephemeral 目录
    call flutter clean >nul 2>&1
    call flutter pub get >nul 2>&1
)

REM 先构建一次以确保资源文件被正确安装
call flutter build windows --debug >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo 构建失败！请检查错误信息。
    pause
    exit /b %ERRORLEVEL%
)

REM 启动应用
flutter run -d windows

pause

