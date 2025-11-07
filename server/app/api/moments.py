"""
朋友圈API接口
"""
from fastapi import APIRouter, HTTPException
from sqlalchemy.ext.asyncio import AsyncSession
from sqlalchemy import select
from typing import List

from app.models.database import AsyncSessionLocal, Moment
from app.models.schemas import MomentsSyncRequest, MomentsResponse

router = APIRouter()


@router.post("/moments/sync", response_model=dict)
async def sync_moments(request: MomentsSyncRequest):
    """同步朋友圈数据"""
    async with AsyncSessionLocal() as session:
        try:
            for moment_data in request.data:
                # 检查是否已存在
                stmt = select(Moment).where(
                    Moment.we_chat_id == moment_data.we_chat_id,
                    Moment.moment_id == moment_data.moment_id
                )
                result = await session.execute(stmt)
                existing = result.scalar_one_or_none()

                if existing:
                    # 更新现有记录
                    existing.friend_id = moment_data.friend_id
                    existing.nick_name = moment_data.nick_name
                    existing.content = moment_data.moments
                    existing.release_time = moment_data.release_time
                    existing.moment_type = moment_data.type
                    existing.json_text = moment_data.json_text
                else:
                    # 创建新记录
                    moment = Moment(
                        we_chat_id=moment_data.we_chat_id,
                        moment_id=moment_data.moment_id,
                        friend_id=moment_data.friend_id,
                        nick_name=moment_data.nick_name,
                        content=moment_data.moments,
                        release_time=moment_data.release_time,
                        moment_type=moment_data.type,
                        json_text=moment_data.json_text
                    )
                    session.add(moment)

            await session.commit()
            return {"success": True, "message": f"已同步 {len(request.data)} 条朋友圈数据"}
        except Exception as e:
            await session.rollback()
            raise HTTPException(status_code=500, detail=f"同步失败: {str(e)}")


@router.get("/moments", response_model=List[MomentsResponse])
async def get_moments(we_chat_id: str = None, limit: int = 50, offset: int = 0):
    """获取朋友圈列表"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(Moment)
            if we_chat_id:
                stmt = stmt.where(Moment.we_chat_id == we_chat_id)
            stmt = stmt.order_by(Moment.created_at.desc()).limit(limit).offset(offset)
            
            result = await session.execute(stmt)
            moments = result.scalars().all()
            
            return [MomentsResponse.model_validate(moment) for moment in moments]
        except Exception as e:
            raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")

