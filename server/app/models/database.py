"""
数据库模型和连接管理
"""
from sqlalchemy.ext.asyncio import create_async_engine, AsyncSession, async_sessionmaker
from sqlalchemy.orm import declarative_base
from sqlalchemy import Column, Integer, String, Text, DateTime
from datetime import datetime

# 数据库配置
DATABASE_URL = "sqlite+aiosqlite:///./sales_champion.db"

# 创建异步引擎
engine = create_async_engine(DATABASE_URL, echo=True)

# 创建异步会话工厂
AsyncSessionLocal = async_sessionmaker(
    engine, class_=AsyncSession, expire_on_commit=False
)

# 声明基类
Base = declarative_base()


class Contact(Base):
    """联系人表"""
    __tablename__ = "contacts"

    id = Column(Integer, primary_key=True, index=True)
    we_chat_id = Column(String(100), index=True, comment="微信ID")
    friend_id = Column(String(100), index=True, comment="好友ID")
    nick_name = Column(String(200), comment="昵称")
    remark = Column(String(200), comment="备注")
    avatar = Column(String(500), comment="头像URL")
    city = Column(String(100), comment="城市")
    province = Column(String(100), comment="省份")
    country = Column(String(100), comment="国家")
    sex = Column(Integer, comment="性别")
    label_ids = Column(String(500), comment="标签ID列表")
    friend_no = Column(String(100), comment="好友编号")
    is_new_friend = Column(String(10), comment="是否新好友")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


class Moment(Base):
    """朋友圈表"""
    __tablename__ = "moments"

    id = Column(Integer, primary_key=True, index=True)
    we_chat_id = Column(String(100), index=True, comment="微信ID")
    moment_id = Column(String(100), index=True, comment="朋友圈ID")
    friend_id = Column(String(100), index=True, comment="好友ID")
    nick_name = Column(String(200), comment="昵称")
    content = Column(Text, comment="朋友圈内容")
    release_time = Column(String(100), comment="发布时间")
    moment_type = Column(Integer, comment="类型")
    json_text = Column(Text, comment="JSON数据")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


class Tag(Base):
    """标签表"""
    __tablename__ = "tags"

    id = Column(Integer, primary_key=True, index=True)
    we_chat_id = Column(String(100), index=True, comment="微信ID")
    tag_id = Column(String(100), index=True, comment="标签ID")
    tag_name = Column(String(200), comment="标签名称")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")
    updated_at = Column(DateTime, default=datetime.utcnow, onupdate=datetime.utcnow, comment="更新时间")


class ChatMessage(Base):
    """聊天记录表"""
    __tablename__ = "chat_messages"

    id = Column(Integer, primary_key=True, index=True)
    we_chat_id = Column(String(100), index=True, comment="微信ID")
    from_we_chat_id = Column(String(100), index=True, comment="发送者微信ID")
    to_we_chat_id = Column(String(100), index=True, comment="接收者微信ID")
    message_id = Column(String(100), index=True, comment="消息ID")
    content = Column(Text, comment="消息内容")
    message_type = Column(Integer, comment="消息类型")
    send_time = Column(String(100), comment="发送时间")
    is_read = Column(Integer, default=0, comment="是否已读")
    created_at = Column(DateTime, default=datetime.utcnow, comment="创建时间")


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


async def init_db():
    """初始化数据库"""
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)


async def close_db():
    """关闭数据库连接"""
    await engine.dispose()
