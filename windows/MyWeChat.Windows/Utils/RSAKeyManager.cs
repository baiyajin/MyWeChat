using System;
using System.Security.Cryptography;
using System.Text;

namespace MyWeChat.Windows.Utils
{
    /// <summary>
    /// RSA密钥管理器（客户端）
    /// 用于密钥交换协议
    /// </summary>
    public class RSAKeyManager
    {
        private static RSACryptoServiceProvider? _rsaProvider;
        private static string? _serverPublicKeyPem;
        private static readonly object _lock = new object();

        /// <summary>
        /// 设置服务器公钥（PEM格式）
        /// </summary>
        public static void SetServerPublicKey(string publicKeyPem)
        {
            lock (_lock)
            {
                try
                {
                    _serverPublicKeyPem = publicKeyPem;
                    
                    // 解析PEM格式的公钥
                    _rsaProvider = new RSACryptoServiceProvider(2048);
                    _rsaProvider.ImportFromPem(publicKeyPem);
                    
                    Logger.LogInfo("服务器RSA公钥已设置");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"设置服务器RSA公钥失败: {ex.Message}", ex);
                    _rsaProvider = null;
                    _serverPublicKeyPem = null;
                }
            }
        }

        /// <summary>
        /// 使用服务器公钥加密会话密钥
        /// </summary>
        /// <param name="sessionKey">32字节的会话密钥</param>
        /// <returns>Base64编码的加密密钥</returns>
        public static string EncryptSessionKey(byte[] sessionKey)
        {
            if (sessionKey == null || sessionKey.Length != 32)
            {
                throw new ArgumentException("会话密钥必须是32字节", nameof(sessionKey));
            }

            lock (_lock)
            {
                if (_rsaProvider == null)
                {
                    throw new InvalidOperationException("服务器RSA公钥未设置，无法加密会话密钥");
                }

                try
                {
                    // 使用OAEP填充方式加密
                    byte[] encryptedKey = _rsaProvider.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);
                    return Convert.ToBase64String(encryptedKey);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"加密会话密钥失败: {ex.Message}", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// 检查服务器公钥是否已设置
        /// </summary>
        public static bool IsPublicKeySet()
        {
            lock (_lock)
            {
                return _rsaProvider != null && !string.IsNullOrEmpty(_serverPublicKeyPem);
            }
        }

        /// <summary>
        /// 清除服务器公钥（用于重新连接）
        /// </summary>
        public static void ClearPublicKey()
        {
            lock (_lock)
            {
                _rsaProvider?.Dispose();
                _rsaProvider = null;
                _serverPublicKeyPem = null;
            }
        }
    }
}

