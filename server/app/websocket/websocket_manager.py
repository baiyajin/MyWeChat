"""
WebSocket连接管理器
管理Windows端和App端的WebSocket连接
"""
from fastapi import WebSocket
from typing import List, Dict, Set
import json
import asyncio
from sqlalchemy import select
from app.models.database import AsyncSessionLocal, AccountInfo
from app.services.license_service import LicenseService


class WebSocketManager:
    """WebSocket连接管理器"""
    
    def __init__(self):
        # Windows端连接集合
        self.windows_clients: Set[WebSocket] = set()
        # App端连接集合
        self.app_clients: Set[WebSocket] = set()
        # 临时连接集合（等待client_type消息）
        self.pending_clients: Set[WebSocket] = set()
        # App端连接与微信账号ID的映射关系
        self.app_client_wxid_map: Dict[WebSocket, str] = {}

    async def connect(self, websocket: WebSocket):
        """接受WebSocket连接"""
        await websocket.accept()
        
        # 先添加到临时连接集合，等待client_type消息后再分类
        self.pending_clients.add(websocket)
        print(f"WebSocket连接已建立，等待客户端类型注册（临时连接数: {len(self.pending_clients)}）")

    def disconnect(self, websocket: WebSocket):
        """断开WebSocket连接"""
        if websocket in self.pending_clients:
            self.pending_clients.remove(websocket)
            print(f"临时连接已断开，当前临时连接数: {len(self.pending_clients)}")
        
        if websocket in self.windows_clients:
            self.windows_clients.remove(websocket)
            print(f"Windows端连接已断开，当前连接数: {len(self.windows_clients)}")
        
        if websocket in self.app_clients:
            self.app_clients.remove(websocket)
            # 清理App端映射
            if websocket in self.app_client_wxid_map:
                del self.app_client_wxid_map[websocket]
            print(f"App端连接已断开，当前连接数: {len(self.app_clients)}")

    async def handle_message(self, websocket: WebSocket, message: Dict):
        """处理WebSocket消息"""
        try:
            message_type = message.get("type", "")
            
            print(f"收到WebSocket消息，类型: {message_type}, 来源: {websocket}")
            
            if message_type == "sync_contacts":
                # Windows端同步联系人数据，只转发到App端（不保存到数据库）
                print(f"收到联系人数据同步，转发到App端，数据数量: {len(message.get('data', []))}")
                await self._forward_to_app_clients_by_wxid(message)
            
            elif message_type == "sync_moments":
                # Windows端同步朋友圈数据，只转发到App端（不保存到数据库）
                print(f"收到朋友圈数据同步，转发到App端，数据数量: {len(message.get('data', []))}")
                await self._forward_to_app_clients_by_wxid(message)
            
            elif message_type == "sync_tags":
                # Windows端同步标签数据，只转发到App端（不保存到数据库）
                print(f"收到标签数据同步，转发到App端，数据数量: {len(message.get('data', []))}")
                await self._forward_to_app_clients_by_wxid(message)
            
            elif message_type == "sync_chat_message":
                # Windows端同步聊天消息，只转发到App端（不保存到数据库）
                print("收到聊天消息同步，转发到App端")
                await self._forward_to_app_clients_by_wxid(message)
            
            elif message_type == "sync_my_info":
                # Windows端同步我的信息，保存到数据库并转发到App端
                print("收到账号信息同步，保存到数据库并转发到App端")
                await self._save_account_info_to_db(message.get("data", {}))
                await self.broadcast_to_app_clients(message)
            
            elif message_type == "command":
                # App端发送命令，转发到Windows端
                print("转发命令到Windows端")
                await self.send_to_windows_client(message)
            
            elif message_type == "command_result":
                # Windows端返回命令执行结果，转发到App端
                print("转发命令执行结果到App端")
                await self.send_to_app_client(message)
            
            elif message_type == "client_type":
                # 客户端类型注册
                client_type = message.get("client_type", "")
                print(f"客户端类型注册: {client_type}")
                
                # 先从临时连接集合中移除（如果存在）
                if websocket in self.pending_clients:
                    self.pending_clients.remove(websocket)
                
                # 从其他集合中移除（如果存在）
                if websocket in self.app_clients:
                    self.app_clients.remove(websocket)
                    # 清理App端映射
                    if websocket in self.app_client_wxid_map:
                        del self.app_client_wxid_map[websocket]
                
                if websocket in self.windows_clients:
                    self.windows_clients.remove(websocket)
                
                # 根据client_type添加到对应的集合
                if client_type == "windows":
                    self.windows_clients.add(websocket)
                    print(f"Windows端连接数: {len(self.windows_clients)}")
                elif client_type == "app":
                    self.app_clients.add(websocket)
                    print(f"App端连接数: {len(self.app_clients)}")
                else:
                    print(f"未知的客户端类型: {client_type}，保持为临时连接")
                    self.pending_clients.add(websocket)
            
            elif message_type == "login":
                # App端或Windows端登录请求（手机号+授权码）
                await self._handle_login(websocket, message)
            
            elif message_type == "verify_login_code":
                # App端验证登录码
                await self._handle_verify_login_code(websocket, message)
            
            elif message_type == "quick_login":
                # App端快速登录（使用wxid）
                await self._handle_quick_login(websocket, message)
            
            elif message_type == "set_wxid":
                # App端设置当前微信账号ID（登录成功后）
                wxid = message.get("wxid", "")
                if wxid:
                    self.app_client_wxid_map[websocket] = wxid
                    print(f"App端已设置微信账号ID: {wxid}")
            
        except Exception as e:
            print(f"处理WebSocket消息失败: {e}")
            import traceback
            traceback.print_exc()


    async def broadcast_to_app_clients(self, message: Dict):
        """广播消息到所有App端"""
        await self.send_to_app_client(message)
    
    async def _forward_to_app_clients_by_wxid(self, message: Dict):
        """根据微信账号ID转发消息到对应的App端"""
        # 从消息中提取we_chat_id（可能在不同位置）
        data = message.get("data", [])
        if not data:
            # 如果没有数据，直接转发给所有App端
            await self.broadcast_to_app_clients(message)
            return
        
        # 提取we_chat_id（从第一条数据中获取）
        we_chat_id = None
        if isinstance(data, list) and len(data) > 0:
            first_item = data[0]
            if isinstance(first_item, dict):
                we_chat_id = first_item.get("we_chat_id") or first_item.get("weChatId")
        elif isinstance(data, dict):
            we_chat_id = data.get("we_chat_id") or data.get("weChatId")
        
        if not we_chat_id:
            # 如果无法提取we_chat_id，转发给所有App端
            await self.broadcast_to_app_clients(message)
            return
        
        # 只转发给登录了对应微信账号的App端
        forwarded_count = 0
        for app_client, client_wxid in self.app_client_wxid_map.items():
            if client_wxid == we_chat_id:
                try:
                    message_json = json.dumps(message, ensure_ascii=False)
                    await app_client.send_text(message_json)
                    forwarded_count += 1
                except Exception as e:
                    print(f"转发消息到App端失败: {e}")
        
        if forwarded_count > 0:
            print(f"已转发消息到 {forwarded_count} 个App端（微信账号ID: {we_chat_id}）")
        else:
            print(f"没有找到登录了微信账号 {we_chat_id} 的App端")
    
    async def send_to_windows_client(self, message: Dict):
        """发送消息到Windows端（单播）"""
        if not self.windows_clients:
            print(f"没有Windows端连接（Windows端连接数: {len(self.windows_clients)}, App端连接数: {len(self.app_clients)}, 临时连接数: {len(self.pending_clients)}）")
            return
        
        message_json = json.dumps(message, ensure_ascii=False)
        disconnected = set()
        
        for client in self.windows_clients:
            try:
                await client.send_text(message_json)
                print(f"消息已成功发送到Windows端（命令类型: {message.get('command_type', 'unknown')}）")
                break  # 只发送给第一个连接的Windows客户端
            except Exception as e:
                print(f"发送消息到Windows端失败: {e}")
                disconnected.add(client)
        
        # 移除断开的连接
        for client in disconnected:
            self.disconnect(client)
    
    async def send_to_app_client(self, message: Dict):
        """发送消息到App端（单播）"""
        if not self.app_clients:
            print("没有App端连接，无法转发消息")
            return
        
        message_json = json.dumps(message, ensure_ascii=False)
        disconnected = set()
        
        print(f"正在转发消息到App端，App端连接数: {len(self.app_clients)}")
        
        for client in self.app_clients:
            try:
                await client.send_text(message_json)
                print(f"消息已成功转发到App端，消息类型: {message.get('type', 'unknown')}")
                break  # 只发送给第一个连接的App客户端
            except Exception as e:
                print(f"发送消息到App端失败: {e}")
                disconnected.add(client)
        
        # 移除断开的连接
        for client in disconnected:
            self.disconnect(client)

    async def _save_account_info_to_db(self, account_data: Dict):
        """保存账号信息到数据库"""
        try:
            if not account_data:
                print("账号信息数据为空，跳过保存")
                return

            wxid = account_data.get("wxid") or account_data.get("wxId") or account_data.get("WxId")
            if not wxid:
                print("账号信息缺少wxid，跳过保存")
                return

            async with AsyncSessionLocal() as session:
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
                print(f"账号信息已保存到数据库: wxid={wxid}")
        except Exception as e:
            print(f"保存账号信息到数据库失败: {e}")
            import traceback
            traceback.print_exc()

    async def _handle_login(self, websocket: WebSocket, message: Dict):
        """处理App端或Windows端登录请求（手机号+授权码）"""
        try:
            phone = message.get("phone", "").strip()
            license_key = message.get("license_key", "").strip()
            
            if not phone:
                await websocket.send_text(json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "手机号不能为空"
                }, ensure_ascii=False))
                return
            
            if not license_key:
                await websocket.send_text(json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "授权码不能为空"
                }, ensure_ascii=False))
                return
            
            # 验证授权码
            is_valid, error_msg = await LicenseService.verify_license(phone, license_key)
            
            if not is_valid:
                await websocket.send_text(json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": error_msg or "授权验证失败"
                }, ensure_ascii=False))
                return
            
            # 获取授权信息
            license_info = await LicenseService.get_license_by_phone(phone)
            if not license_info:
                await websocket.send_text(json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "获取授权信息失败"
                }, ensure_ascii=False))
                return
            
            # 登录成功，返回授权信息
            await websocket.send_text(json.dumps({
                "type": "login_response",
                "success": True,
                "message": "登录成功",
                "has_manage_permission": license_info.has_manage_permission
            }, ensure_ascii=False))
            
            print(f"手机号 {phone} 登录成功")
        except Exception as e:
            print(f"处理登录请求失败: {e}")
            import traceback
            traceback.print_exc()
            try:
                await websocket.send_text(json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": f"登录失败: {str(e)}"
                }, ensure_ascii=False))
            except:
                pass
    
    async def _handle_verify_login_code(self, websocket: WebSocket, message: Dict):
        """处理App端或Windows端验证登录码（已废弃，保留用于兼容）"""
        # 此方法已废弃，登录现在直接通过 _handle_login 完成（手机号+授权码）
        await websocket.send_text(json.dumps({
            "type": "verify_login_code_response",
            "success": False,
            "message": "请使用手机号+授权码方式登录"
        }, ensure_ascii=False))
    
    async def _handle_quick_login(self, websocket: WebSocket, message: Dict):
        """处理App端或Windows端快速登录（使用wxid）"""
        try:
            wxid = message.get("wxid", "").strip()
            if not wxid:
                await websocket.send_text(json.dumps({
                    "type": "quick_login_response",
                    "success": False,
                    "message": "微信账号ID不能为空"
                }, ensure_ascii=False))
                return
            
            # 验证wxid是否存在
            async with AsyncSessionLocal() as session:
                stmt = select(AccountInfo).where(AccountInfo.wxid == wxid)
                result = await session.execute(stmt)
                account_info = result.scalar_one_or_none()
                
                if not account_info:
                    await websocket.send_text(json.dumps({
                        "type": "quick_login_response",
                        "success": False,
                        "message": "微信账号不存在"
                    }, ensure_ascii=False))
                    return
                
                # 如果是App端，设置App端的微信账号ID映射
                if websocket in self.app_clients:
                    self.app_client_wxid_map[websocket] = wxid
                
                account_data = {
                    "wxid": account_info.wxid,
                    "nickname": account_info.nickname,
                    "avatar": account_info.avatar,
                    "account": account_info.account,
                    "device_id": account_info.device_id,
                    "phone": account_info.phone,
                    "wx_user_dir": account_info.wx_user_dir,
                    "unread_msg_count": account_info.unread_msg_count,
                    "is_fake_device_id": account_info.is_fake_device_id,
                    "pid": account_info.pid
                }
            
            await websocket.send_text(json.dumps({
                "type": "quick_login_response",
                "success": True,
                "message": "快速登录成功",
                "wxid": wxid,
                "account_info": account_data
            }, ensure_ascii=False))
            
            # 判断是App端还是Windows端
            client_type = "App端" if websocket in self.app_clients else "Windows端"
            print(f"{client_type}快速登录成功: wxid={wxid}")
        except Exception as e:
            print(f"快速登录失败: {e}")
            import traceback
            traceback.print_exc()
            try:
                await websocket.send_text(json.dumps({
                    "type": "quick_login_response",
                    "success": False,
                    "message": f"快速登录失败: {str(e)}"
                }, ensure_ascii=False))
            except:
                pass


# 全局WebSocket管理器实例
websocket_manager = WebSocketManager()

