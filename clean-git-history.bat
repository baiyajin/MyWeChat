@echo off
chcp 65001 >nul
echo 开始清理Git历史中的日志文件（包含私密信息）...
echo.

REM 1. 确保.gitignore包含*.log规则
echo 1. 检查.gitignore规则...
if exist .gitignore (
    findstr /C:"*.log" .gitignore >nul
    if errorlevel 1 (
        echo    添加*.log规则到.gitignore...
        echo. >> .gitignore
        echo # 日志文件（包含私密信息）>> .gitignore
        echo *.log >> .gitignore
    ) else (
        echo    .gitignore已包含*.log规则
    )
) else (
    echo    创建.gitignore文件...
    echo # 日志文件（包含私密信息）> .gitignore
    echo *.log >> .gitignore
)

REM 2. 从Git索引中移除所有.log文件（如果存在）
echo.
echo 2. 从Git索引中移除.log文件...
git rm --cached --ignore-unmatch *.log 2>nul
if errorlevel 1 (
    echo    没有找到被跟踪的.log文件
) else (
    echo    已从Git索引中移除.log文件
)

REM 3. 使用git filter-branch清理历史
echo.
echo 3. 清理Git历史中的.log文件...
echo    警告：这将重写Git历史，请确保已备份！
echo    按任意键继续，或按Ctrl+C取消...
pause >nul

REM 使用git filter-branch移除所有.log文件
git filter-branch --force --index-filter "git rm --cached --ignore-unmatch *.log" --prune-empty --tag-name-filter cat -- --all

REM 4. 清理引用
echo.
echo 4. 清理Git引用...
for /f "delims=" %%i in ('git for-each-ref --format="%%(refname)" refs/original/') do (
    git update-ref -d "%%i" 2>nul
)
echo    引用清理完成

REM 5. 强制垃圾回收
echo.
echo 5. 执行Git垃圾回收...
git reflog expire --expire=now --all
git gc --prune=now --aggressive
echo    垃圾回收完成

echo.
echo 清理完成！
echo.
echo 重要提示：
echo 1. 如果已推送到远程仓库，需要使用 'git push --force' 强制推送
echo 2. 所有协作者需要重新克隆仓库
echo 3. 确保.gitignore规则已生效，防止以后再次提交日志文件
echo.
pause

