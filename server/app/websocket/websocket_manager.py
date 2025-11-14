"""
WebSocket连接管理器
管理Windows端和App端的WebSocket连接
"""
from fastapi import WebSocket
from typing import List, Dict, Set
import json
import asyncio
import base64
from sqlalchemy import select
from app.models.database import AsyncSessionLocal, AccountInfo
from app.services.license_service import LicenseService
from app.utils.encryption_service import encryption_service
from app.utils.rsa_key_manager import rsa_key_manager


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
        # WebSocket连接与登录手机号的映射关系（用于验证手机号匹配）
        self.websocket_phone_map: Dict[WebSocket, str] = {}
        # Windows端连接与手机号的映射关系（用于权限控制）
        self.windows_client_phone_map: Dict[WebSocket, str] = {}

    async def connect(self, websocket: WebSocket):
        """接受WebSocket连接"""
        await websocket.accept()
        
        # 先添加到临时连接集合，等待client_type消息后再分类
        self.pending_clients.add(websocket)
        print(f"WebSocket连接已建立，等待客户端类型注册（临时连接数: {len(self.pending_clients)}）")
        
        # 发送RSA公钥给客户端（用于密钥交换）
        try:
            public_key_pem = rsa_key_manager.get_public_key_pem()
            await websocket.send_text(json.dumps({
                "type": "rsa_public_key",
                "public_key": public_key_pem
            }, ensure_ascii=False))
            print("已发送RSA公钥给客户端")
        except Exception as e:
            print(f"发送RSA公钥失败: {e}")

    def disconnect(self, websocket: WebSocket):
        """断开WebSocket连接"""
        # 清理会话密钥
        connection_id = self._get_connection_id(websocket)
        encryption_service.remove_session_key(connection_id)
        
        if websocket in self.pending_clients:
            self.pending_clients.remove(websocket)
            print(f"临时连接已断开，当前临时连接数: {len(self.pending_clients)}")
        
        if websocket in self.windows_clients:
            self.windows_clients.remove(websocket)
            # 清理Windows端与手机号的映射
            if websocket in self.windows_client_phone_map:
                del self.windows_client_phone_map[websocket]
            print(f"Windows端连接已断开，当前连接数: {len(self.windows_clients)}")
        
        if websocket in self.app_clients:
            self.app_clients.remove(websocket)
            # 清理App端映射
            if websocket in self.app_client_wxid_map:
                del self.app_client_wxid_map[websocket]
            print(f"App端连接已断开，当前连接数: {len(self.app_clients)}")
        
        # 清理登录手机号映射
        if websocket in self.websocket_phone_map:
            del self.websocket_phone_map[websocket]
    
    def _encrypt_message(self, websocket: WebSocket, message_json: str) -> str:
        """加密消息（辅助方法，使用会话密钥）"""
        try:
            connection_id = self._get_connection_id(websocket)
            
            # 如果会话密钥已设置，使用会话密钥加密
            if encryption_service.has_session_key(connection_id):
                encrypted_message = encryption_service.encrypt_string_for_communication(connection_id, message_json)
                message_wrapper = {
                    "encrypted": True,
                    "data": encrypted_message
                }
                return json.dumps(message_wrapper, ensure_ascii=False)
            else:
                # 会话密钥未设置，使用明文（向后兼容）
                print(f"警告: 连接 {connection_id[:8]}... 的会话密钥未设置，使用明文发送")
                return message_json
        except Exception as e:
            print(f"加密消息失败，使用明文: {e}")
            # 如果加密失败，使用明文（向后兼容）
            return message_json

    def _get_connection_id(self, websocket: WebSocket) -> str:
        """获取WebSocket连接的唯一ID"""
        return str(id(websocket))
    
    async def handle_message(self, websocket: WebSocket, message: Dict):
        """处理WebSocket消息"""
        try:
            message_type = message.get("type", "")
            
            print(f"收到WebSocket消息，类型: {message_type}, 来源: {websocket}")
            
            # 处理会话密钥交换
            if message_type == "session_key":
                encrypted_key_b64 = message.get("encrypted_key")
                if encrypted_key_b64:
                    try:
                        # 使用RSA私钥解密会话密钥
                        session_key = rsa_key_manager.decrypt_session_key(encrypted_key_b64)
                        
                        # 保存会话密钥
                        connection_id = self._get_connection_id(websocket)
                        encryption_service.set_session_key(connection_id, session_key)
                        
                        # 发送密钥交换成功消息
                        await websocket.send_text(json.dumps({
                            "type": "key_exchange_success"
                        }, ensure_ascii=False))
                        
                        print(f"会话密钥交换成功（连接ID: {connection_id[:8]}...）")
                        return
                    except Exception as e:
                        print(f"处理会话密钥失败: {e}")
                        return
            
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
            
            elif message_type == "sync_official_account":
                # Windows端同步公众号消息，只转发到App端（不保存到数据库）
                print("收到公众号消息同步，转发到App端")
                await self._forward_to_app_clients_by_wxid(message)
            
            elif message_type == "sync_my_info":
                # Windows端同步我的信息，保存到数据库并转发到App端
                data = message.get("data", {})
                wxid = data.get("wxid", "") if isinstance(data, dict) else ""
                nickname = data.get("nickname", "") if isinstance(data, dict) else ""
                phone = data.get("phone", "") if isinstance(data, dict) else ""
                
                print(f"========== 收到账号信息同步 ==========")
                print(f"wxid: {wxid}")
                print(f"nickname: {nickname}")
                print(f"phone: {phone}")
                print(f"保存到数据库并转发到App端")
                
                # 建立Windows端与手机号的映射关系（用于权限控制）
                if phone and websocket in self.windows_clients:
                    self.windows_client_phone_map[websocket] = phone
                    print(f"已建立Windows端与手机号的映射: {phone}")
                
                await self._save_account_info_to_db(message.get("data", {}), websocket)
                
                # ========== 按手机号精准转发，而不是广播 ==========
                if phone:
                    # 只转发给登录了对应手机号的App端
                    forwarded_count = 0
                    message_json = json.dumps(message, ensure_ascii=False)
                    
                    print(f"开始按手机号精准转发: phone={phone}, App端连接数={len(self.app_clients)}, 手机号映射数={len(self.websocket_phone_map)}")
                    
                    for app_client, client_phone in self.websocket_phone_map.items():
                        if client_phone == phone:
                            try:
                                # 加密消息
                                encrypted_message = self._encrypt_message(websocket, message_json)
                                await app_client.send_text(encrypted_message)
                                forwarded_count += 1
                                print(f"✓ 已转发账号信息到App端（已加密，手机号: {phone}, wxid: {wxid}）")
                            except Exception as e:
                                print(f"✗ 转发消息到App端失败: {e}")
                    
                    if forwarded_count > 0:
                        print(f"========== 转发完成: 已转发账号信息到 {forwarded_count} 个App端（手机号: {phone}） ==========")
                    else:
                        print(f"========== 转发完成: 没有找到登录了手机号 {phone} 的App端 ==========")
                        print(f"当前手机号映射: {list(self.websocket_phone_map.values())}")
                else:
                    # 如果没有手机号，广播给所有App端（兼容旧逻辑）
                    print("警告: sync_my_info消息中没有手机号，广播给所有App端")
                    await self.broadcast_to_app_clients(message)
            
            elif message_type == "command":
                # App端发送命令，转发到Windows端（带权限验证）
                await self._handle_command(websocket, message)
            
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
                    # 加密消息
                    encrypted_message = self._encrypt_message(app_client, message_json)
                    await app_client.send_text(encrypted_message)
                    forwarded_count += 1
                except Exception as e:
                    print(f"转发消息到App端失败: {e}")
        
        if forwarded_count > 0:
            print(f"已转发消息到 {forwarded_count} 个App端（微信账号ID: {we_chat_id}）")
        else:
            print(f"没有找到登录了微信账号 {we_chat_id} 的App端")
    
    async def _handle_command(self, websocket: WebSocket, message: Dict):
        """处理App端发送的命令（带权限验证）"""
        try:
            # 验证App端是否已登录
            if websocket not in self.websocket_phone_map:
                print("警告: App端未登录，拒绝执行命令")
                response = json.dumps({
                    "type": "command_result",
                    "command_id": message.get("command_id", ""),
                    "status": "error",
                    "result": "未登录，无法执行命令"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
            
            app_phone = self.websocket_phone_map[websocket]
            command_type = message.get("command_type", "")
            
            print(f"收到App端命令: command_type={command_type}, phone={app_phone}")
            
            # 对于get_logs命令，只转发给该手机号对应的Windows端
            if command_type == "get_logs":
                # 查找该手机号对应的Windows端
                target_windows_client = None
                for windows_client, windows_phone in self.windows_client_phone_map.items():
                    if windows_phone == app_phone:
                        target_windows_client = windows_client
                        break
                
                if target_windows_client:
                    print(f"找到匹配的Windows端（手机号: {app_phone}），转发get_logs命令")
                    message_json = json.dumps(message, ensure_ascii=False)
                    try:
                        encrypted_message = self._encrypt_message(target_windows_client, message_json)
                        await target_windows_client.send_text(encrypted_message)
                        print(f"get_logs命令已成功转发到Windows端（手机号: {app_phone}）")
                    except Exception as e:
                        print(f"转发get_logs命令到Windows端失败: {e}")
                        response = json.dumps({
                            "type": "command_result",
                            "command_id": message.get("command_id", ""),
                            "status": "error",
                            "result": f"转发命令失败: {str(e)}"
                        }, ensure_ascii=False)
                        await websocket.send_text(self._encrypt_message(websocket, response))
                else:
                    print(f"未找到匹配的Windows端（手机号: {app_phone}），拒绝get_logs命令")
                    print(f"当前Windows端手机号映射: {list(self.windows_client_phone_map.values())}")
                    print(f"当前App端手机号映射: {list(self.websocket_phone_map.values())}")
                    response = json.dumps({
                        "type": "command_result",
                        "command_id": message.get("command_id", ""),
                        "status": "error",
                        "result": "未找到对应的Windows端，无法获取日志"
                    }, ensure_ascii=False)
                    await websocket.send_text(self._encrypt_message(websocket, response))
            else:
                # 对于其他命令，转发给所有Windows端（保持原有逻辑）
                print(f"转发命令到Windows端: {command_type}")
                await self.send_to_windows_client(message)
        except Exception as e:
            print(f"处理命令失败: {e}")
            import traceback
            traceback.print_exc()
            try:
                response = json.dumps({
                    "type": "command_result",
                    "command_id": message.get("command_id", ""),
                    "status": "error",
                    "result": f"处理命令失败: {str(e)}"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
            except:
                pass
    
    async def send_to_windows_client(self, message: Dict):
        """发送消息到Windows端（单播）"""
        if not self.windows_clients:
            print(f"没有Windows端连接（Windows端连接数: {len(self.windows_clients)}, App端连接数: {len(self.app_clients)}, 临时连接数: {len(self.pending_clients)}）")
            return
        
        message_json = json.dumps(message, ensure_ascii=False)
        
        disconnected = set()
        
        for client in self.windows_clients:
            try:
                # 加密消息（每个连接使用自己的会话密钥）
                encrypted_message = self._encrypt_message(client, message_json)
                await client.send_text(encrypted_message)
                print(f"消息已成功发送到Windows端（已加密，命令类型: {message.get('command_type', 'unknown')}）")
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
                # 加密消息（每个连接使用自己的会话密钥）
                encrypted_message = self._encrypt_message(client, message_json)
                await client.send_text(encrypted_message)
                print(f"消息已成功转发到App端（已加密），消息类型: {message.get('type', 'unknown')}")
                break  # 只发送给第一个连接的App客户端
            except Exception as e:
                print(f"发送消息到App端失败: {e}")
                disconnected.add(client)
        
        # 移除断开的连接
        for client in disconnected:
            self.disconnect(client)

    async def _save_account_info_to_db(self, account_data: Dict, websocket: WebSocket = None):
        """保存账号信息到数据库"""
        try:
            if not account_data:
                print("账号信息数据为空，跳过保存")
                return

            wxid = account_data.get("wxid") or account_data.get("wxId") or account_data.get("WxId")
            if not wxid:
                print("账号信息缺少wxid，跳过保存")
                return
            
            # 获取微信账号的手机号
            wechat_phone = account_data.get("phone", "").strip()
            if not wechat_phone:
                print("账号信息缺少手机号，跳过保存")
                return
            
            # 如果提供了websocket，验证手机号匹配
            if websocket and websocket in self.websocket_phone_map:
                login_phone = self.websocket_phone_map[websocket]
                # 验证绑定的微信手机号是否匹配
                is_match, error_msg = await LicenseService.verify_wechat_phone_match(login_phone, wechat_phone)
                if not is_match:
                    print(f"手机号匹配验证失败: {error_msg}")
                    # 不保存账号信息，返回错误（可以通过WebSocket通知客户端）
                    # 这里只打印日志，不阻止保存，因为可能是Windows端同步的数据
                    # 如果是App端，应该在客户端处理这个错误
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
                response = json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "手机号不能为空"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
            
            if not license_key:
                response = json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "授权码不能为空"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
                
            # 验证授权码
            is_valid, error_msg = await LicenseService.verify_license(phone, license_key)
            
            if not is_valid:
                response = json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": error_msg or "授权验证失败"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
            
            # 获取授权信息
            license_info = await LicenseService.get_license_by_phone(phone)
            if not license_info:
                response = json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": "获取授权信息失败"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
            
            # 保存WebSocket与登录手机号的映射关系（用于后续验证手机号匹配）
            self.websocket_phone_map[websocket] = phone
            
            # 登录成功，返回授权信息
            response = json.dumps({
                "type": "login_response",
                "success": True,
                "message": "登录成功",
                "has_manage_permission": license_info.has_manage_permission
            }, ensure_ascii=False)
            await websocket.send_text(self._encrypt_message(websocket, response))
                
            print(f"手机号 {phone} 登录成功")
        except Exception as e:
            print(f"处理登录请求失败: {e}")
            import traceback
            traceback.print_exc()
            try:
                response = json.dumps({
                    "type": "login_response",
                    "success": False,
                    "message": f"登录失败: {str(e)}"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
            except:
                pass
    
    async def _handle_verify_login_code(self, websocket: WebSocket, message: Dict):
        """处理App端或Windows端验证登录码（已废弃，保留用于兼容）"""
        # 此方法已废弃，登录现在直接通过 _handle_login 完成（手机号+授权码）
        response = json.dumps({
            "type": "verify_login_code_response",
            "success": False,
            "message": "请使用手机号+授权码方式登录"
        }, ensure_ascii=False)
        await websocket.send_text(self._encrypt_message(response))
    
    async def _handle_quick_login(self, websocket: WebSocket, message: Dict):
        """处理App端或Windows端快速登录（使用wxid）"""
        try:
            wxid = message.get("wxid", "").strip()
            if not wxid:
                response = json.dumps({
                    "type": "quick_login_response",
                    "success": False,
                    "message": "微信账号ID不能为空"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
                return
            
            # 验证wxid是否存在
            async with AsyncSessionLocal() as session:
                stmt = select(AccountInfo).where(AccountInfo.wxid == wxid)
                result = await session.execute(stmt)
                account_info = result.scalar_one_or_none()
                
                if not account_info:
                    response = json.dumps({
                        "type": "quick_login_response",
                        "success": False,
                        "message": "微信账号不存在"
                    }, ensure_ascii=False)
                    await websocket.send_text(self._encrypt_message(websocket, response))
                    return
                
                # 如果是App端，设置App端的微信账号ID映射
                if websocket in self.app_clients:
                    self.app_client_wxid_map[websocket] = wxid
                
                # ========== 维护websocket_phone_map（用于精准转发） ==========
                # 从账号信息中提取手机号并保存到映射关系
                if account_info.phone:
                    self.websocket_phone_map[websocket] = account_info.phone
                    print(f"快速登录：已保存手机号映射关系: {account_info.phone}")
                
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
            
            response = json.dumps({
                "type": "quick_login_response",
                "success": True,
                "message": "快速登录成功",
                "wxid": wxid,
                "account_info": account_data
            }, ensure_ascii=False)
            await websocket.send_text(self._encrypt_message(websocket, response))
            
            # 判断是App端还是Windows端
            client_type = "App端" if websocket in self.app_clients else "Windows端"
            print(f"{client_type}快速登录成功: wxid={wxid}")
        except Exception as e:
            print(f"快速登录失败: {e}")
            import traceback
            traceback.print_exc()
            try:
                response = json.dumps({
                    "type": "quick_login_response",
                    "success": False,
                    "message": f"快速登录失败: {str(e)}"
                }, ensure_ascii=False)
                await websocket.send_text(self._encrypt_message(websocket, response))
            except:
                pass


# 全局WebSocket管理器实例
websocket_manager = WebSocketManager()

