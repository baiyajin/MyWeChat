"""
RSA密钥管理服务（服务器端）
用于密钥交换协议
"""
import os
import base64
from cryptography.hazmat.primitives.asymmetric import rsa, padding
from cryptography.hazmat.primitives import serialization, hashes
from cryptography.hazmat.backends import default_backend
from typing import Optional, Tuple


class RSAKeyManager:
    """RSA密钥管理器（服务器端）"""
    
    _instance = None
    _private_key = None
    _public_key = None
    _public_key_pem = None
    
    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(RSAKeyManager, cls).__new__(cls)
        return cls._instance
    
    def __init__(self):
        if self._private_key is None:
            self._load_or_generate_keys()
    
    def _load_or_generate_keys(self):
        """加载或生成RSA密钥对"""
        # 尝试从文件加载密钥
        key_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(__file__))), "keys")
        os.makedirs(key_dir, exist_ok=True)
        
        private_key_path = os.path.join(key_dir, "rsa_private_key.pem")
        public_key_path = os.path.join(key_dir, "rsa_public_key.pem")
        
        try:
            # 尝试加载现有密钥
            if os.path.exists(private_key_path) and os.path.exists(public_key_path):
                with open(private_key_path, "rb") as f:
                    self._private_key = serialization.load_pem_private_key(
                        f.read(),
                        password=None,
                        backend=default_backend()
                    )
                
                with open(public_key_path, "rb") as f:
                    self._public_key = serialization.load_pem_public_key(
                        f.read(),
                        backend=default_backend()
                    )
                
                # 生成PEM格式的公钥字符串
                self._public_key_pem = self._public_key.public_bytes(
                    encoding=serialization.Encoding.PEM,
                    format=serialization.PublicFormat.SubjectPublicKeyInfo
                ).decode('utf-8')
                
                print("RSA密钥对已从文件加载")
                return
        except Exception as e:
            print(f"加载RSA密钥失败: {e}，将生成新密钥")
        
        # 生成新密钥对
        self._private_key = rsa.generate_private_key(
            public_exponent=65537,
            key_size=2048,
            backend=default_backend()
        )
        
        self._public_key = self._private_key.public_key()
        
        # 生成PEM格式的公钥字符串
        self._public_key_pem = self._public_key.public_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PublicFormat.SubjectPublicKeyInfo
        ).decode('utf-8')
        
        # 保存密钥到文件
        try:
            # 保存私钥
            private_key_pem = self._private_key.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.PKCS8,
                encryption_algorithm=serialization.NoEncryption()
            )
            with open(private_key_path, "wb") as f:
                f.write(private_key_pem)
            
            # 保存公钥
            public_key_pem = self._public_key.public_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PublicFormat.SubjectPublicKeyInfo
            )
            with open(public_key_path, "wb") as f:
                f.write(public_key_pem)
            
            print("RSA密钥对已生成并保存到文件")
        except Exception as e:
            print(f"保存RSA密钥失败: {e}")
    
    def get_public_key_pem(self) -> str:
        """获取PEM格式的公钥字符串"""
        return self._public_key_pem
    
    def decrypt_session_key(self, encrypted_key_b64: str) -> bytes:
        """使用RSA私钥解密会话密钥"""
        try:
            encrypted_key = base64.b64decode(encrypted_key_b64)
            
            # 使用OAEP填充方式解密
            session_key = self._private_key.decrypt(
                encrypted_key,
                padding.OAEP(
                    mgf=padding.MGF1(algorithm=hashes.SHA256()),
                    algorithm=hashes.SHA256(),
                    label=None
                )
            )
            
            if len(session_key) != 32:
                raise ValueError(f"会话密钥长度不正确: {len(session_key)}，期望32字节")
            
            return session_key
        except Exception as e:
            print(f"解密会话密钥失败: {e}")
            raise


# 全局RSA密钥管理器实例
rsa_key_manager = RSAKeyManager()

