"""
授权验证服务
提供授权码验证、检查过期、激活授权等功能
"""
from datetime import datetime, timedelta
from sqlalchemy import select
from typing import Optional, Tuple
from app.models.database import AsyncSessionLocal, UserLicense


class LicenseService:
    """授权验证服务"""
    
    @staticmethod
    async def verify_license(phone: str, license_key: str) -> Tuple[bool, Optional[str]]:
        """
        验证授权码
        
        Args:
            phone: 登录手机号
            license_key: 授权码
            
        Returns:
            Tuple[bool, Optional[str]]: (是否有效, 错误信息)
        """
        try:
            async with AsyncSessionLocal() as session:
                stmt = select(UserLicense).where(
                    UserLicense.phone == phone,
                    UserLicense.license_key == license_key
                )
                result = await session.execute(stmt)
                license = result.scalar_one_or_none()
                
                if not license:
                    return False, "手机号或授权码错误"
                
                # 检查状态
                if license.status == "revoked":
                    return False, "授权已被撤销"
                
                if license.status == "expired":
                    return False, "授权已过期"
                
                # 检查是否过期
                if license.expire_date and license.expire_date < datetime.utcnow():
                    # 更新状态为过期
                    license.status = "expired"
                    await session.commit()
                    return False, "授权已过期"
                
                # 验证通过
                return True, None
                
        except Exception as e:
            return False, f"验证授权失败: {str(e)}"
    
    @staticmethod
    async def check_phone_authorized(phone: str) -> Tuple[bool, Optional[str]]:
        """
        检查手机号是否已授权
        
        Args:
            phone: 手机号
            
        Returns:
            Tuple[bool, Optional[str]]: (是否已授权, 错误信息)
        """
        try:
            async with AsyncSessionLocal() as session:
                stmt = select(UserLicense).where(UserLicense.phone == phone)
                result = await session.execute(stmt)
                license = result.scalar_one_or_none()
                
                if not license:
                    return False, "该手机号未授权"
                
                # 检查状态
                if license.status == "revoked":
                    return False, "授权已被撤销"
                
                if license.status == "expired":
                    return False, "授权已过期"
                
                # 检查是否过期
                if license.expire_date and license.expire_date < datetime.utcnow():
                    license.status = "expired"
                    await session.commit()
                    return False, "授权已过期"
                
                return True, None
                
        except Exception as e:
            return False, f"检查授权失败: {str(e)}"
    
    @staticmethod
    async def verify_wechat_phone_match(phone: str, wechat_phone: str) -> Tuple[bool, Optional[str]]:
        """
        验证绑定的微信手机号是否匹配
        
        Args:
            phone: 登录手机号
            wechat_phone: 微信账号的手机号
            
        Returns:
            Tuple[bool, Optional[str]]: (是否匹配, 错误信息)
        """
        try:
            async with AsyncSessionLocal() as session:
                stmt = select(UserLicense).where(UserLicense.phone == phone)
                result = await session.execute(stmt)
                license = result.scalar_one_or_none()
                
                if not license:
                    return False, "未找到授权信息"
                
                # 检查绑定的微信手机号是否匹配
                if license.bound_wechat_phone != wechat_phone:
                    return False, f"绑定的微信手机号({license.bound_wechat_phone})与当前微信账号手机号({wechat_phone})不匹配"
                
                return True, None
                
        except Exception as e:
            return False, f"验证手机号匹配失败: {str(e)}"
    
    @staticmethod
    async def has_manage_permission(phone: str) -> bool:
        """
        检查是否有授权码管理权限
        
        Args:
            phone: 登录手机号
            
        Returns:
            bool: 是否有管理权限
        """
        try:
            async with AsyncSessionLocal() as session:
                stmt = select(UserLicense).where(UserLicense.phone == phone)
                result = await session.execute(stmt)
                license = result.scalar_one_or_none()
                
                if not license:
                    return False
                
                return license.has_manage_permission == True
                
        except Exception as e:
            return False
    
    @staticmethod
    async def get_license_by_phone(phone: str) -> Optional[UserLicense]:
        """
        根据手机号获取授权信息
        
        Args:
            phone: 登录手机号
            
        Returns:
            Optional[UserLicense]: 授权信息
        """
        try:
            async with AsyncSessionLocal() as session:
                stmt = select(UserLicense).where(UserLicense.phone == phone)
                result = await session.execute(stmt)
                return result.scalar_one_or_none()
        except Exception as e:
            return None

