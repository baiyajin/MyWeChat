"""
HTTP密钥交换API接口
用于HTTP API的密钥交换协议
"""
from fastapi import APIRouter, HTTPException, Request
from pydantic import BaseModel
import json
import base64

from app.utils.rsa_key_manager import rsa_key_manager
from app.utils.encryption_service import encryption_service
from app.utils.http_session_manager import http_session_manager

router = APIRouter()


class SessionKeyRequest(BaseModel):
    """会话密钥交换请求"""
    encrypted_key: str  # Base64编码的RSA加密会话密钥


@router.get("/key-exchange/public-key")
async def get_public_key():
    """获取RSA公钥（用于密钥交换）"""
    try:
        public_key_pem = rsa_key_manager.get_public_key_pem()
        return {
            "type": "rsa_public_key",
            "public_key": public_key_pem
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"获取公钥失败: {str(e)}")


@router.post("/key-exchange/session-key")
async def exchange_session_key(request: SessionKeyRequest):
    """交换会话密钥"""
    try:
        # 使用RSA私钥解密会话密钥
        session_key = rsa_key_manager.decrypt_session_key(request.encrypted_key)
        
        # 创建HTTP会话
        session_id = http_session_manager.create_session(session_key)
        
        return {
            "type": "key_exchange_success",
            "session_id": session_id
        }
    except Exception as e:
        raise HTTPException(status_code=400, detail=f"密钥交换失败: {str(e)}")

