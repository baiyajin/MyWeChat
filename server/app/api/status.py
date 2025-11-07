"""
状态API接口
返回服务器和客户端连接状态
"""
from fastapi import APIRouter
from app.websocket.websocket_manager import websocket_manager

router = APIRouter()


@router.get("/status")
async def get_status():
    """获取系统状态"""
    return {
        "server": {
            "status": "running",
            "version": "1.0.0"
        },
        "windows": {
            "status": "connected" if len(websocket_manager.windows_clients) > 0 else "disconnected",
            "connected_count": len(websocket_manager.windows_clients)
        },
        "app": {
            "status": "connected" if len(websocket_manager.app_clients) > 0 else "disconnected",
            "connected_count": len(websocket_manager.app_clients)
        }
    }

