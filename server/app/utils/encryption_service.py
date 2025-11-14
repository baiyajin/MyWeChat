"""
加密服务（AES-256-GCM）
与客户端 C# 版本兼容
支持会话密钥（用于通讯加密）和本地密钥（用于日志加密）分离
"""
import os
import base64
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend
from typing import Optional, Dict


class EncryptionService:
    """加密服务（AES-256-GCM）"""
    
    _instance = None
    _local_key = None  # 本地密钥（用于日志加密）
    _session_keys: Dict[str, bytes] = {}  # 会话密钥字典（WebSocket连接ID -> 会话密钥）
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(EncryptionService, cls).__new__(cls)
        return cls._instance
    
    def __init__(self):
        if self._local_key is None:
            self._local_key = self._get_local_encryption_key()
    
    def _get_local_encryption_key(self) -> bytes:
        """获取本地加密密钥（32字节，256位，用于日志加密）"""
        # 优先从环境变量读取密钥
        env_key = os.getenv("MYWECHAT_ENCRYPTION_KEY")
        if env_key:
            # 如果环境变量是 base64 编码的，先解码
            try:
                key = base64.b64decode(env_key)
                if len(key) == 32:
                    return key
            except:
                pass
        
        # 如果没有环境变量，使用默认密钥（基于固定盐值）
        # 注意：生产环境应该使用环境变量设置密钥
        default_salt = b"MyWeChat_Server_Encryption_Salt_2024"
        default_password = b"MyWeChat_Default_Password_Change_In_Production"
        
        kdf = PBKDF2HMAC(
            algorithm=hashes.SHA256(),
            length=32,
            salt=default_salt,
            iterations=100000,
            backend=default_backend()
        )
        key = kdf.derive(default_password)
        return key
    
    def set_session_key(self, connection_id: str, session_key: bytes):
        """设置会话密钥（用于通讯加密）"""
        if len(session_key) != 32:
            raise ValueError("会话密钥必须是32字节")
        self._session_keys[connection_id] = session_key
        print(f"会话密钥已设置（连接ID: {connection_id[:8]}...）")
    
    def get_session_key(self, connection_id: str) -> Optional[bytes]:
        """获取会话密钥（用于通讯加密）"""
        return self._session_keys.get(connection_id)
    
    def remove_session_key(self, connection_id: str):
        """移除会话密钥"""
        if connection_id in self._session_keys:
            del self._session_keys[connection_id]
            print(f"会话密钥已移除（连接ID: {connection_id[:8]}...）")
    
    def has_session_key(self, connection_id: str) -> bool:
        """检查是否有会话密钥"""
        return connection_id in self._session_keys
    
    def encrypt_string_for_communication(self, connection_id: str, plain_text: str) -> str:
        """加密字符串（用于通讯，使用会话密钥）"""
        if not plain_text:
            return ""
        
        session_key = self.get_session_key(connection_id)
        if session_key is None:
            raise ValueError(f"连接 {connection_id} 的会话密钥未设置")
        
        try:
            plain_bytes = plain_text.encode('utf-8')
            encrypted = self._encrypt_bytes_with_key(plain_bytes, session_key)
            return base64.b64encode(encrypted).decode('utf-8')
        except Exception as e:
            print(f"加密通讯字符串失败: {e}")
            raise
    
    def decrypt_string_for_communication(self, connection_id: str, cipher_text: str) -> str:
        """解密字符串（用于通讯，使用会话密钥）"""
        if not cipher_text:
            return ""
        
        session_key = self.get_session_key(connection_id)
        if session_key is None:
            raise ValueError(f"连接 {connection_id} 的会话密钥未设置")
        
        try:
            cipher_bytes = base64.b64decode(cipher_text)
            decrypted = self._decrypt_bytes_with_key(cipher_bytes, session_key)
            return decrypted.decode('utf-8')
        except Exception as e:
            print(f"解密通讯字符串失败: {e}")
            raise
    
    def encrypt_string_for_log(self, plain_text: str) -> str:
        """加密字符串（用于日志，使用本地密钥）"""
        if not plain_text:
            return ""
        
        try:
            plain_bytes = plain_text.encode('utf-8')
            encrypted = self._encrypt_bytes_with_key(plain_bytes, self._local_key)
            return base64.b64encode(encrypted).decode('utf-8')
        except Exception as e:
            print(f"加密日志字符串失败: {e}")
            raise
    
    def decrypt_string_for_log(self, cipher_text: str) -> str:
        """解密字符串（用于日志，使用本地密钥）"""
        if not cipher_text:
            return ""
        
        try:
            cipher_bytes = base64.b64decode(cipher_text)
            decrypted = self._decrypt_bytes_with_key(cipher_bytes, self._local_key)
            return decrypted.decode('utf-8')
        except Exception as e:
            print(f"解密日志字符串失败: {e}")
            raise
    
    def _encrypt_bytes_with_key(self, plain_bytes: bytes, key: bytes) -> bytes:
        """使用指定密钥加密字节数组
        格式：nonce(12字节) + ciphertext + tag(16字节)
        """
        if not plain_bytes:
            return b""
        
        try:
            # 生成随机 nonce（12字节）
            import secrets
            nonce = secrets.token_bytes(12)
            
            # 加密
            aesgcm = AESGCM(key)
            ciphertext = aesgcm.encrypt(nonce, plain_bytes, None)
            
            # 组合：nonce + ciphertext（ciphertext 已经包含 tag）
            # 注意：AESGCM.encrypt 返回的是 ciphertext + tag
            # 所以格式是：nonce(12) + ciphertext + tag(16)
            result = nonce + ciphertext
            return result
        except Exception as e:
            print(f"加密字节数组失败: {e}")
            raise
    
    def _decrypt_bytes_with_key(self, cipher_bytes: bytes, key: bytes) -> bytes:
        """使用指定密钥解密字节数组
        格式：nonce(12字节) + ciphertext + tag(16字节)
        """
        if not cipher_bytes or len(cipher_bytes) < 28:  # 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
            return b""
        
        try:
            # 提取 nonce、ciphertext 和 tag
            nonce = cipher_bytes[:12]
            ciphertext_with_tag = cipher_bytes[12:]
            
            # 解密
            aesgcm = AESGCM(key)
            plaintext = aesgcm.decrypt(nonce, ciphertext_with_tag, None)
            return plaintext
        except Exception as e:
            print(f"解密字节数组失败: {e}")
            raise
    
    # 为了向后兼容，保留旧的方法（使用本地密钥）
    def encrypt_string(self, plain_text: str) -> str:
        """加密字符串（使用本地密钥，用于日志）"""
        return self.encrypt_string_for_log(plain_text)
    
    def decrypt_string(self, cipher_text: str) -> str:
        """解密字符串（使用本地密钥，用于日志）"""
        return self.decrypt_string_for_log(cipher_text)


# 全局加密服务实例
encryption_service = EncryptionService()

