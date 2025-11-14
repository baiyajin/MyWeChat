"""
授权管理API接口
提供授权用户的增删改查功能
"""
from fastapi import APIRouter, HTTPException, Request
from sqlalchemy import select
from typing import List, Optional
from datetime import datetime, timedelta

from app.models.database import AsyncSessionLocal, UserLicense
from app.models.schemas import (
    UserLicenseResponse,
    UserLicenseCreate,
    UserLicenseUpdate,
    ExtendLicenseRequest
)
from app.utils.license_generator import generate_license_key
from app.services.license_service import LicenseService
from app.utils.http_request_decrypt import decrypt_request_body

router = APIRouter()


@router.get("/licenses", response_model=List[UserLicenseResponse])
async def get_all_licenses(
    limit: int = 100,
    offset: int = 0,
    status: Optional[str] = None,
    phone: Optional[str] = None
):
    """
    获取所有授权用户列表
    
    Args:
        limit: 每页数量
        offset: 偏移量
        status: 状态筛选（active/expired/revoked）
        phone: 手机号搜索
    """
    try:
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense)
            
            # 状态筛选
            if status:
                stmt = stmt.where(UserLicense.status == status)
            
            # 手机号搜索
            if phone:
                stmt = stmt.where(UserLicense.phone.contains(phone))
            
            stmt = stmt.order_by(UserLicense.created_at.desc()).limit(limit).offset(offset)
            result = await session.execute(stmt)
            licenses = result.scalars().all()
            
            return [UserLicenseResponse.model_validate(license) for license in licenses]
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")


@router.get("/licenses/{license_id}", response_model=UserLicenseResponse)
async def get_license(license_id: int):
    """获取单个授权用户信息"""
    try:
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense).where(UserLicense.id == license_id)
            result = await session.execute(stmt)
            license = result.scalar_one_or_none()
            
            if not license:
                raise HTTPException(status_code=404, detail="授权用户不存在")
            
            return UserLicenseResponse.model_validate(license)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"查询失败: {str(e)}")


@router.post("/licenses", response_model=UserLicenseResponse)
async def create_license(request: Request):
    """创建授权用户"""
    try:
        # 解密请求体（如果已加密）
        decrypted_body = await decrypt_request_body(request)
        
        # 解析为Pydantic模型
        try:
            license_data = UserLicenseCreate.model_validate(decrypted_body)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"请求体格式错误: {str(e)}")
        
        async with AsyncSessionLocal() as session:
            # 检查手机号是否已存在
            stmt = select(UserLicense).where(UserLicense.phone == license_data.phone)
            result = await session.execute(stmt)
            existing = result.scalar_one_or_none()
            
            if existing:
                raise HTTPException(status_code=400, detail="该手机号已存在")
            
            # 生成授权码（如果未提供）
            license_key = license_data.license_key
            if not license_key:
                license_key = generate_license_key()
                # 确保授权码唯一
                while True:
                    stmt = select(UserLicense).where(UserLicense.license_key == license_key)
                    result = await session.execute(stmt)
                    if result.scalar_one_or_none() is None:
                        break
                    license_key = generate_license_key()
            
            # 检查授权码是否已存在
            stmt = select(UserLicense).where(UserLicense.license_key == license_key)
            result = await session.execute(stmt)
            if result.scalar_one_or_none():
                raise HTTPException(status_code=400, detail="该授权码已存在")
            
            # 设置绑定的微信手机号（默认等于phone）
            bound_wechat_phone = license_data.bound_wechat_phone or license_data.phone
            
            # 创建授权用户
            new_license = UserLicense(
                phone=license_data.phone,
                license_key=license_key,
                bound_wechat_phone=bound_wechat_phone,
                has_manage_permission=license_data.has_manage_permission,
                status="active",
                expire_date=license_data.expire_date
            )
            
            session.add(new_license)
            await session.commit()
            await session.refresh(new_license)
            
            return UserLicenseResponse.model_validate(new_license)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"创建失败: {str(e)}")


@router.put("/licenses/{license_id}", response_model=UserLicenseResponse)
async def update_license(license_id: int, request: Request):
    """更新授权用户信息"""
    try:
        # 解密请求体（如果已加密）
        decrypted_body = await decrypt_request_body(request)
        
        # 解析为Pydantic模型
        try:
            license_data = UserLicenseUpdate.model_validate(decrypted_body)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"请求体格式错误: {str(e)}")
        
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense).where(UserLicense.id == license_id)
            result = await session.execute(stmt)
            license = result.scalar_one_or_none()
            
            if not license:
                raise HTTPException(status_code=404, detail="授权用户不存在")
            
            # 更新字段
            if license_data.license_key is not None:
                # 检查新授权码是否已存在（排除自己）
                stmt = select(UserLicense).where(
                    UserLicense.license_key == license_data.license_key,
                    UserLicense.id != license_id
                )
                result = await session.execute(stmt)
                if result.scalar_one_or_none():
                    raise HTTPException(status_code=400, detail="该授权码已存在")
                license.license_key = license_data.license_key
            
            if license_data.bound_wechat_phone is not None:
                license.bound_wechat_phone = license_data.bound_wechat_phone
            
            if license_data.has_manage_permission is not None:
                license.has_manage_permission = license_data.has_manage_permission
            
            if license_data.status is not None:
                license.status = license_data.status
            
            if license_data.expire_date is not None:
                license.expire_date = license_data.expire_date
            
            license.updated_at = datetime.utcnow()
            
            await session.commit()
            await session.refresh(license)
            
            return UserLicenseResponse.model_validate(license)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"更新失败: {str(e)}")


@router.delete("/licenses/{license_id}")
async def delete_license(license_id: int):
    """删除授权用户（软删除）"""
    try:
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense).where(UserLicense.id == license_id)
            result = await session.execute(stmt)
            license = result.scalar_one_or_none()
            
            if not license:
                raise HTTPException(status_code=404, detail="授权用户不存在")
            
            # 软删除：标记为revoked
            license.status = "revoked"
            license.updated_at = datetime.utcnow()
            
            await session.commit()
            
            return {"message": "删除成功"}
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"删除失败: {str(e)}")


@router.post("/licenses/{license_id}/extend")
async def extend_license(license_id: int, request: Request):
    """延期授权"""
    try:
        # 解密请求体（如果已加密）
        decrypted_body = await decrypt_request_body(request)
        
        # 解析为Pydantic模型
        try:
            extend_data = ExtendLicenseRequest.model_validate(decrypted_body)
        except Exception as e:
            raise HTTPException(status_code=400, detail=f"请求体格式错误: {str(e)}")
        
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense).where(UserLicense.id == license_id)
            result = await session.execute(stmt)
            license = result.scalar_one_or_none()
            
            if not license:
                raise HTTPException(status_code=404, detail="授权用户不存在")
            
            # 计算新的过期时间
            if not license.expire_date:
                # 如果没有过期时间，从当前时间开始计算
                new_expire_date = datetime.utcnow()
            else:
                new_expire_date = license.expire_date
            
            # 延期
            if extend_data.days:
                new_expire_date += timedelta(days=extend_data.days)
            if extend_data.months:
                new_expire_date += timedelta(days=extend_data.months * 30)
            if extend_data.years:
                new_expire_date += timedelta(days=extend_data.years * 365)
            
            license.expire_date = new_expire_date
            # 如果状态是过期，更新为active
            if license.status == "expired":
                license.status = "active"
            license.updated_at = datetime.utcnow()
            
            await session.commit()
            await session.refresh(license)
            
            return UserLicenseResponse.model_validate(license)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"延期失败: {str(e)}")


@router.post("/licenses/{license_id}/generate-key")
async def generate_new_key(license_id: int):
    """为授权用户生成新的授权码"""
    try:
        async with AsyncSessionLocal() as session:
            stmt = select(UserLicense).where(UserLicense.id == license_id)
            result = await session.execute(stmt)
            license = result.scalar_one_or_none()
            
            if not license:
                raise HTTPException(status_code=404, detail="授权用户不存在")
            
            # 生成新的授权码
            new_key = generate_license_key()
            # 确保授权码唯一
            while True:
                stmt = select(UserLicense).where(UserLicense.license_key == new_key)
                result = await session.execute(stmt)
                if result.scalar_one_or_none() is None:
                    break
                new_key = generate_license_key()
            
            license.license_key = new_key
            license.updated_at = datetime.utcnow()
            
            await session.commit()
            await session.refresh(license)
            
            return UserLicenseResponse.model_validate(license)
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"生成授权码失败: {str(e)}")

