"""
HTTP会话密钥管理器
管理HTTP请求的会话密钥（用于无状态HTTP API的密钥交换）
"""
import secrets
import time
from typing import Optional, Dict
from datetime import datetime, timedelta


class HTTPSessionManager:
    """HTTP会话密钥管理器"""
    
    _instance = None
    _sessions: Dict[str, dict] = {}  # session_id -> {session_key, created_at, last_used}
    _session_timeout = 3600  # 会话超时时间（秒，1小时）
    _cleanup_interval = 300  # 清理间隔（秒，5分钟）
    _last_cleanup = time.time()
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(HTTPSessionManager, cls).__new__(cls)
        return cls._instance
    
    def create_session(self, session_key: bytes) -> str:
        """创建新会话，返回session_id"""
        if len(session_key) != 32:
            raise ValueError("会话密钥必须是32字节")
        
        # 生成唯一的session_id
        session_id = secrets.token_urlsafe(32)
        
        # 存储会话信息
        self._sessions[session_id] = {
            "session_key": session_key,
            "created_at": time.time(),
            "last_used": time.time()
        }
        
        # 定期清理过期会话
        self._cleanup_expired_sessions()
        
        print(f"HTTP会话已创建（session_id: {session_id[:16]}...）")
        return session_id
    
    def get_session_key(self, session_id: str) -> Optional[bytes]:
        """获取会话密钥"""
        if not session_id:
            return None
        
        session = self._sessions.get(session_id)
        if not session:
            return None
        
        # 检查是否过期
        if time.time() - session["last_used"] > self._session_timeout:
            # 会话已过期，删除
            del self._sessions[session_id]
            print(f"HTTP会话已过期（session_id: {session_id[:16]}...）")
            return None
        
        # 更新最后使用时间
        session["last_used"] = time.time()
        return session["session_key"]
    
    def remove_session(self, session_id: str):
        """移除会话"""
        if session_id in self._sessions:
            del self._sessions[session_id]
            print(f"HTTP会话已移除（session_id: {session_id[:16]}...）")
    
    def _cleanup_expired_sessions(self):
        """清理过期会话"""
        current_time = time.time()
        
        # 每5分钟清理一次
        if current_time - self._last_cleanup < self._cleanup_interval:
            return
        
        self._last_cleanup = current_time
        
        expired_sessions = []
        for session_id, session in self._sessions.items():
            if current_time - session["last_used"] > self._session_timeout:
                expired_sessions.append(session_id)
        
        for session_id in expired_sessions:
            del self._sessions[session_id]
        
        if expired_sessions:
            print(f"已清理 {len(expired_sessions)} 个过期的HTTP会话")


# 全局HTTP会话管理器实例
http_session_manager = HTTPSessionManager()

