# 清理Git历史中的私密信息

## 问题说明

日志文件（如 `windows.log`）可能包含私密信息（微信ID、账号、头像URL等），如果这些文件已经被提交到Git历史中，需要清理。

## 解决方案

### 1. 更新.gitignore（已完成）

`.gitignore` 已更新，确保所有 `.log` 文件都被忽略：
- `*.log` - 所有日志文件
- `windows.log` - Windows端日志文件
- `windows-*.log` - Windows端相关日志文件

### 2. 清理Git历史中的日志文件

如果日志文件已经被提交到Git历史中，需要使用以下脚本清理：

#### 方法一：使用PowerShell脚本（推荐）

```powershell
cd D:\baiyajin-code\wx-hook\MyWeChat
.\clean-git-history.ps1
```

#### 方法二：使用BAT脚本

```cmd
cd D:\baiyajin-code\wx-hook\MyWeChat
clean-git-history.bat
```

#### 方法三：手动执行命令

如果脚本无法运行，可以手动执行以下命令：

```bash
# 1. 从Git索引中移除.log文件
git rm --cached --ignore-unmatch *.log

# 2. 使用git filter-branch清理历史
git filter-branch --force --index-filter "git rm --cached --ignore-unmatch *.log" --prune-empty --tag-name-filter cat -- --all

# 3. 清理引用
git for-each-ref --format="%(refname)" refs/original/ | ForEach-Object { git update-ref -d $_ }

# 4. 强制垃圾回收
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

### 3. 推送到远程仓库（如果需要）

**警告：这将重写远程仓库的历史！**

```bash
# 强制推送到远程仓库
git push --force --all
git push --force --tags
```

**重要提示：**
- 强制推送会重写远程仓库的历史
- 所有协作者需要重新克隆仓库
- 确保所有协作者都知道这个操作

### 4. 验证清理结果

```bash
# 检查Git历史中是否还有.log文件
git log --all --pretty=format:"%H %s" --name-only | findstr /i "\.log"

# 如果没有任何输出，说明清理成功
```

## 预防措施

### 1. 确保.gitignore规则生效

`.gitignore` 已包含以下规则：
```
*.log
windows.log
windows-*.log
```

### 2. 检查提交前的内容

在提交前，使用以下命令检查是否有日志文件：

```bash
# 检查暂存区中的文件
git status

# 检查是否有.log文件被跟踪
git ls-files | findstr /i "\.log"
```

## 注意事项

1. **备份重要数据**：清理Git历史前，请确保已备份重要数据
2. **通知协作者**：如果已推送到远程仓库，需要通知所有协作者
3. **谨慎操作**：清理Git历史是不可逆的操作，请谨慎执行
4. **测试环境**：建议先在测试仓库中测试清理脚本

## 相关文件

- `.gitignore` - Git忽略规则
- `clean-git-history.ps1` - PowerShell清理脚本
- `clean-git-history.bat` - BAT清理脚本

