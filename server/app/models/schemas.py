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
    target_we_chat_id: Optional[str] = ""  # 可选字段，某些命令（如get_logs）不需要指定微信ID


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


class UserLicenseResponse(BaseModel):
    """授权用户响应"""
    id: int
    phone: str
    license_key: str
    bound_wechat_phone: Optional[str]
    has_manage_permission: bool
    status: str
    expire_date: datetime
    created_at: datetime
    updated_at: datetime

    class Config:
        from_attributes = True


class UserLicenseCreate(BaseModel):
    """创建授权用户请求"""
    phone: str
    license_key: Optional[str] = None  # 如果不提供，自动生成
    bound_wechat_phone: Optional[str] = None  # 如果不提供，默认等于phone
    has_manage_permission: bool = False
    expire_date: datetime  # 必填，默认一年


class UserLicenseUpdate(BaseModel):
    """更新授权用户请求"""
    license_key: Optional[str] = None
    bound_wechat_phone: Optional[str] = None
    has_manage_permission: Optional[bool] = None
    status: Optional[str] = None
    expire_date: Optional[datetime] = None


class ExtendLicenseRequest(BaseModel):
    """延期请求"""
    days: Optional[int] = None  # 延期天数
    months: Optional[int] = None  # 延期月数
    years: Optional[int] = None  # 延期年数
