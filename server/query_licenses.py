"""查询授权用户表数据"""
import asyncio
from sqlalchemy import select
from app.models.database import AsyncSessionLocal, UserLicense


async def query_licenses():
    """查询所有授权用户"""
    async with AsyncSessionLocal() as session:
        try:
            stmt = select(UserLicense).order_by(UserLicense.id)
            result = await session.execute(stmt)
            licenses = result.scalars().all()
            
            print(f"\n授权用户表中共有 {len(licenses)} 条记录:\n")
            print("-" * 100)
            print(f"{'ID':<5} {'手机号':<15} {'授权码':<20} {'绑定微信手机号':<15} {'管理权限':<10} {'状态':<10} {'过期时间':<20}")
            print("-" * 100)
            
            for license in licenses:
                manage_permission = "是" if license.has_manage_permission else "否"
                expire_date_str = license.expire_date.strftime("%Y-%m-%d %H:%M:%S") if license.expire_date else "无"
                print(f"{license.id:<5} {license.phone:<15} {license.license_key:<20} {license.bound_wechat_phone or '':<15} {manage_permission:<10} {license.status:<10} {expire_date_str:<20}")
            
            print("-" * 100)
            
        except Exception as e:
            print(f"查询失败: {str(e)}")
            raise
        finally:
            await session.close()


if __name__ == "__main__":
    asyncio.run(query_licenses())
