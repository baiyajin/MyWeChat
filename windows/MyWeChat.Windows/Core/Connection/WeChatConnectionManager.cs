using System;
using MyWeChat.Windows.Core.Hook;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Core.Connection
{
    /// <summary>
    /// 微信连接管理器
    /// 负责管理微信客户端的连接状态和Hook状态
    /// </summary>
    public class WeChatConnectionManager
    {
        private WeChatHookManager? _hookManager;
        private string? _weChatVersion;
        private bool _isConnected;
        private bool _isInitialized = false;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected => _isConnected && _hookManager != null && _hookManager.IsHooked;

        /// <summary>
        /// 微信版本号
        /// </summary>
        public string? WeChatVersion => _weChatVersion;

        /// <summary>
        /// 客户端ID
        /// </summary>
        public int ClientId => _hookManager?.ClientId ?? 0;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// Hook消息接收事件
        /// </summary>
        public event EventHandler<string>? OnMessageReceived;

        /// <summary>
        /// 初始化连接管理器
        /// </summary>
        public bool Initialize()
        {
            // 防止重复初始化
            if (_isInitialized)
            {
                Logger.LogWarning("连接管理器已初始化，跳过重复初始化");
                return true;
            }
            
            try
            {
                Logger.LogInfo("========== 开始初始化连接管理器 ==========");
                
                // 检测微信版本（减少日志输出）
                _weChatVersion = WeChatVersionDetector.DetectWeChatVersion();
                Logger.LogInfo($"检测微信版本: {(_weChatVersion ?? "未检测到")}");
                
                if (string.IsNullOrEmpty(_weChatVersion))
                {
                    Logger.LogError("未检测到微信版本");
                    Logger.LogError("可能的原因:");
                    Logger.LogError("  1. 微信未安装");
                    Logger.LogError("  2. 注册表中没有微信信息");
                    Logger.LogError("  3. 微信安装路径不正确");
                    return false;
                }

                // 尝试标准化版本号（如果找不到精确匹配，会自动选择最接近的版本）
                string? normalizedVersion = WeChatVersionDetector.DetectWeChatVersion();
                if (normalizedVersion != _weChatVersion)
                {
                    Logger.LogInfo($"微信版本标准化: {_weChatVersion} -> {normalizedVersion}");
                }
                
                if (string.IsNullOrEmpty(normalizedVersion))
                {
                    Logger.LogError($"无法找到支持的微信版本，检测到版本: {_weChatVersion}");
                    Logger.LogError("请检查DLLs目录中是否有对应版本的DLL文件");
                    return false;
                }

                // 如果版本被标准化了，更新版本号
                if (normalizedVersion != _weChatVersion)
                {
                    Logger.LogInfo($"微信版本 {_weChatVersion} 已匹配到支持版本 {normalizedVersion}");
                    _weChatVersion = normalizedVersion;
                }

                Logger.LogInfo($"使用微信版本: {_weChatVersion}");

                // 检查DLL目录
                string? dllDirectory = WeChatVersionDetector.GetDllDirectoryPath(_weChatVersion);
                if (string.IsNullOrEmpty(dllDirectory) || !System.IO.Directory.Exists(dllDirectory))
                {
                    Logger.LogError($"DLL目录不存在: {dllDirectory ?? "null"}");
                    return false;
                }

                // 检查关键DLL文件
                string wxHelpPath = System.IO.Path.Combine(dllDirectory, "WxHelp.dll");
                if (!System.IO.File.Exists(wxHelpPath))
                {
                    Logger.LogWarning($"WxHelp.dll不存在: {wxHelpPath}");
                }

                // 初始化Hook管理器
                _hookManager = new WeChatHookManager();
                
                // 在初始化之前订阅Hook事件，确保不会丢失消息
                // 订阅Hook事件
                _hookManager.OnHooked += (sender, clientId) =>
                {
                    _isConnected = true;
                    OnConnectionStateChanged?.Invoke(this, true);
                    Logger.LogInfo($"微信连接成功，ClientId: {clientId}");
                };

                _hookManager.OnUnhooked += (sender, e) =>
                {
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    Logger.LogInfo("微信连接已断开");
                };

                // 订阅Hook消息事件，转发到外部（减少日志输出）
                _hookManager.OnMessageReceived += (sender, message) =>
                {
                    // 只在重要消息时输出日志
                    if (message.Contains("\"type\":1112") || message.Contains("\"messageType\":1112"))
                    {
                        Logger.LogInfo($"连接管理器收到微信登录回调");
                    }
                    OnMessageReceived?.Invoke(this, message);
                };
                
                if (!_hookManager.Initialize(_weChatVersion))
                {
                    Logger.LogError("Hook管理器初始化失败");
                    return false;
                }

                _isInitialized = true;
                Logger.LogInfo("========== 连接管理器初始化完成 ==========");
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"连接管理器初始化失败: {ex.Message}");
                Logger.LogError($"异常类型: {ex.GetType().Name}");
                Logger.LogError($"堆栈跟踪: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"内部异常: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// 连接微信
        /// </summary>
        /// <param name="weChatExePath">微信可执行文件路径</param>
        /// <returns>返回是否成功</returns>
        public bool Connect(string? weChatExePath = null)
        {
            try
            {
                Logger.LogInfo("========== 开始连接微信 ==========");
                
                if (_hookManager == null)
                {
                    string errorMsg = "Hook管理器未初始化，请先调用Initialize()方法";
                    Logger.LogError(errorMsg);
                    throw new InvalidOperationException(errorMsg);
                }

                Logger.LogInfo($"Hook管理器状态: 已初始化");
                Logger.LogInfo($"微信版本: {_weChatVersion ?? "未知"}");
                Logger.LogInfo($"指定的微信路径: {weChatExePath ?? "自动查找"}");
                
                bool result = _hookManager.OpenAndHook(weChatExePath);
                
                if (result)
                {
                    Logger.LogInfo("========== 微信连接成功 ==========");
                    Logger.LogInfo($"客户端ID: {ClientId}");
                    Logger.LogInfo($"连接状态: {IsConnected}");
                }
                else
                {
                    Logger.LogError("========== 微信连接失败 ==========");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                string errorMsg = $"连接微信异常: {ex.Message}";
                string stackTrace = ex.StackTrace ?? "";
                string innerException = ex.InnerException != null ? $"内部异常: {ex.InnerException.Message}" : "";
                
                Logger.LogError($"========== 连接微信发生异常 ==========");
                Logger.LogError($"异常类型: {ex.GetType().Name}");
                Logger.LogError($"错误消息: {errorMsg}");
                Logger.LogError($"堆栈跟踪: {stackTrace}");
                if (!string.IsNullOrEmpty(innerException))
                {
                    Logger.LogError(innerException);
                }
                throw;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _hookManager?.CloseHook();
        }

        /// <summary>
        /// 发送命令
        /// </summary>
        public bool SendCommand(int commandType, object? data = null)
        {
            if (!IsConnected)
            {
                Logger.LogWarning("微信未连接，无法发送命令");
                return false;
            }

            return _hookManager?.SendCommand(commandType, data) ?? false;
        }
    }
}

