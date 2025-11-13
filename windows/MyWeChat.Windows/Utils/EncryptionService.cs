using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// 加密服务（AES-256-GCM）
    /// 提供字符串和字节数组的加密/解密功能
    /// </summary>
    public static class EncryptionService
    {
        /// <summary>
        /// 加密字符串（返回 base64 编码的密文）
        /// </summary>
        public static string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return string.Empty;
            }

            try
            {
                byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = EncryptBytes(plainBytes);
                return Convert.ToBase64String(encrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"加密字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 解密字符串（从 base64 编码的密文）
        /// </summary>
        public static string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return string.Empty;
            }

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);
                byte[] decrypted = DecryptBytes(cipherBytes);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch (Exception ex)
            {
                Logger.LogError($"解密字符串失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 加密字节数组
        /// 格式：nonce(12字节) + ciphertext + tag(16字节)
        /// </summary>
        public static byte[] EncryptBytes(byte[] plainBytes)
        {
            if (plainBytes == null || plainBytes.Length == 0)
            {
                return Array.Empty<byte>();
            }

            try
            {
                byte[] key = KeyDerivationService.GetEncryptionKey();
                
                using (AesGcm aesGcm = new AesGcm(key))
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
        /// 解密字节数组
        /// 格式：nonce(12字节) + ciphertext + tag(16字节)
        /// </summary>
        public static byte[] DecryptBytes(byte[] cipherBytes)
        {
            if (cipherBytes == null || cipherBytes.Length < 28) // 至少需要 12(nonce) + 0(ciphertext) + 16(tag)
            {
                return Array.Empty<byte>();
            }

            try
            {
                byte[] key = KeyDerivationService.GetEncryptionKey();

                // 提取 nonce、ciphertext 和 tag
                byte[] nonce = new byte[12];
                Buffer.BlockCopy(cipherBytes, 0, nonce, 0, 12);

                int ciphertextLength = cipherBytes.Length - 28; // 总长度 - nonce(12) - tag(16)
                byte[] ciphertext = new byte[ciphertextLength];
                Buffer.BlockCopy(cipherBytes, 12, ciphertext, 0, ciphertextLength);

                byte[] tag = new byte[16];
                Buffer.BlockCopy(cipherBytes, 12 + ciphertextLength, tag, 0, 16);

                // 解密
                using (AesGcm aesGcm = new AesGcm(key))
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
    }
}

