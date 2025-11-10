"""
导入账号信息到数据库
从 account_info.json 文件读取数据并保存到数据库
"""
import asyncio
import json
import os
from pathlib import Path
from sqlalchemy import select
from app.models.database import AsyncSessionLocal, AccountInfo, init_db


async def import_account_info(json_file_path: str):
    """从JSON文件导入账号信息到数据库"""
    try:
        # 读取JSON文件
        if not os.path.exists(json_file_path):
            print(f"文件不存在: {json_file_path}")
            return False
        
        with open(json_file_path, 'r', encoding='utf-8-sig') as f:
            account_data = json.load(f)
        
        print(f"读取账号信息: {json_file_path}")
        print(f"账号数据: {account_data}")
        
        # 初始化数据库
        await init_db()
        
        # 保存到数据库
        async with AsyncSessionLocal() as session:
            wxid = account_data.get("wxid") or account_data.get("wxId") or account_data.get("WxId")
            if not wxid:
                print("账号信息缺少wxid，无法保存")
                return False
            
            # 检查是否已存在
            stmt = select(AccountInfo).where(AccountInfo.wxid == wxid)
            result = await session.execute(stmt)
            existing = result.scalar_one_or_none()
            
            if existing:
                # 更新现有记录
                existing.nickname = account_data.get("nickname", existing.nickname)
                existing.avatar = account_data.get("avatar", existing.avatar)
                existing.account = account_data.get("account", existing.account)
                existing.device_id = account_data.get("device_id", existing.device_id)
                existing.phone = account_data.get("phone", existing.phone)
                existing.wx_user_dir = account_data.get("wx_user_dir", existing.wx_user_dir)
                existing.unread_msg_count = account_data.get("unread_msg_count", existing.unread_msg_count)
                existing.is_fake_device_id = account_data.get("is_fake_device_id", existing.is_fake_device_id)
                existing.pid = account_data.get("pid", existing.pid)
                print(f"更新账号信息到数据库: wxid={wxid}")
            else:
                # 创建新记录
                account_info = AccountInfo(
                    wxid=wxid,
                    nickname=account_data.get("nickname", ""),
                    avatar=account_data.get("avatar", ""),
                    account=account_data.get("account", ""),
                    device_id=account_data.get("device_id", ""),
                    phone=account_data.get("phone", ""),
                    wx_user_dir=account_data.get("wx_user_dir", ""),
                    unread_msg_count=account_data.get("unread_msg_count", 0),
                    is_fake_device_id=account_data.get("is_fake_device_id", 0),
                    pid=account_data.get("pid", 0)
                )
                session.add(account_info)
                print(f"保存账号信息到数据库: wxid={wxid}")
            
            await session.commit()
            print(f"账号信息已成功保存到数据库: wxid={wxid}, nickname={account_data.get('nickname', '')}")
            return True
            
    except Exception as e:
        print(f"导入账号信息失败: {e}")
        import traceback
        traceback.print_exc()
        return False


async def main():
    """主函数"""
    # 默认文件路径（相对于脚本所在目录）
    # 尝试多个可能的路径
    possible_paths = [
        # Windows客户端目录下的文件
        "../windows/MyWeChat.Windows/bin/x86/Debug/net9.0-windows/account_info.json",
        # 项目根目录下的文件
        "../account_info.json",
        # 当前目录下的文件
        "account_info.json",
    ]
    
    json_file_path = None
    for path in possible_paths:
        full_path = os.path.join(os.path.dirname(__file__), path)
        if os.path.exists(full_path):
            json_file_path = full_path
            break
    
    if not json_file_path:
        # 如果找不到文件，使用绝对路径
        json_file_path = r"D:\baiyajin-code\wx-hook\MyWeChat\windows\MyWeChat.Windows\bin\x86\Debug\net9.0-windows\account_info.json"
        if not os.path.exists(json_file_path):
            print("未找到 account_info.json 文件")
            print("请将 account_info.json 文件放在以下位置之一：")
            for path in possible_paths:
                print(f"  - {os.path.join(os.path.dirname(__file__), path)}")
            return
    
    print(f"使用文件路径: {json_file_path}")
    success = await import_account_info(json_file_path)
    
    if success:
        print("导入成功！")
    else:
        print("导入失败！")


if __name__ == "__main__":
    asyncio.run(main())

