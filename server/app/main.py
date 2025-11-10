"""
FastAPI主应用
提供RESTful API和WebSocket服务
"""
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles
import uvicorn
import json
import asyncio
from typing import List, Dict
import os

from app.models import database
from app.api import contacts, moments, tags, commands, status, account
from app.websocket.websocket_manager import websocket_manager

app = FastAPI(title="MyWeChat后端服务", version="1.0.0")

# 配置静态文件服务
static_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), "static")
if os.path.exists(static_dir):
    app.mount("/static", StaticFiles(directory=static_dir), name="static")

# 配置CORS
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 注册路由
app.include_router(contacts.router, prefix="/api", tags=["联系人"])
app.include_router(moments.router, prefix="/api", tags=["朋友圈"])
app.include_router(tags.router, prefix="/api", tags=["标签"])
app.include_router(commands.router, prefix="/api", tags=["命令"])
app.include_router(status.router, prefix="/api", tags=["状态"])
app.include_router(account.router, prefix="/api", tags=["账号信息"])


@app.on_event("startup")
async def startup_event():
    """应用启动事件"""
    # 初始化数据库
    await database.init_db()
    print("数据库初始化完成")


@app.on_event("shutdown")
async def shutdown_event():
    """应用关闭事件"""
    await database.close_db()
    print("数据库连接已关闭")


@app.get("/")
async def root():
    """根路径"""
    return {"message": "MyWeChat后端服务", "version": "1.0.0"}


@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket):
    """WebSocket端点"""
    await websocket_manager.connect(websocket)
    try:
        while True:
            data = await websocket.receive_text()
            message = json.loads(data)
            
            # 处理消息
            await websocket_manager.handle_message(websocket, message)
    except WebSocketDisconnect:
        websocket_manager.disconnect(websocket)
    except Exception as e:
        print(f"WebSocket错误: {e}")
        websocket_manager.disconnect(websocket)


if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)

