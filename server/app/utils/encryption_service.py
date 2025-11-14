"""
加密服务（AES-256-GCM）
与客户端 C# 版本兼容
"""
import os
import base64
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
from cryptography.hazmat.backends import default_backend


class EncryptionService:
    """加密服务（AES-256-GCM）"""
    
    _instance = None
    _key = None
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(EncryptionService, cls).__new__(cls)
        return cls._instance
    
    def __init__(self):
        if self._key is None:
            self._key = self._get_encryption_key()
    
    def _get_encryption_key(self) -> bytes:
        """获取加密密钥（32字节，256位）"""
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
    
    def encrypt_string(self, plain_text: str) -> str:
        """加密字符串（返回 base64 编码的密文）"""
        if not plain_text:
            return ""
        
        try:
            plain_bytes = plain_text.encode('utf-8')
            encrypted = self.encrypt_bytes(plain_bytes)
            return base64.b64encode(encrypted).decode('utf-8')
        except Exception as e:
            print(f"加密字符串失败: {e}")
            raise
    
    def decrypt_string(self, cipher_text: str) -> str:
        """解密字符串（从 base64 编码的密文）"""
        if not cipher_text:
            return ""
        
        try:
            cipher_bytes = base64.b64decode(cipher_text)
            decrypted = self.decrypt_bytes(cipher_bytes)
            return decrypted.decode('utf-8')
        except Exception as e:
            print(f"解密字符串失败: {e}")
            raise
    
    def encrypt_bytes(self, plain_bytes: bytes) -> bytes:
        """加密字节数组
        格式：nonce(12字节) + ciphertext + tag(16字节)
        """
        if not plain_bytes:
            return b""
        
        try:
            # 生成随机 nonce（12字节）
            import secrets
            nonce = secrets.token_bytes(12)
            
            # 加密
            aesgcm = AESGCM(self._key)
            ciphertext = aesgcm.encrypt(nonce, plain_bytes, None)
            
            # 组合：nonce + ciphertext（ciphertext 已经包含 tag）
            # 注意：AESGCM.encrypt 返回的是 ciphertext + tag
            # 所以格式是：nonce(12) + ciphertext + tag(16)
            result = nonce + ciphertext
            return result
        except Exception as e:
            print(f"加密字节数组失败: {e}")
            raise
    
    def decrypt_bytes(self, cipher_bytes: bytes) -> bytes:
        """解密字节数组
        格式：nonce(12字节) + ciphertext + tag(16字节)
        """
        if not cipher_bytes or len(cipher_bytes) < 28:  # 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
            return b""
        
        try:
            # 提取 nonce、ciphertext 和 tag
            nonce = cipher_bytes[:12]
            ciphertext_with_tag = cipher_bytes[12:]
            
            # 解密
            aesgcm = AESGCM(self._key)
            plaintext = aesgcm.decrypt(nonce, ciphertext_with_tag, None)
            return plaintext
        except Exception as e:
            print(f"解密字节数组失败: {e}")
            raise


# 全局加密服务实例
encryption_service = EncryptionService()

