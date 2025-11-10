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
        private readonly object _initLock = new object(); // 初始化锁

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
            // 使用锁防止多线程重复初始化
            lock (_initLock)
            {
                // 防止重复初始化
                if (_isInitialized)
                {
                    // 只在调试时输出警告，避免日志过多
                    // Logger.LogWarning("连接管理器已初始化，跳过重复初始化");
                    return true;
                }
            }
            
            try
            {
                Logger.LogInfo("========== 开始初始化连接管理器 ==========");
                
                // 检测微信版本（只调用一次，减少日志输出）
                _weChatVersion = WeChatVersionDetector.DetectWeChatVersion();
                
                if (string.IsNullOrEmpty(_weChatVersion))
                {
                    Logger.LogError("未检测到微信版本");
                    return false;
                }

                // 标准化版本号（DetectWeChatVersion已经返回标准化后的版本）
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

                // 初始化成功后才设置标志
                lock (_initLock)
                {
                    _isInitialized = true;
                }

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
                // 初始化失败时，不设置标志，允许重试
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

