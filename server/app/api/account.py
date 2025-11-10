"""
账号信息API接口
"""
from fastapi import APIRouter, HTTPException
from sqlalchemy import select
from typing import List, Optional

from app.models.database import AsyncSessionLocal, AccountInfo
from app.models.schemas import AccountInfoResponse

router = APIRouter()


@router.get("/account", response_model=Optional[AccountInfoResponse])
async def get_account_info(wxid: Optional[str] = None):
    """获取账号信息"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(AccountInfo)
            if wxid:
                stmt = stmt.where(AccountInfo.wxid == wxid)
            else:
                # 如果没有指定wxid，返回最新的账号信息
                stmt = stmt.order_by(AccountInfo.updated_at.desc())
            
            result = await session.execute(stmt)
            account_info = result.scalar_one_or_none()
            
            if not account_info:
                return None
            
            return AccountInfoResponse.model_validate(account_info)
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")


@router.get("/accounts", response_model=List[AccountInfoResponse])
async def get_all_accounts(limit: int = 100, offset: int = 0):
    """获取所有账号信息列表"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(AccountInfo).order_by(AccountInfo.updated_at.desc()).limit(limit).offset(offset)
            result = await session.execute(stmt)
            accounts = result.scalars().all()
            
            return [AccountInfoResponse.model_validate(account) for account in accounts]
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")

