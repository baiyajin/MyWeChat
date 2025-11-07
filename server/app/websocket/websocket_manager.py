"""
WebSocket连接管理器
管理Windows端和App端的WebSocket连接
"""
from fastapi import WebSocket
from typing import List, Dict, Set
import json
import asyncio


class WebSocketManager:
    """WebSocket连接管理器"""
    
    def __init__(self):
        # Windows端连接集合
        self.windows_clients: Set[WebSocket] = set()
        # App端连接集合
        self.app_clients: Set[WebSocket] = set()

    async def connect(self, websocket: WebSocket):
        """接受WebSocket连接"""
        await websocket.accept()
        
        # 根据连接类型区分Windows端和App端
        # 可以通过消息中的client_type字段区分
        # 暂时先添加到Windows端，后续根据消息类型区分
        self.windows_clients.add(websocket)
        print(f"WebSocket连接已建立，当前Windows端连接数: {len(self.windows_clients)}")

    def disconnect(self, websocket: WebSocket):
        """断开WebSocket连接"""
        if websocket in self.windows_clients:
            self.windows_clients.remove(websocket)
            print(f"Windows端连接已断开，当前连接数: {len(self.windows_clients)}")
        
        if websocket in self.app_clients:
            self.app_clients.remove(websocket)
            print(f"App端连接已断开，当前连接数: {len(self.app_clients)}")

    async def handle_message(self, websocket: WebSocket, message: Dict):
        """处理WebSocket消息"""
        try:
            message_type = message.get("type", "")
            
            print(f"收到WebSocket消息，类型: {message_type}, 来源: {websocket}")
            
            if message_type == "sync_contacts":
                # Windows端同步联系人数据，实时转发到App端
                print(f"转发好友列表同步到App端，数据数量: {len(message.get('data', []))}")
                await self.broadcast_to_app_clients(message)
            
            elif message_type == "sync_moments":
                # Windows端同步朋友圈数据，实时转发到App端
                print(f"转发朋友圈同步到App端，数据数量: {len(message.get('data', []))}")
                await self.broadcast_to_app_clients(message)
            
            elif message_type == "sync_tags":
                # Windows端同步标签数据，实时转发到App端
                print(f"转发标签同步到App端，数据数量: {len(message.get('data', []))}")
                await self.broadcast_to_app_clients(message)
            
            elif message_type == "sync_chat_message":
                # Windows端同步聊天消息，实时转发到App端
                print("转发聊天消息同步到App端")
                await self.broadcast_to_app_clients(message)
            
            elif message_type == "sync_my_info":
                # Windows端同步我的信息，实时转发到App端
                print("转发我的信息同步到App端")
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
                if client_type == "windows":
                    if websocket in self.app_clients:
                        self.app_clients.remove(websocket)
                    self.windows_clients.add(websocket)
                    print(f"Windows端连接数: {len(self.windows_clients)}")
                elif client_type == "app":
                    if websocket in self.windows_clients:
                        self.windows_clients.remove(websocket)
                    self.app_clients.add(websocket)
                    print(f"App端连接数: {len(self.app_clients)}")
            
        except Exception as e:
            print(f"处理WebSocket消息失败: {e}")
            import traceback
            traceback.print_exc()


    async def broadcast_to_app_clients(self, message: Dict):
        """广播消息到所有App端"""
        await self.send_to_app_client(message)
    
    async def send_to_windows_client(self, message: Dict):
        """发送消息到Windows端（单播）"""
        if not self.windows_clients:
            print("没有Windows端连接")
            return
        
        message_json = json.dumps(message, ensure_ascii=False)
        disconnected = set()
        
        for client in self.windows_clients:
            try:
                await client.send_text(message_json)
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


# 全局WebSocket管理器实例
websocket_manager = WebSocketManager()

