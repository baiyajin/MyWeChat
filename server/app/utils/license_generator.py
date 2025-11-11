"""
授权码生成工具
生成20位随机授权码（字母+数字+特殊符号，随机排列）
"""
import random
import string


def generate_license_key() -> str:
    """
    生成20位随机授权码
    
    规则：
    - 长度：20位
    - 包含：大写字母、小写字母、数字、特殊符号
    - 随机排列
    
    Returns:
        str: 20位授权码
    """
    # 定义字符集
    uppercase_letters = string.ascii_uppercase  # A-Z
    lowercase_letters = string.ascii_lowercase  # a-z
    digits = string.digits  # 0-9
    special_chars = "!@#$%^&*"  # 特殊符号
    
    # 确保至少包含每种类型的字符
    # 分配：大写字母4个，小写字母4个，数字6个，特殊符号6个
    chars = (
        random.choices(uppercase_letters, k=4) +
        random.choices(lowercase_letters, k=4) +
        random.choices(digits, k=6) +
        random.choices(special_chars, k=6)
    )
    
    # 随机打乱顺序
    random.shuffle(chars)
    
    # 组合成20位字符串
    license_key = ''.join(chars)
    
    return license_key


def is_valid_license_key(license_key: str) -> bool:
    """
    验证授权码格式是否有效
    
    Args:
        license_key: 授权码字符串
        
    Returns:
        bool: 是否有效
    """
    if not license_key or len(license_key) != 20:
        return False
    
    # 检查是否包含至少一个大写字母、小写字母、数字和特殊符号
    has_upper = any(c.isupper() for c in license_key)
    has_lower = any(c.islower() for c in license_key)
    has_digit = any(c.isdigit() for c in license_key)
    has_special = any(c in "!@#$%^&*" for c in license_key)
    
    return has_upper and has_lower and has_digit and has_special

