"""
数据模型Schema
用于API请求和响应
"""
from pydantic import BaseModel
from typing import List, Dict, Any, Optional
from datetime import datetime


class ContactSyncItem(BaseModel):
    """联系人同步项"""
    we_chat_id: str
    friend_id: str
    nick_name: str
    remark: str = ""
    avatar: str = ""
    city: str = ""
    province: str = ""
    country: str = ""
    sex: int = 0
    label_ids: str = ""
    friend_no: str = ""
    is_new_friend: str = "0"


class ContactSyncRequest(BaseModel):
    """联系人同步请求"""
    data: List[ContactSyncItem]


class ContactResponse(BaseModel):
    """联系人响应"""
    id: int
    we_chat_id: str
    friend_id: str
    nick_name: str
    remark: str
    avatar: str
    city: str
    province: str
    country: str
    sex: int
    label_ids: str
    friend_no: str
    is_new_friend: str

    class Config:
        from_attributes = True


class MomentsSyncItem(BaseModel):
    """朋友圈同步项"""
    we_chat_id: str
    moment_id: str
    friend_id: str
    nick_name: str
    moments: str
    release_time: str
    type: int
    json_text: str = ""


class MomentsSyncRequest(BaseModel):
    """朋友圈同步请求"""
    data: List[MomentsSyncItem]


class MomentsResponse(BaseModel):
    """朋友圈响应"""
    id: int
    we_chat_id: str
    moment_id: str
    friend_id: str
    nick_name: str
    content: str
    release_time: str
    moment_type: int
    json_text: str
    created_at: datetime

    class Config:
        from_attributes = True


class TagSyncItem(BaseModel):
    """标签同步项"""
    we_chat_id: str
    tag_id: str
    tag_name: str


class TagSyncRequest(BaseModel):
    """标签同步请求"""
    data: List[TagSyncItem]


class TagResponse(BaseModel):
    """标签响应"""
    id: int
    we_chat_id: str
    tag_id: str
    tag_name: str

    class Config:
        from_attributes = True


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
