"""
账号信息API接口
"""
from fastapi import APIRouter, HTTPException, Request
from sqlalchemy import select
from typing import List, Optional
import json

from app.models.database import AsyncSessionLocal, AccountInfo
from app.models.schemas import AccountInfoResponse
from app.utils.encryption_service import encryption_service

router = APIRouter()


@router.get("/account")
async def get_account_info(request: Request, wxid: Optional[str] = None, phone: Optional[str] = None):
    """获取账号信息（支持加密响应）"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(AccountInfo)
            if wxid:
                stmt = stmt.where(AccountInfo.wxid == wxid)
            elif phone:
                # 如果指定了手机号，根据手机号查询
                stmt = stmt.where(AccountInfo.phone == phone)
            else:
                # 如果没有指定wxid和phone，返回最新的账号信息
                stmt = stmt.order_by(AccountInfo.updated_at.desc())
            
            result = await session.execute(stmt)
            account_info = result.scalar_one_or_none()
            
            if not account_info:
                return None
            
            account_data = AccountInfoResponse.model_validate(account_info)
            
            # 检查请求头是否要求加密
            encryption_header = request.headers.get("X-Encryption")
            if encryption_header:
                # 客户端要求加密响应
                account_dict = account_data.model_dump()
                account_json = json.dumps(account_dict, ensure_ascii=False)
                encrypted_data = encryption_service.encrypt_string_for_log(account_json)
                return {
                    "encrypted": True,
                    "data": encrypted_data
                }
            else:
                # 返回明文响应（向后兼容）
                return account_data
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")


@router.get("/accounts")
async def get_all_accounts(request: Request, limit: int = 100, offset: int = 0):
    """获取所有账号信息列表（支持加密响应）"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(AccountInfo).order_by(AccountInfo.updated_at.desc()).limit(limit).offset(offset)
            result = await session.execute(stmt)
            accounts = result.scalars().all()
            
            accounts_data = [AccountInfoResponse.model_validate(account) for account in accounts]
            
            # 检查请求头是否要求加密
            encryption_header = request.headers.get("X-Encryption")
            if encryption_header:
                # 客户端要求加密响应
                accounts_list = [account.model_dump() for account in accounts_data]
                accounts_json = json.dumps(accounts_list, ensure_ascii=False)
                encrypted_data = encryption_service.encrypt_string_for_log(accounts_json)
                return {
                    "encrypted": True,
                    "data": encrypted_data
                }
            else:
                # 返回明文响应（向后兼容）
                return accounts_data
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")

