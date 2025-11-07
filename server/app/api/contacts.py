"""
联系人API接口
"""
from fastapi import APIRouter, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from typing import List
import json

from app.models.database import AsyncSessionLocal, Contact
from app.models.schemas import ContactSyncRequest, ContactResponse

router = APIRouter()


@router.post("/contacts/sync", response_model=dict)
async def sync_contacts(request: ContactSyncRequest):
    """同步联系人数据"""
    async with AsyncSessionLocal() as session:
        try:
            for contact_data in request.data:
                # 检查是否已存在
                stmt = select(Contact).where(
                    Contact.we_chat_id == contact_data.we_chat_id,
                    Contact.friend_id == contact_data.friend_id
                )
                result = await session.execute(stmt)
                existing = result.scalar_one_or_none()

                if existing:
                    # 更新现有记录
                    existing.nick_name = contact_data.nick_name
                    existing.remark = contact_data.remark
                    existing.avatar = contact_data.avatar
                    existing.city = contact_data.city
                    existing.province = contact_data.province
                    existing.country = contact_data.country
                    existing.sex = contact_data.sex
                    existing.label_ids = contact_data.label_ids
                    existing.friend_no = contact_data.friend_no
                    existing.is_new_friend = contact_data.is_new_friend
                else:
                    # 创建新记录
                    contact = Contact(
                        we_chat_id=contact_data.we_chat_id,
                        friend_id=contact_data.friend_id,
                        nick_name=contact_data.nick_name,
                        remark=contact_data.remark,
                        avatar=contact_data.avatar,
                        city=contact_data.city,
                        province=contact_data.province,
                        country=contact_data.country,
                        sex=contact_data.sex,
                        label_ids=contact_data.label_ids,
                        friend_no=contact_data.friend_no,
                        is_new_friend=contact_data.is_new_friend
                    )
                    session.add(contact)

            await session.commit()
            return {"success": True, "message": f"已同步 {len(request.data)} 条联系人数据"}
        except Exception as e:
            await session.rollback()
            raise HTTPException(status_code=500, detail=f"同步失败: {str(e)}")


@router.get("/contacts", response_model=List[ContactResponse])
async def get_contacts(we_chat_id: str = None, limit: int = 100, offset: int = 0):
    """获取联系人列表"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(Contact)
            if we_chat_id:
                stmt = stmt.where(Contact.we_chat_id == we_chat_id)
            stmt = stmt.limit(limit).offset(offset)
            
            result = await session.execute(stmt)
            contacts = result.scalars().all()
            
            return [ContactResponse.model_validate(contact) for contact in contacts]
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")

