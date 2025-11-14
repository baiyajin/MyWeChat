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
from app.utils.encryption_service import encryption_service
import json
import base64

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
                result_data = decrypted_body.get("result", "")
                
                # 如果是get_logs命令，需要解密日志内容
                if command.command_type == "get_logs" and command.status == "completed":
                    try:
                        result_json = json.loads(result_data) if isinstance(result_data, str) else result_data
                        encrypted_log_content = result_json.get("encrypted_log_content", "")
                        machine_id = result_json.get("machine_id", "")
                        
                        if encrypted_log_content and machine_id:
                            # 根据机器ID生成密钥
                            encryption_key = encryption_service.get_encryption_key_from_machine_id(machine_id)
                            
                            # 解密日志内容（逐行解密）
                            decrypted_log_lines = []
                            log_lines = encrypted_log_content.strip().split('\n')
                            for line in log_lines:
                                line = line.strip()
                                if not line:
                                    continue
                                try:
                                    # 使用生成的密钥解密每一行
                                    decrypted_line = encryption_service._decrypt_bytes_with_key(
                                        base64.b64decode(line), encryption_key
                                    ).decode('utf-8')
                                    decrypted_log_lines.append(decrypted_line)
                                except Exception as e:
                                    # 如果某行解密失败，跳过或记录错误
                                    print(f"解密日志行失败: {e}")
                                    continue
                            
                            # 更新结果，包含解密后的日志内容
                            result_json["decrypted_log_content"] = "\n".join(decrypted_log_lines)
                            result_data = json.dumps(result_json, ensure_ascii=False)
                    except Exception as e:
                        print(f"处理get_logs命令结果失败: {e}")
                        # 如果解密失败，使用原始结果
                        pass
                
                command.result = str(result_data)
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

