"""
数据库模型和连接管理
"""
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession, async_sessionmaker
from sqlalchemy.orm import declarative_base
from sqlalchemy import Column, Integer, String, Text, DateTime, Boolean
from datetime import datetime

# 数据库配置
DATABASE_URL = "sqlite+aiosqlite:///./my_wechat.db"

# 创建异步引擎
engine = create_async_engine(DATABASE_URL, echo=True)

# 创建异步会话工厂
AsyncSessionLocal = async_sessionmaker(
    engine, class_=AsyncSession, expire_on_commit=False
)

# 声明基类
Base = declarative_base()


class Command(Base):
    """命令队列表"""
    __tablename__ = "commands"

    id = Column(Integer, primary_key=True, index=True)
    command_id = Column(String(100), unique=True, index=True, comment="命令ID")
    command_type = Column(String(100), comment="命令类型")
    command_data = Column(Text, comment="命令数据（JSON格式）")
    target_we_chat_id = Column(String(100), comment="目标微信ID")
    status = Column(String(50), default="pending", comment="状态：pending, processing, completed, failed")
    result = Column(Text, comment="执行结果")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


class AccountInfo(Base):
    """账号信息表"""
    __tablename__ = "account_info"

    id = Column(Integer, primary_key=True, index=True)
    wxid = Column(String(100), unique=True, index=True, comment="微信ID")
    nickname = Column(String(200), comment="昵称")
    avatar = Column(String(500), comment="头像URL")
    account = Column(String(100), comment="账号")
    device_id = Column(String(200), comment="设备ID")
    phone = Column(String(50), comment="手机号")
    wx_user_dir = Column(String(500), comment="微信用户目录")
    unread_msg_count = Column(Integer, default=0, comment="未读消息数")
    is_fake_device_id = Column(Integer, default=0, comment="是否为假设备ID")
    pid = Column(Integer, default=0, comment="进程ID")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


class UserLicense(Base):
    """授权用户表"""
    __tablename__ = "user_license"

    id = Column(Integer, primary_key=True, index=True)
    phone = Column(String(50), unique=True, index=True, comment="登录手机号")
    license_key = Column(String(50), unique=True, index=True, comment="授权码（20位，全局唯一）")
    bound_wechat_phone = Column(String(50), comment="绑定的微信手机号（用于验证，默认等于phone）")
    has_manage_permission = Column(Boolean, default=False, comment="是否有授权码管理权限")
    status = Column(String(20), default="active", comment="状态：active/expired/revoked")
    expire_date = Column(DateTime, comment="过期时间（必填）")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


async def init_db():
    """初始化数据库"""
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


async def close_db():
    """关闭数据库连接"""
    await engine.dispose()
