# 清理Git历史中的日志文件（包含私密信息）
# 使用方法：在MyWeChat目录下运行此脚本

Write-Host "开始清理Git历史中的日志文件..." -ForegroundColor Yellow

# 1. 确保.gitignore包含*.log规则
Write-Host "`n1. 检查.gitignore规则..." -ForegroundColor Cyan
if (Test-Path ".gitignore") {
    $gitignoreContent = Get-Content ".gitignore" -Raw
    if ($gitignoreContent -notmatch "^\*\.log") {
        Write-Host "   添加*.log规则到.gitignore..." -ForegroundColor Yellow
        Add-Content ".gitignore" "`n# 日志文件（包含私密信息）`n*.log"
    } else {
        Write-Host "   .gitignore已包含*.log规则" -ForegroundColor Green
    }
} else {
    Write-Host "   创建.gitignore文件..." -ForegroundColor Yellow
    "# 日志文件（包含私密信息）`n*.log" | Out-File -FilePath ".gitignore" -Encoding UTF8
}

# 2. 从Git索引中移除所有.log文件（如果存在）
Write-Host "`n2. 从Git索引中移除.log文件..." -ForegroundColor Cyan
git rm --cached --ignore-unmatch *.log 2>&1 | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "   已从Git索引中移除.log文件" -ForegroundColor Green
} else {
    Write-Host "   没有找到被跟踪的.log文件" -ForegroundColor Yellow
}

# 3. 使用git filter-branch清理历史（如果历史中有这些文件）
Write-Host "`n3. 清理Git历史中的.log文件..." -ForegroundColor Cyan
Write-Host "   警告：这将重写Git历史，请确保已备份！" -ForegroundColor Red
Write-Host "   按任意键继续，或按Ctrl+C取消..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# 使用git filter-branch移除所有.log文件
git filter-branch --force --index-filter `
    "git rm --cached --ignore-unmatch *.log" `
    --prune-empty --tag-name-filter cat -- --all 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "   Git历史清理完成" -ForegroundColor Green
} else {
    Write-Host "   清理过程中可能没有找到.log文件，或已清理完成" -ForegroundColor Yellow
}

# 4. 清理引用
Write-Host "`n4. 清理Git引用..." -ForegroundColor Cyan
git for-each-ref --format="%(refname)" refs/original/ | ForEach-Object {
    git update-ref -d $_ 2>&1 | Out-Null
}
Write-Host "   引用清理完成" -ForegroundColor Green

# 5. 强制垃圾回收
Write-Host "`n5. 执行Git垃圾回收..." -ForegroundColor Cyan
git reflog expire --expire=now --all 2>&1 | Out-Null
git gc --prune=now --aggressive 2>&1 | Out-Null
Write-Host "   垃圾回收完成" -ForegroundColor Green

Write-Host "`n清理完成！" -ForegroundColor Green
Write-Host "`n重要提示：" -ForegroundColor Yellow
Write-Host "1. 如果已推送到远程仓库，需要使用 'git push --force' 强制推送" -ForegroundColor Yellow
Write-Host "2. 所有协作者需要重新克隆仓库" -ForegroundColor Yellow
Write-Host "3. 确保.gitignore规则已生效，防止以后再次提交日志文件" -ForegroundColor Yellow

