"""
数据模型Schema
用于API请求和响应
"""
from pydantic import BaseModel
from typing import List, Dict, Any, Optional
from datetime import datetime


class CommandRequest(BaseModel):
    """命令请求"""
    command_type: str
    command_data: Dict[str, Any]
    target_we_chat_id: str


class CommandResponse(BaseModel):
    """命令响应"""
    command_id: str
    command_type: str
    status: str
    result: Optional[str] = None
    created_at: datetime

    class Config:
        from_attributes = True


class AccountInfoResponse(BaseModel):
    """账号信息响应"""
    id: int
    wxid: str
    nickname: str
    avatar: str
    account: str
    device_id: str
    phone: str
    wx_user_dir: str
    unread_msg_count: int
    is_fake_device_id: int
    pid: int
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True
