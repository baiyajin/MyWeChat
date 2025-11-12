using System;
using System.Diagnostics;
using System.Windows.Threading;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Utils;
using Newtonsoft.Json;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 微信管理器
    /// 封装微信进程检测、启动、连接管理等通用功能
    /// </summary>
    public class WeChatManager : IDisposable
    {
        private WeChatConnectionManager? _connectionManager;
        private DispatcherTimer? _processCheckTimer;
        private bool _isWeChatConnected = false;
        private string? _currentWxid = null;
        private bool _isDisposed = false;
        private readonly object _lock = new object();
        
        // 定时器检测间隔（秒）
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(3);
        
        // 窗口上下文（用于UI更新）
        private Dispatcher? _dispatcher;

        /// <summary>
        /// 是否已连接微信
        /// </summary>
        public bool IsConnected => _isWeChatConnected && _connectionManager != null && _connectionManager.IsConnected;

        /// <summary>
        /// 当前微信ID
        /// </summary>
        public string? CurrentWxid => _currentWxid;

        /// <summary>
        /// 微信连接管理器（供外部使用）
        /// </summary>
        public WeChatConnectionManager? ConnectionManager => _connectionManager;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// 微信ID获取事件（1112回调）
        /// </summary>
        public event EventHandler<string>? OnWxidReceived;

        /// <summary>
        /// 微信消息接收事件（所有消息，包括1112）
        /// </summary>
        public event EventHandler<string>? OnMessageReceived;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dispatcher">UI调度器（用于UI线程更新）</param>
        public WeChatManager(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// 初始化微信管理器
        /// </summary>
        public bool Initialize()
        {
            lock (_lock)
            {
                if (_connectionManager != null)
                {
                    Logger.LogWarning("微信管理器已初始化，跳过重复初始化");
                    return true;
                }

                try
                {
                    _connectionManager = new WeChatConnectionManager();

                    // 订阅连接状态变化事件
                    _connectionManager.OnConnectionStateChanged += (sender, isConnected) =>
                    {
                        _isWeChatConnected = isConnected;
                        OnConnectionStateChanged?.Invoke(this, isConnected);
                    };

                    // 订阅消息接收事件（用于获取1112回调）
                    _connectionManager.OnMessageReceived += OnWeChatMessageReceived;

                    // 初始化连接管理器
                    if (!_connectionManager.Initialize())
                    {
                        Logger.LogError("微信连接管理器初始化失败");
                        _connectionManager = null;
                        return false;
                    }

                    Logger.LogInfo("微信管理器初始化成功");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"初始化微信管理器失败: {ex.Message}", ex);
                    _connectionManager = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// 启动微信进程检测定时器
        /// </summary>
        public void StartProcessCheckTimer()
        {
            lock (_lock)
            {
                if (_processCheckTimer != null)
                {
                    Logger.LogWarning("微信进程检测定时器已启动，跳过重复启动");
                    return;
                }

                try
                {
                    _processCheckTimer = new DispatcherTimer
                    {
                        Interval = _checkInterval
                    };
                    _processCheckTimer.Tick += ProcessCheckTimer_Tick;
                    _processCheckTimer.Start();

                    Logger.LogInfo("微信进程检测定时器已启动");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"启动微信进程检测定时器失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 停止微信进程检测定时器
        /// </summary>
        public void StopProcessCheckTimer()
        {
            lock (_lock)
            {
                if (_processCheckTimer != null)
                {
                    _processCheckTimer.Stop();
                    _processCheckTimer.Tick -= ProcessCheckTimer_Tick;
                    _processCheckTimer = null;
                    Logger.LogInfo("微信进程检测定时器已停止");
                }
            }
        }

        /// <summary>
        /// 手动连接微信
        /// </summary>
        public bool Connect()
        {
            lock (_lock)
            {
                if (_connectionManager == null)
                {
                    Logger.LogWarning("微信连接管理器未初始化，无法连接");
                    return false;
                }

                try
                {
                    bool result = _connectionManager.Connect();
                    if (result)
                    {
                        _isWeChatConnected = true;
                        Logger.LogInfo("微信连接成功");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"微信连接失败: {ex.Message}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 断开微信连接
        /// </summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                if (_connectionManager != null && _connectionManager.IsConnected)
                {
                    _connectionManager.Disconnect();
                    _isWeChatConnected = false;
                    Logger.LogInfo("微信连接已断开");
                }
            }
        }

        /// <summary>
        /// 检查微信进程是否运行
        /// </summary>
        public bool IsWeChatProcessRunning()
        {
            try
            {
                Process[] weChatProcesses = Process.GetProcessesByName("WeChat");
                if (weChatProcesses.Length > 0)
                {
                    return true;
                }

                Process[] weixinProcesses = Process.GetProcessesByName("Weixin");
                if (weixinProcesses.Length > 0)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"检查微信进程失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 微信进程检测定时器事件
        /// </summary>
        private void ProcessCheckTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                bool weChatRunning = IsWeChatProcessRunning();

                if (weChatRunning && !_isWeChatConnected)
                {
                    // 发现微信进程，但未连接，尝试连接
                    Logger.LogInfo("检测到微信进程，尝试连接...");

                    if (_connectionManager != null)
                    {
                        bool result = _connectionManager.Connect();
                        if (result)
                        {
                            _isWeChatConnected = true;
                            Logger.LogInfo("微信连接成功");
                        }
                    }
                }
                else if (!weChatRunning && _isWeChatConnected)
                {
                    // 微信进程已退出，断开连接
                    Logger.LogInfo("微信进程已退出，断开连接");
                    _isWeChatConnected = false;
                    if (_connectionManager != null && _connectionManager.IsConnected)
                    {
                        _connectionManager.Disconnect();
                    }
                }
                else if (!weChatRunning)
                {
                    // 微信进程未运行，自动启动微信
                    Logger.LogInfo("微信进程未运行，正在自动启动微信...");

                    if (_connectionManager != null && !_isWeChatConnected)
                    {
                        try
                        {
                            bool result = _connectionManager.Connect();
                            if (result)
                            {
                                _isWeChatConnected = true;
                                Logger.LogInfo("微信自动启动成功");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"微信自动启动异常: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"微信进程检测失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理微信消息（用于获取1112回调）
        /// </summary>
        private void OnWeChatMessageReceived(object? sender, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                // 确保message不为null（编译器警告修复）
                string nonNullMessage = message ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nonNullMessage))
                {
                    return;
                }

                // ========== 全局服务日志：微信消息接收 ==========
                Logger.LogInfo($"========== [全局服务] 收到微信消息 ==========");
                Logger.LogInfo($"[全局服务] 消息长度: {nonNullMessage.Length}");
                Logger.LogInfo($"[全局服务] 收到时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

                // 先触发通用消息接收事件
                OnMessageReceived?.Invoke(this, nonNullMessage);

                // 清理消息：移除空白和控制字符
                string cleanMessage = nonNullMessage.Trim();
                
                // 清理无效的控制字符（保留JSON必需字符）
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                foreach (char c in cleanMessage)
                {
                    if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                    {
                        continue;
                    }
                    sb.Append(c);
                }
                cleanMessage = sb.ToString();

                dynamic? messageObj = null;
                try
                {
                    messageObj = JsonConvert.DeserializeObject<dynamic>(cleanMessage);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning($"JSON解析失败，尝试修复: {ex.Message}");
                    
                    // 策略1: 提取第一个完整的JSON对象
                    int firstBrace = cleanMessage.IndexOf('{');
                    int lastBrace = cleanMessage.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        string extractedJson = cleanMessage.Substring(firstBrace, lastBrace - firstBrace + 1);
                        try
                        {
                            messageObj = JsonConvert.DeserializeObject<dynamic>(extractedJson);
                            Logger.LogInfo("通过提取JSON对象成功解析");
                        }
                        catch
                        {
                            Logger.LogWarning($"提取JSON对象后仍解析失败，忽略此消息");
                            return;
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"无法找到完整的JSON对象，忽略此消息");
                        return;
                    }
                }

                if (messageObj == null) return;

                // 获取消息类型
                int messageType = 0;
                if (messageObj.type != null)
                {
                    int.TryParse(messageObj.type.ToString(), out messageType);
                }

                // ========== 全局服务日志：消息类型 ==========
                Logger.LogInfo($"[全局服务] 消息类型: {messageType}");

                // 根据消息类型输出相应日志
                switch (messageType)
                {
                    case 1112:
                        // 账号信息回调，在下面处理
                        break;
                    case 11126:
                        Logger.LogInfo($"[全局服务] 收到联系人列表消息（11126）");
                        break;
                    case 11132:
                        Logger.LogInfo($"[全局服务] 收到文本消息（11132）");
                        break;
                    case 11144:
                        Logger.LogInfo($"[全局服务] 收到朋友圈消息（11144）");
                        break;
                    case 11241:
                        Logger.LogInfo($"[全局服务] 收到标签消息（11241）");
                        break;
                    case 11238:
                        Logger.LogInfo($"[全局服务] 收到其他消息（11238）");
                        break;
                    case 5:
                        Logger.LogInfo($"[全局服务] 收到公众号消息（5）");
                        break;
                    default:
                        if (messageType > 0)
                        {
                            Logger.LogInfo($"[全局服务] 收到未知类型消息: {messageType}");
                        }
                        break;
                }

                // 1112 表示账号信息回调
                if (messageType == 1112)
                {
                    dynamic? loginInfo = null;

                    if (messageObj?.data != null)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson) && dataJson.TrimStart().StartsWith("{"))
                        {
                            try
                            {
                                loginInfo = JsonConvert.DeserializeObject<dynamic>(dataJson) ?? null;
                            }
                            catch
                            {
                                loginInfo = messageObj.data;
                            }
                        }
                        else
                        {
                            loginInfo = messageObj.data;
                        }
                    }

                    if (loginInfo != null)
                    {
                        // 尝试多种方式获取wxid
                        string wxid = loginInfo.wxid?.ToString() ?? "";
                        if (string.IsNullOrEmpty(wxid))
                        {
                            wxid = loginInfo.wxId?.ToString() ?? "";
                        }
                        if (string.IsNullOrEmpty(wxid))
                        {
                            wxid = loginInfo.WxId?.ToString() ?? "";
                        }

                        // 检查是否是进程ID（纯数字）
                        if (!string.IsNullOrEmpty(wxid) && !int.TryParse(wxid, out _))
                        {
                            lock (_lock)
                            {
                                _currentWxid = wxid;
                            }
                            
                            // ========== 全局服务日志：账号信息解析结果 ==========
                            string nickname = loginInfo.nickname?.ToString() ?? "";
                            string avatar = loginInfo.avatar?.ToString() ?? "";
                            string account = loginInfo.account?.ToString() ?? "";
                            Logger.LogInfo($"[全局服务] ========== 收到微信账号数据（1112回调） ==========");
                            Logger.LogInfo($"[全局服务] wxid: {wxid}");
                            Logger.LogInfo($"[全局服务] nickname: {nickname}");
                            Logger.LogInfo($"[全局服务] avatar: {avatar}");
                            Logger.LogInfo($"[全局服务] account: {account}");

                            // 触发wxid获取事件
                            OnWxidReceived?.Invoke(this, wxid);
                        }
                        else
                        {
                            Logger.LogWarning($"[全局服务] 收到的wxid无效（可能是进程ID）: {wxid}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理微信消息失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_lock)
            {
                // 停止定时器
                StopProcessCheckTimer();

                // 断开连接
                Disconnect();

                // 清理事件订阅
                if (_connectionManager != null)
                {
                    _connectionManager.OnConnectionStateChanged -= null;
                    _connectionManager.OnMessageReceived -= null;
                    _connectionManager = null;
                }

                _isDisposed = true;
            }

            Logger.LogInfo("微信管理器已释放");
        }
    }
}

