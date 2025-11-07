"""
标签API接口
"""
from fastapi import APIRouter, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from typing import List

from app.models.database import AsyncSessionLocal, Tag
from app.models.schemas import TagSyncRequest, TagResponse

router = APIRouter()


@router.post("/tags/sync", response_model=dict)
async def sync_tags(request: TagSyncRequest):
    """同步标签数据"""
    async with AsyncSessionLocal() as session:
        try:
            for tag_data in request.data:
                # 检查是否已存在
                stmt = select(Tag).where(
                    Tag.we_chat_id == tag_data.we_chat_id,
                    Tag.tag_id == tag_data.tag_id
                )
                result = await session.execute(stmt)
                existing = result.scalar_one_or_none()

                if existing:
                    # 更新现有记录
                    existing.tag_name = tag_data.tag_name
                else:
                    # 创建新记录
                    tag = Tag(
                        we_chat_id=tag_data.we_chat_id,
                        tag_id=tag_data.tag_id,
                        tag_name=tag_data.tag_name
                    )
                    session.add(tag)

            await session.commit()
            return {"success": True, "message": f"已同步 {len(request.data)} 条标签数据"}
        except Exception as e:
            await session.rollback()
            raise HTTPException(status_code=500, detail=f"同步失败: {str(e)}")


@router.get("/tags", response_model=List[TagResponse])
async def get_tags(we_chat_id: str = None):
    """获取标签列表"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(Tag)
            if we_chat_id:
                stmt = stmt.where(Tag.we_chat_id == we_chat_id)
            
            result = await session.execute(stmt)
            tags = result.scalars().all()
            
            return [TagResponse.model_validate(tag) for tag in tags]
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")

