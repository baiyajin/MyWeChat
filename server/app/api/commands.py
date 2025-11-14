"""
命令API接口
"""
from fastapi import APIRouter, HTTPException, Request
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from typing import List
import uuid
from datetime import datetime

from app.models.database import AsyncSessionLocal, Command
from app.models.schemas import CommandRequest, CommandResponse
from app.websocket.websocket_manager import websocket_manager
from app.utils.http_request_decrypt import decrypt_request_body
import json

router = APIRouter()


@router.post("/commands", response_model=CommandResponse)
async def create_command(request: Request):
    """创建命令（App端发送命令）"""
    try:
        # 解密请求体（如果已加密）
        decrypted_body = await decrypt_request_body(request)
        
        # 解析为Pydantic模型
        try:
            command_request = CommandRequest.model_validate(decrypted_body)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"请求体格式错误: {str(e)}")
        
        async with AsyncSessionLocal() as session:
            try:
                command_id = str(uuid.uuid4())
                
                # 保存到数据库
                command = Command(
                    command_id=command_id,
                    command_type=command_request.command_type,
                    command_data=json.dumps(command_request.command_data),
                    target_we_chat_id=command_request.target_we_chat_id,
                    status="pending"
                )
                session.add(command)
                await session.commit()

                # 通过WebSocket转发到Windows端
                await websocket_manager.send_to_windows_client({
                    "type": "command",
                    "command_id": command_id,
                    "command_type": command_request.command_type,
                    "command_data": command_request.command_data,
                    "target_we_chat_id": command_request.target_we_chat_id
                })

                from datetime import datetime
                return CommandResponse(
                    command_id=command_id,
                    command_type=command_request.command_type,
                    status="pending",
                    result=None,
                    created_at=datetime.utcnow()
                )
            except Exception as e:
                await session.rollback()
                raise HTTPException(status_code=500, detail=f"创建命令失败: {str(e)}")
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"处理命令失败: {str(e)}")


@router.get("/commands/{command_id}", response_model=CommandResponse)
async def get_command(command_id: str):
    """获取命令状态"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(Command).where(Command.command_id == command_id)
            result = await session.execute(stmt)
            command = result.scalar_one_or_none()

            if not command:
                raise HTTPException(status_code=404, detail="命令不存在")

            return CommandResponse(
                command_id=command.command_id,
                command_type=command.command_type,
                status=command.status,
                result=command.result,
                created_at=command.created_at
            )
        except HTTPException:
            raise
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")


@router.post("/commands/{command_id}/result")
async def update_command_result(command_id: str, request: Request):
    """更新命令执行结果（Windows端调用）"""
    try:
        # 解密请求体（如果已加密）
        decrypted_body = await decrypt_request_body(request)
        
        # 解析为字典
        if not isinstance(decrypted_body, dict):
            raise HTTPException(status_code=400, detail="请求体格式错误：必须是JSON对象")
        
        async with AsyncSessionLocal() as session:
            try:
                stmt = select(Command).where(Command.command_id == command_id)
                result_query = await session.execute(stmt)
                command = result_query.scalar_one_or_none()

                if not command:
                    raise HTTPException(status_code=404, detail="命令不存在")

                command.status = decrypted_body.get("status", "completed")
                command.result = str(decrypted_body.get("result", ""))
                await session.commit()

                # 通知App端命令执行结果
                await websocket_manager.send_to_app_client({
                    "type": "command_result",
                    "command_id": command_id,
                    "status": command.status,
                    "result": command.result
                })

                return {"success": True}
            except HTTPException:
                raise
            except Exception as e:
                await session.rollback()
                raise HTTPException(status_code=500, detail=f"更新失败: {str(e)}")
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"处理请求失败: {str(e)}")

