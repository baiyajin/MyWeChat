using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Utils;
using Newtonsoft.Json;
using System.Net.Http.Headers;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// API服务（用于HTTP请求）
    /// 支持HTTP密钥交换协议
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverUrl;
        private string? _httpSessionId; // HTTP会话ID
        private byte[]? _httpSessionKey; // HTTP会话密钥
        private readonly object _sessionLock = new object();

        public ApiService(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        /// <summary>
        /// 确保HTTP会话密钥已交换（如果未交换则进行密钥交换）
        /// </summary>
        private async Task<bool> EnsureHttpSessionKeyAsync()
        {
            lock (_sessionLock)
            {
                // 如果已有会话密钥，直接返回
                if (!string.IsNullOrEmpty(_httpSessionId) && _httpSessionKey != null)
                {
                    return true;
                }
            }

            try
            {
                // 步骤1：获取RSA公钥
                string publicKeyUrl = $"{_serverUrl}/api/key-exchange/public-key";
                HttpResponseMessage publicKeyResponse = await _httpClient.GetAsync(publicKeyUrl);
                
                if (!publicKeyResponse.IsSuccessStatusCode)
                {
                    Logger.LogError($"获取RSA公钥失败: {publicKeyResponse.StatusCode}");
                    return false;
                }

                string publicKeyJson = await publicKeyResponse.Content.ReadAsStringAsync();
                var publicKeyObj = JsonConvert.DeserializeObject<dynamic>(publicKeyJson);
                string? publicKeyPem = publicKeyObj?.public_key?.ToString();

                if (string.IsNullOrEmpty(publicKeyPem))
                {
                    Logger.LogError("RSA公钥为空");
                    return false;
                }

                // 设置服务器公钥
                RSAKeyManager.SetServerPublicKey(publicKeyPem);
                Logger.LogInfo("已获取服务器RSA公钥");

                // 步骤2：生成随机会话密钥
                byte[] sessionKey = new byte[32];
                using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(sessionKey);
                }

                // 步骤3：使用RSA公钥加密会话密钥
                string encryptedSessionKey = RSAKeyManager.EncryptSessionKey(sessionKey);

                // 步骤4：发送加密的会话密钥给服务器
                string sessionKeyUrl = $"{_serverUrl}/api/key-exchange/session-key";
                var sessionKeyRequest = new
                {
                    encrypted_key = encryptedSessionKey
                };
                string sessionKeyJson = JsonConvert.SerializeObject(sessionKeyRequest);
                HttpContent content = new StringContent(sessionKeyJson, Encoding.UTF8, "application/json");
                HttpResponseMessage sessionKeyResponse = await _httpClient.PostAsync(sessionKeyUrl, content);

                if (!sessionKeyResponse.IsSuccessStatusCode)
                {
                    Logger.LogError($"交换会话密钥失败: {sessionKeyResponse.StatusCode}");
                    return false;
                }

                string sessionKeyResponseJson = await sessionKeyResponse.Content.ReadAsStringAsync();
                var sessionKeyResponseObj = JsonConvert.DeserializeObject<dynamic>(sessionKeyResponseJson);
                string? sessionId = sessionKeyResponseObj?.session_id?.ToString();

                if (string.IsNullOrEmpty(sessionId))
                {
                    Logger.LogError("服务器返回的会话ID为空");
                    return false;
                }

                // 保存会话ID和会话密钥
                lock (_sessionLock)
                {
                    _httpSessionId = sessionId;
                    _httpSessionKey = new byte[32];
                    Buffer.BlockCopy(sessionKey, 0, _httpSessionKey, 0, 32);
                }

                Logger.LogInfo("HTTP密钥交换成功");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"HTTP密钥交换失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 根据手机号获取账号信息
        /// </summary>
        public async Task<AccountInfo?> GetAccountInfoByPhoneAsync(string phone)
        {
            try
            {
                // 确保HTTP会话密钥已交换
                if (!await EnsureHttpSessionKeyAsync())
                {
                    Logger.LogWarning("HTTP密钥交换失败，无法获取账号信息");
                    return null;
                }

                string url = $"{_serverUrl}/api/account?phone={Uri.EscapeDataString(phone)}";
                
                // 创建请求，携带会话ID
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                lock (_sessionLock)
                {
                    if (!string.IsNullOrEmpty(_httpSessionId))
                    {
                        request.Headers.Add("X-Session-ID", _httpSessionId);
                    }
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                
                // 如果会话过期（401），重新进行密钥交换
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Logger.LogWarning("HTTP会话已过期，重新进行密钥交换");
                    lock (_sessionLock)
                    {
                        _httpSessionId = null;
                        _httpSessionKey = null;
                    }
                    
                    // 重新交换密钥并重试
                    if (await EnsureHttpSessionKeyAsync())
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, url);
                        lock (_sessionLock)
                        {
                            if (!string.IsNullOrEmpty(_httpSessionId))
                            {
                                request.Headers.Add("X-Session-ID", _httpSessionId);
                            }
                        }
                        response = await _httpClient.SendAsync(request);
                    }
                }
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    // 尝试解密响应（如果服务器返回加密数据）
                    try
                    {
                        var responseObj = JsonConvert.DeserializeObject<dynamic>(json);
                        if (responseObj != null)
                        {
                            bool? isEncrypted = responseObj.encrypted as bool?;
                            if (isEncrypted == true && responseObj.data != null)
                            {
                                // 加密响应，需要解密（使用HTTP会话密钥）
                                string? encryptedData = responseObj.data?.ToString();
                                if (!string.IsNullOrEmpty(encryptedData))
                                {
                                    lock (_sessionLock)
                                    {
                                        if (_httpSessionKey != null)
                                        {
                                            // 使用HTTP会话密钥解密（直接使用，不修改全局会话密钥）
                                            json = EncryptionService.DecryptStringForHttp(_httpSessionKey, encryptedData);
                                            Logger.LogInfo($"解密响应成功，解密后JSON长度: {json?.Length ?? 0}");
                                        }
                                        else
                                        {
                                            Logger.LogWarning("HTTP会话密钥未设置，无法解密响应");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception decryptEx)
                    {
                        Logger.LogWarning($"解密响应失败: {decryptEx.Message}");
                        Logger.LogError($"解密响应异常详情: {decryptEx}");
                        // 解密失败，尝试使用原始JSON
                    }
                    
                    if (string.IsNullOrEmpty(json))
                    {
                        Logger.LogWarning("响应JSON为空，无法解析账号信息");
                        return null;
                    }
                    
                    var accountData = JsonConvert.DeserializeObject<dynamic>(json);
                    
                    if (accountData != null)
                    {
                        string avatar = accountData.avatar?.ToString() ?? "";
                        Logger.LogInfo($"解析账号信息: wxid={accountData.wxid?.ToString() ?? ""}, nickname={accountData.nickname?.ToString() ?? ""}, avatar={(string.IsNullOrEmpty(avatar) ? "空" : "有值")}");
                        return new AccountInfo
                        {
                            WeChatId = accountData.wxid?.ToString() ?? "",
                            NickName = accountData.nickname?.ToString() ?? "",
                            Avatar = avatar,
                            BoundAccount = accountData.account?.ToString() ?? "",
                            Phone = accountData.phone?.ToString() ?? "",
                            DeviceId = accountData.device_id?.ToString() ?? "",
                            WxUserDir = accountData.wx_user_dir?.ToString() ?? "",
                            UnreadMsgCount = accountData.unread_msg_count ?? 0,
                            IsFakeDeviceId = accountData.is_fake_device_id ?? 0,
                            Pid = accountData.pid ?? 0
                        };
                    }
                }
                else
                {
                    Logger.LogWarning($"根据手机号获取账号信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"根据手机号获取账号信息异常: {ex.Message}", ex);
            }
            
            return null;
        }

        /// <summary>
        /// 根据wxid获取账号信息
        /// </summary>
        public async Task<AccountInfo?> GetAccountInfoAsync(string wxid)
        {
            try
            {
                // 确保HTTP会话密钥已交换
                if (!await EnsureHttpSessionKeyAsync())
                {
                    Logger.LogWarning("HTTP密钥交换失败，无法获取账号信息");
                    return null;
                }

                string url = $"{_serverUrl}/api/account?wxid={Uri.EscapeDataString(wxid)}";
                
                // 创建请求，携带会话ID
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                lock (_sessionLock)
                {
                    if (!string.IsNullOrEmpty(_httpSessionId))
                    {
                        request.Headers.Add("X-Session-ID", _httpSessionId);
                    }
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                
                // 如果会话过期（401），重新进行密钥交换
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Logger.LogWarning("HTTP会话已过期，重新进行密钥交换");
                    lock (_sessionLock)
                    {
                        _httpSessionId = null;
                        _httpSessionKey = null;
                    }
                    
                    // 重新交换密钥并重试
                    if (await EnsureHttpSessionKeyAsync())
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, url);
                        lock (_sessionLock)
                        {
                            if (!string.IsNullOrEmpty(_httpSessionId))
                            {
                                request.Headers.Add("X-Session-ID", _httpSessionId);
                            }
                        }
                        response = await _httpClient.SendAsync(request);
                    }
                }
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    // 尝试解密响应（如果服务器返回加密数据）
                    try
                    {
                        var responseObj = JsonConvert.DeserializeObject<dynamic>(json);
                        if (responseObj != null)
                        {
                            bool? isEncrypted = responseObj.encrypted as bool?;
                            if (isEncrypted == true && responseObj.data != null)
                            {
                                // 加密响应，需要解密（使用HTTP会话密钥）
                                string? encryptedData = responseObj.data?.ToString();
                                if (!string.IsNullOrEmpty(encryptedData))
                                {
                                    lock (_sessionLock)
                                    {
                                        if (_httpSessionKey != null)
                                        {
                                            // 使用HTTP会话密钥解密（直接使用，不修改全局会话密钥）
                                            json = EncryptionService.DecryptStringForHttp(_httpSessionKey, encryptedData);
                                        }
                                        else
                                        {
                                            Logger.LogWarning("HTTP会话密钥未设置，无法解密响应");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception decryptEx)
                    {
                        Logger.LogWarning($"解密响应失败: {decryptEx.Message}");
                        // 解密失败，尝试使用原始JSON
                    }
                    
                    var accountData = JsonConvert.DeserializeObject<dynamic>(json);
                    
                    if (accountData != null)
                    {
                        return new AccountInfo
                        {
                            WeChatId = accountData.wxid?.ToString() ?? "",
                            NickName = accountData.nickname?.ToString() ?? "",
                            Avatar = accountData.avatar?.ToString() ?? "",
                            BoundAccount = accountData.account?.ToString() ?? "",
                            Phone = accountData.phone?.ToString() ?? "",
                            DeviceId = accountData.device_id?.ToString() ?? "",
                            WxUserDir = accountData.wx_user_dir?.ToString() ?? "",
                            UnreadMsgCount = accountData.unread_msg_count ?? 0,
                            IsFakeDeviceId = accountData.is_fake_device_id ?? 0,
                            Pid = accountData.pid ?? 0
                        };
                    }
                }
                else
                {
                    Logger.LogWarning($"获取账号信息失败: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"获取账号信息异常: {ex.Message}", ex);
            }
            
            return null;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

