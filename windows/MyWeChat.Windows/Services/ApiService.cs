using System;
using System.Net.Http;
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
    /// </summary>
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverUrl;

        public ApiService(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            // 设置请求头，标识支持加密
            _httpClient.DefaultRequestHeaders.Add("X-Encryption", "AES-256-GCM");
        }

        /// <summary>
        /// 根据手机号获取账号信息
        /// </summary>
        public async Task<AccountInfo?> GetAccountInfoByPhoneAsync(string phone)
        {
            try
            {
                string url = $"{_serverUrl}/api/account?phone={Uri.EscapeDataString(phone)}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    // 尝试解密响应（如果服务器返回加密数据）
                    try
                    {
                        var responseObj = JsonConvert.DeserializeObject<dynamic>(json);
                        if (responseObj != null && responseObj.encrypted == true && responseObj.data != null)
                        {
                            // 加密响应，需要解密（HTTP API使用固定密钥，与服务器一致）
                            string encryptedData = responseObj.data.ToString();
                            json = EncryptionService.DecryptStringForLog(encryptedData);
                        }
                    }
                    catch
                    {
                        // 解密失败或非加密响应，使用原始JSON
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
                string url = $"{_serverUrl}/api/account?wxid={Uri.EscapeDataString(wxid)}";
                HttpResponseMessage response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    
                    // 尝试解密响应（如果服务器返回加密数据）
                    try
                    {
                        var responseObj = JsonConvert.DeserializeObject<dynamic>(json);
                        if (responseObj != null && responseObj.encrypted == true && responseObj.data != null)
                        {
                            // 加密响应，需要解密（HTTP API使用固定密钥，与服务器一致）
                            string encryptedData = responseObj.data.ToString();
                            json = EncryptionService.DecryptStringForLog(encryptedData);
                        }
                    }
                    catch
                    {
                        // 解密失败或非加密响应，使用原始JSON
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

