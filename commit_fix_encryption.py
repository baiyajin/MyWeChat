#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import subprocess
import sys
import os

# 获取当前提交的tree和parent
tree_result = subprocess.run(
    ["git", "write-tree"],
    capture_output=True,
    text=True,
    encoding="utf-8"
)
tree = tree_result.stdout.strip()

parent_result = subprocess.run(
    ["git", "rev-parse", "HEAD"],
    capture_output=True,
    text=True,
    encoding="utf-8"
)
parent = parent_result.stdout.strip()

# 获取作者和提交者信息
env_result = subprocess.run(
    ["git", "log", "--format=%an%n%ae%n%cn%n%ce%n%at%n%ct", "-1"],
    capture_output=True,
    text=True,
    encoding="utf-8"
)
env_lines = env_result.stdout.strip().split("\n")

# 设置环境变量
env = os.environ.copy()
env["GIT_AUTHOR_NAME"] = env_lines[0]
env["GIT_AUTHOR_EMAIL"] = env_lines[1]
env["GIT_COMMITTER_NAME"] = env_lines[2]
env["GIT_COMMITTER_EMAIL"] = env_lines[3]
env["GIT_AUTHOR_DATE"] = f"{env_lines[4]} +0800"
env["GIT_COMMITTER_DATE"] = f"{env_lines[5]} +0800"

# 新的提交信息
new_msg = "修复HTTP解密问题：添加DecryptStringForHttp方法避免修改全局会话密钥，修复Windows端登录后手机号映射问题"

# 使用 commit-tree 创建新提交
result = subprocess.run(
    ["git", "commit-tree", "-p", parent, "-m", new_msg, tree],
    capture_output=True,
    text=True,
    encoding="utf-8",
    env=env
)

if result.returncode == 0:
    new_hash = result.stdout.strip()
    print(f"New commit hash: {new_hash}")
    
    # 重置HEAD到新提交
    subprocess.run(["git", "reset", "--soft", new_hash])
    print(f"提交完成: {new_hash}")
    
    # 删除临时脚本
    if os.path.exists("commit_fix_encryption.py"):
        os.remove("commit_fix_encryption.py")
else:
    print(f"Error: {result.stderr}")
    sys.exit(1)

