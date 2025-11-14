"""
HTTP请求体解密工具
用于解密客户端发送的加密请求体
"""
from fastapi import Request, HTTPException
from app.utils.encryption_service import encryption_service
from app.utils.http_session_manager import http_session_manager
import json


async def decrypt_request_body(request: Request) -> dict:
    """
    解密HTTP请求体（如果已加密）
    
    如果请求包含 X-Session-ID 且请求体是加密格式，则解密
    否则返回原始请求体
    
    返回解密后的JSON字典
    """
    try:
        # 读取原始请求体
        body_bytes = await request.body()
        
        if not body_bytes:
            return {}
        
        # 尝试解析为JSON
        try:
            body_json = json.loads(body_bytes.decode('utf-8'))
        except json.JSONDecodeError:
            # 如果不是JSON，返回空字典
            return {}
        
        # 检查是否有会话ID
        session_id = request.headers.get("X-Session-ID")
        if not session_id:
            # 没有会话ID，直接返回原始请求体（向后兼容）
            return body_json if isinstance(body_json, dict) else {}
        
        # 检查是否是加密格式
        if isinstance(body_json, dict) and body_json.get("encrypted") == True and body_json.get("data"):
            # 加密请求体，需要解密
            encrypted_data = body_json["data"]
            
            try:
                # 使用HTTP会话密钥解密
                decrypted_data = encryption_service.decrypt_string_for_http(session_id, encrypted_data)
                
                # 解析解密后的JSON
                decrypted_json = json.loads(decrypted_data)
                return decrypted_json if isinstance(decrypted_json, dict) else {}
            except Exception as e:
                # 解密失败，可能是会话密钥无效或过期
                raise HTTPException(
                    status_code=401,
                    detail=f"请求体解密失败: {str(e)}"
                )
        else:
            # 非加密请求体，直接返回（向后兼容）
            return body_json if isinstance(body_json, dict) else {}
    
    except HTTPException:
        raise
    except Exception as e:
        # 其他错误，返回空字典（向后兼容）
        print(f"解密请求体时发生错误: {e}")
        return {}

