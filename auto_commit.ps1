# 自动提交脚本 - 使用文件方式提交，避免中文乱码
# 使用方法: .\auto_commit.ps1 "提交信息"

param(
    [Parameter(Mandatory=$true)]
    [string]$CommitMessage
)

# 设置编码为UTF-8
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
chcp 65001 | Out-Null

# 检查是否有未提交的更改
$status = git status --porcelain
if ([string]::IsNullOrWhiteSpace($status)) {
    Write-Host "没有需要提交的更改" -ForegroundColor Yellow
    exit 0
}

# 创建提交信息文件（使用UTF-8无BOM编码）
$commitFile = "commit_message.txt"
$utf8NoBom = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($commitFile, $CommitMessage, $utf8NoBom)

# 添加所有更改
git add -A

# 使用文件方式提交
git commit -F $commitFile

# 删除临时文件
Remove-Item $commitFile -ErrorAction SilentlyContinue

Write-Host "提交完成" -ForegroundColor Green
git log --oneline -1

