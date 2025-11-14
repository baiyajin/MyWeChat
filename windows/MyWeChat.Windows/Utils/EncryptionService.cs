using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 加密服务（AES-256-GCM）
    /// 提供字符串和字节数组的加密/解密功能
    /// 支持两种密钥模式：
    /// 1. 本地密钥（用于日志加密，基于机器特征）
    /// 2. 会话密钥（用于通讯加密，从服务器交换获得）
    /// </summary>
    public static class EncryptionService
    {
        // 会话密钥（用于通讯加密）
        private static byte[]? _sessionKey;
        private static readonly object _sessionKeyLock = new object();

        /// <summary>
        /// 设置会话密钥（用于通讯加密）
        /// </summary>
        public static void SetSessionKey(byte[] sessionKey)
        {
            if (sessionKey == null || sessionKey.Length != 32)
            {
                throw new ArgumentException("会话密钥必须是32字节", nameof(sessionKey));
            }

            lock (_sessionKeyLock)
            {
                _sessionKey = new byte[32];
                Buffer.BlockCopy(sessionKey, 0, _sessionKey, 0, 32);
                Logger.LogInfo("会话密钥已设置（用于通讯加密）");
            }
        }

        /// <summary>
        /// 清除会话密钥
        /// </summary>
        public static void ClearSessionKey()
        {
            lock (_sessionKeyLock)
            {
                if (_sessionKey != null)
                {
                    Array.Clear(_sessionKey, 0, _sessionKey.Length);
                    _sessionKey = null;
                }
            }
        }

        /// <summary>
        /// 检查会话密钥是否已设置
        /// </summary>
        public static bool HasSessionKey()
        {
            lock (_sessionKeyLock)
            {
                return _sessionKey != null;
            }
        }

        /// <summary>
        /// 加密字符串（用于通讯，使用会话密钥）
        /// </summary>
        public static string EncryptStringForCommunication(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = EncryptBytesWithKey(plainBytes, GetSessionKey());
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"加密通讯字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 解密字符串（用于通讯，使用会话密钥）
        /// </summary>
        public static string DecryptStringForCommunication(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = DecryptBytesWithKey(cipherBytes, GetSessionKey());
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"解密通讯字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 解密字符串（用于HTTP API，直接使用指定的HTTP会话密钥）
        /// </summary>
        public static string DecryptStringForHttp(byte[] httpSessionKey, string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            if (httpSessionKey == null || httpSessionKey.Length != 32)
            {
                throw new ArgumentException("HTTP会话密钥必须是32字节", nameof(httpSessionKey));
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = DecryptBytesWithKey(cipherBytes, httpSessionKey);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"解密HTTP字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 加密字符串（用于日志，使用本地密钥）
        /// </summary>
        public static string EncryptStringForLog(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = EncryptBytesWithKey(plainBytes, KeyDerivationService.GetEncryptionKey());
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"加密日志字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 解密字符串（用于日志，使用本地密钥）
        /// </summary>
        public static string DecryptStringForLog(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = DecryptBytesWithKey(cipherBytes, KeyDerivationService.GetEncryptionKey());
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"解密日志字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 获取会话密钥
        /// </summary>
        private static byte[] GetSessionKey()
        {
            lock (_sessionKeyLock)
            {
                if (_sessionKey == null)
                {
                    throw new InvalidOperationException("会话密钥未设置，无法进行通讯加密");
                }
                return _sessionKey;
            }
        }

        /// <summary>
        /// 使用指定密钥加密字节数组
        /// 格式：nonce(12字节) + ciphertext + tag(16字节)
        /// </summary>
        private static byte[] EncryptBytesWithKey(byte[] plainBytes, byte[] key)
        {
            if (plainBytes == null || plainBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            if (key == null || key.Length != 32)
            {
                throw new ArgumentException("密钥必须是32字节", nameof(key));
            }

            try
            {
                // 使用新的构造函数指定 tag 大小（16字节）
                using (AesGcm aesGcm = new AesGcm(key, 16))
                {
                    // 生成随机 nonce（12字节）
                    byte[] nonce = new byte[12];
                    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                    {
                        rng.GetBytes(nonce);
                    }

                    // 加密
                    byte[] ciphertext = new byte[plainBytes.Length];
                    byte[] tag = new byte[16];
                    aesGcm.Encrypt(nonce, plainBytes, ciphertext, tag);

                    // 组合：nonce + ciphertext + tag
                    byte[] result = new byte[12 + ciphertext.Length + 16];
                    Buffer.BlockCopy(nonce, 0, result, 0, 12);
                    Buffer.BlockCopy(ciphertext, 0, result, 12, ciphertext.Length);
                    Buffer.BlockCopy(tag, 0, result, 12 + ciphertext.Length, 16);

                    return result;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加密字节数组失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 使用指定密钥解密字节数组
        /// 格式：nonce(12字节) + ciphertext + tag(16字节)
        /// </summary>
        private static byte[] DecryptBytesWithKey(byte[] cipherBytes, byte[] key)
        {
            if (cipherBytes == null || cipherBytes.Length < 28) // 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
            {
                return Array.Empty<byte>();
            }

            if (key == null || key.Length != 32)
            {
                throw new ArgumentException("密钥必须是32字节", nameof(key));
            }

            try
            {
                // 提取 nonce、ciphertext 和 tag
                byte[] nonce = new byte[12];
                Buffer.BlockCopy(cipherBytes, 0, nonce, 0, 12);

                int ciphertextLength = cipherBytes.Length - 28; // 总长度 - nonce(12) - tag(16)
                byte[] ciphertext = new byte[ciphertextLength];
                Buffer.BlockCopy(cipherBytes, 12, ciphertext, 0, ciphertextLength);

                byte[] tag = new byte[16];
                Buffer.BlockCopy(cipherBytes, 12 + ciphertextLength, tag, 0, 16);

                // 解密（使用新的构造函数指定 tag 大小）
                using (AesGcm aesGcm = new AesGcm(key, 16))
                {
                    byte[] plaintext = new byte[ciphertextLength];
                    aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
                    return plaintext;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"解密字节数组失败: {ex.Message}", ex);
                throw;
            }
        }

        // 为了向后兼容，保留旧的方法（使用本地密钥）
        /// <summary>
        /// 加密字符串（使用本地密钥，用于日志）
        /// </summary>
        [Obsolete("请使用 EncryptStringForLog 或 EncryptStringForCommunication")]
        public static string EncryptString(string plainText)
        {
            return EncryptStringForLog(plainText);
        }

        /// <summary>
        /// 解密字符串（使用本地密钥，用于日志）
        /// </summary>
        [Obsolete("请使用 DecryptStringForLog 或 DecryptStringForCommunication")]
        public static string DecryptString(string cipherText)
        {
            return DecryptStringForLog(cipherText);
        }

        /// <summary>
        /// 加密字节数组（使用本地密钥，用于日志）
        /// </summary>
        [Obsolete("请使用 EncryptBytesWithKey")]
        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            return EncryptBytesWithKey(plainBytes, KeyDerivationService.GetEncryptionKey());
        }

        /// <summary>
        /// 解密字节数组（使用本地密钥，用于日志）
        /// </summary>
        [Obsolete("请使用 DecryptBytesWithKey")]
        public static byte[] DecryptBytes(byte[] cipherBytes)
        {
            return DecryptBytesWithKey(cipherBytes, KeyDerivationService.GetEncryptionKey());
        }
    }
}

