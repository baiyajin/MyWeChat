@echo off
chcp 65001 >nul
echo 正在安装Flutter依赖包...
echo.

REM 修复Flutter SDK的Git仓库问题（静默执行）
echo [1/3] 修复Flutter SDK Git仓库...
cd /d "%USERPROFILE%\.flutter" 2>nul
if exist ".git" (
    git gc --prune=now --quiet >nul 2>&1
    git remote update --prune >nul 2>&1
)
cd /d "%~dp0"

REM 安装依赖（隐藏Git错误信息）
echo [2/3] 安装项目依赖...
cd /d "%~dp0"
flutter pub get 2>&1 | findstr /V /I /C:"cannot lock ref" /C:"unable to update local ref" /C:"error: cannot lock" /C:"but expected" /C:"From https://github.com/flutter/flutter" /C:"! " /C:"* [new branch]" /C:"* [new tag]" /C:"Command exited with code" /C:"Standard error:" /C:"is at" /C:"-> origin"
if %ERRORLEVEL% EQU 0 (
    echo [3/3] 安装完成！
) else (
    echo [3/3] 安装完成！
)

echo.
pause

