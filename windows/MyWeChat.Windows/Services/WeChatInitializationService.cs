using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 微信初始化服务（全局单例）
    /// 封装检测微信、打开微信、注入dll、等待微信返回登录账号信息的通用逻辑
    /// </summary>
    public class WeChatInitializationService : IDisposable
    {
        private static WeChatInitializationService? _instance;
        private static readonly object _instanceLock = new object();
        
        private WeChatManager? _weChatManager;
        private Dispatcher? _dispatcher;
        private bool _isInitialized = false;
        private bool _isDisposed = false;
        private readonly object _lock = new object();
        
        // 待匹配的手机号列表（key: phone, value: timestamp）
        private readonly Dictionary<string, DateTime> _pendingPhoneNumbers = new Dictionary<string, DateTime>();
        private readonly object _pendingPhoneLock = new object();
        
        // API服务和WebSocket服务（用于数据库查询和同步到app端）
        private ApiService? _apiService;
        private WebSocketService? _webSocketService;

        /// <summary>
        /// 获取全局单例实例
        /// </summary>
        public static WeChatInitializationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new WeChatInitializationService();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 微信管理器（供外部使用）
        /// </summary>
        public WeChatManager? WeChatManager => _weChatManager;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// 微信ID获取事件（1112回调）
        /// </summary>
        public event EventHandler<string>? OnWxidReceived;

        /// <summary>
        /// 微信消息接收事件（所有消息）
        /// </summary>
        public event EventHandler<string>? OnMessageReceived;

        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private WeChatInitializationService()
        {
        }

        /// <summary>
        /// 初始化微信管理器（异步方法，不阻塞UI）
        /// 如果已经初始化，则跳过重复初始化
        /// </summary>
        public void InitializeAsync(Dispatcher dispatcher, Action<string>? onLog = null)
        {
            if (_isDisposed)
            {
                onLog?.Invoke("微信初始化服务已释放，无法初始化");
                return;
            }

            lock (_lock)
            {
                if (_isInitialized)
                {
                    onLog?.Invoke("微信管理器已初始化，跳过重复初始化");
                    return;
                }

                _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            }

            Task.Run(() =>
            {
                try
                {
                    lock (_lock)
                    {
                        if (_isInitialized || _isDisposed)
                        {
                            return;
                        }

                        onLog?.Invoke("正在初始化微信管理器...");

                        // 创建微信管理器
                        var weChatManager = new WeChatManager(dispatcher);

                        // 订阅连接状态变化事件
                        weChatManager.OnConnectionStateChanged += (sender, isConnected) =>
                        {
                            OnConnectionStateChanged?.Invoke(this, isConnected);
                        };

                        // 订阅微信ID获取事件（1112回调）
                        weChatManager.OnWxidReceived += (sender, wxid) =>
                        {
                            OnWxidReceived?.Invoke(this, wxid);
                        };

                        // 订阅消息接收事件
                        weChatManager.OnMessageReceived += (sender, message) =>
                        {
                            OnMessageReceived?.Invoke(this, message);
                        };
                        
                        // 订阅账号信息接收事件（1112回调，包含完整账号信息）
                        weChatManager.OnAccountInfoReceived += OnAccountInfoReceived;
                        
                        // ========== 全局服务日志：监听激活 ==========
                        Logger.LogInfo("========== [全局服务] 微信消息监听已激活 ==========");
                        Logger.LogInfo("[全局服务] 正在监听微信账号数据消息...");
                        
                        // 初始化API服务和WebSocket服务（用于数据库查询和同步到app端）
                        string serverUrl = ConfigHelper.GetServerUrl();
                        _apiService = new ApiService(serverUrl);
                        
                        string wsUrl = ConfigHelper.GetWebSocketUrl();
                        _webSocketService = new WebSocketService(wsUrl);
                        
                        // 异步连接WebSocket（不阻塞初始化）
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _webSocketService.ConnectAsync();
                                if (_webSocketService.IsConnected)
                                {
                                    // 发送客户端类型
                                    await _webSocketService.SendMessageAsync(new
                                    {
                                        type = "client_type",
                                        client_type = "windows"
                                    });
                                    Logger.LogInfo("[全局服务] WebSocket连接成功，可用于同步账号信息到app端");
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogWarning($"[全局服务] WebSocket连接失败: {ex.Message}，将在需要时重试");
                            }
                        });

                        // 初始化微信管理器
                        if (!weChatManager.Initialize())
                        {
                            onLog?.Invoke("微信管理器初始化失败");
                            return;
                        }

                        _weChatManager = weChatManager;
                        _isInitialized = true;

                        // 注意：微信管理器初始化成功和定时器启动的日志已在WeChatManager中输出，这里不再重复输出

                        // 启动进程检测定时器（必须在UI线程上执行）
                        dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                lock (_lock)
                                {
                                    if (_weChatManager != null && !_isDisposed)
                                    {
                                        _weChatManager.StartProcessCheckTimer();
                                        // 注意：定时器启动的日志已在WeChatManager中输出，这里不再重复输出
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.LogError($"启动进程检测定时器失败: {ex.Message}", ex);
                            }
                        }, DispatcherPriority.Normal);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"初始化微信管理器失败: {ex.Message}", ex);
                    onLog?.Invoke($"初始化微信管理器失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 释放资源（仅在应用退出时调用）
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;

            lock (_lock)
            {
                if (_isDisposed) return;

                // 取消订阅账号信息接收事件
                if (_weChatManager != null)
                {
                    _weChatManager.OnAccountInfoReceived -= OnAccountInfoReceived;
                    _weChatManager.StopProcessCheckTimer();
                    _weChatManager.Dispose();
                    _weChatManager = null;
                }
                
                // 释放API服务和WebSocket服务
                _apiService?.Dispose();
                _apiService = null;
                
                _webSocketService?.Dispose();
                _webSocketService = null;
                
                _isInitialized = false;
                _isDisposed = true;
            }
        }

        /// <summary>
        /// 添加待匹配的手机号
        /// </summary>
        /// <param name="phone">手机号</param>
        public void AddPendingPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return;
            }

            lock (_pendingPhoneLock)
            {
                if (!_pendingPhoneNumbers.ContainsKey(phone))
                {
                    _pendingPhoneNumbers[phone] = DateTime.Now;
                    Logger.LogInfo($"[全局服务] 添加待匹配手机号: {phone}");
                }
                else
                {
                    Logger.LogInfo($"[全局服务] 手机号已存在于待匹配列表: {phone}");
                }
            }
        }

        /// <summary>
        /// 移除待匹配的手机号
        /// </summary>
        /// <param name="phone">手机号</param>
        public void RemovePendingPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return;
            }

            lock (_pendingPhoneLock)
            {
                if (_pendingPhoneNumbers.Remove(phone))
                {
                    Logger.LogInfo($"[全局服务] 移除待匹配手机号: {phone}");
                }
            }
        }

        /// <summary>
        /// 获取所有待匹配的手机号
        /// </summary>
        /// <returns>待匹配手机号列表</returns>
        public List<string> GetPendingPhones()
        {
            lock (_pendingPhoneLock)
            {
                return new List<string>(_pendingPhoneNumbers.Keys);
            }
        }

        /// <summary>
        /// 检查手机号是否在待匹配列表中
        /// </summary>
        /// <param name="phone">手机号</param>
        /// <returns>是否在待匹配列表中</returns>
        public bool IsPendingPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }

            lock (_pendingPhoneLock)
            {
                return _pendingPhoneNumbers.ContainsKey(phone);
            }
        }

        /// <summary>
        /// 处理账号信息接收事件（1112回调）
        /// </summary>
        private void OnAccountInfoReceived(object? sender, AccountInfo accountInfo)
        {
            if (accountInfo == null || string.IsNullOrEmpty(accountInfo.WeChatId))
            {
                return;
            }

            // 在后台线程处理，避免阻塞
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo($"[全局服务] ========== 处理1112回调账号信息 ==========");
                    Logger.LogInfo($"[全局服务] wxid: {accountInfo.WeChatId}, phone: {accountInfo.Phone}");

                    // 如果1112回调中有phone，直接用phone查询数据库
                    if (!string.IsNullOrEmpty(accountInfo.Phone))
                    {
                        Logger.LogInfo($"[全局服务] 1112回调中包含phone: {accountInfo.Phone}，查询数据库...");
                        
                        if (_apiService != null)
                        {
                            var dbAccountInfo = await _apiService.GetAccountInfoByPhoneAsync(accountInfo.Phone);
                            if (dbAccountInfo != null && !string.IsNullOrEmpty(dbAccountInfo.WeChatId))
                            {
                                // 检查wxid是否匹配
                                if (dbAccountInfo.WeChatId == accountInfo.WeChatId)
                                {
                                    // 数据库中有匹配的记录，且wxid一致
                                    Logger.LogInfo($"[全局服务] 数据库中找到匹配记录: wxid={dbAccountInfo.WeChatId}, phone={accountInfo.Phone}");
                                    
                                    // 同步到app端（使用1112回调中的最新数据）
                                    await SyncAccountInfoToAppAsync(accountInfo);
                                    
                                    // 从待匹配列表中移除
                                    RemovePendingPhone(accountInfo.Phone);
                                    
                                    Logger.LogInfo($"[全局服务] 账号信息已匹配并同步到app端");
                                    return;
                                }
                                else
                                {
                                    // wxid不一致，可能是不同的账号
                                    Logger.LogWarning($"[全局服务] 数据库中的wxid({dbAccountInfo.WeChatId})与1112回调中的wxid({accountInfo.WeChatId})不一致，跳过匹配");
                                }
                            }
                            else
                            {
                                Logger.LogInfo($"[全局服务] 数据库中未找到phone={accountInfo.Phone}的记录");
                            }
                        }
                    }
                    
                    // 如果1112回调中没有phone，或者数据库中没有匹配记录，检查待匹配手机号列表
                    List<string> pendingPhones = GetPendingPhones();
                    if (pendingPhones.Count > 0)
                    {
                        Logger.LogInfo($"[全局服务] 检查待匹配手机号列表，共{pendingPhones.Count}个");
                        
                        foreach (string phone in pendingPhones)
                        {
                            if (_apiService != null)
                            {
                                var dbAccountInfo = await _apiService.GetAccountInfoByPhoneAsync(phone);
                                if (dbAccountInfo != null && !string.IsNullOrEmpty(dbAccountInfo.WeChatId))
                                {
                                    // 检查wxid是否匹配
                                    if (dbAccountInfo.WeChatId == accountInfo.WeChatId)
                                    {
                                        // 匹配成功！
                                        Logger.LogInfo($"[全局服务] ========== 账号信息匹配成功！ ==========");
                                        Logger.LogInfo($"[全局服务] phone: {phone}, wxid: {accountInfo.WeChatId}");
                                        
                                        // 更新账号信息的phone字段
                                        accountInfo.Phone = phone;
                                        
                                        // 同步到app端
                                        await SyncAccountInfoToAppAsync(accountInfo);
                                        
                                        // 从待匹配列表中移除
                                        RemovePendingPhone(phone);
                                        
                                        Logger.LogInfo($"[全局服务] 账号信息已匹配并同步到app端");
                                        return;
                                    }
                                }
                            }
                        }
                        
                        Logger.LogInfo($"[全局服务] 待匹配手机号列表中未找到匹配的wxid，继续等待");
                    }
                    
                    // 如果都没有匹配上，保存账号信息到数据库（不关联手机号）
                    // 注意：这里只是同步到app端，数据库的保存应该由服务器端处理
                    // 如果WebSocket已连接，同步到app端，让服务器保存到数据库
                    if (!string.IsNullOrEmpty(accountInfo.Phone))
                    {
                        // 如果1112回调中有phone，直接同步
                        await SyncAccountInfoToAppAsync(accountInfo);
                        Logger.LogInfo($"[全局服务] 账号信息已同步到app端（等待服务器保存到数据库）");
                    }
                    else
                    {
                        Logger.LogInfo($"[全局服务] 1112回调中无phone，且待匹配列表无匹配，等待用户登录时输入手机号");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[全局服务] 处理账号信息失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 同步账号信息到app端
        /// </summary>
        private async Task SyncAccountInfoToAppAsync(AccountInfo accountInfo)
        {
            try
            {
                if (_webSocketService == null)
                {
                    Logger.LogWarning("[全局服务] WebSocketService未初始化，无法同步账号信息");
                    return;
                }

                // 确保WebSocket已连接
                if (!_webSocketService.IsConnected)
                {
                    Logger.LogInfo("[全局服务] WebSocket未连接，尝试连接...");
                    bool connected = await _webSocketService.ConnectAsync();
                    if (!connected)
                    {
                        Logger.LogWarning("[全局服务] WebSocket连接失败，无法同步账号信息");
                        return;
                    }
                    
                    // 发送客户端类型
                    await _webSocketService.SendMessageAsync(new
                    {
                        type = "client_type",
                        client_type = "windows"
                    });
                }

                // 通过WebSocket发送到服务器（发送所有字段）
                var syncData = new
                {
                    type = "sync_my_info",
                    data = new
                    {
                        wxid = accountInfo.WeChatId,
                        nickname = accountInfo.NickName ?? "",
                        avatar = accountInfo.Avatar ?? "",
                        account = accountInfo.BoundAccount ?? accountInfo.WeChatId,
                        device_id = accountInfo.DeviceId ?? "",
                        phone = accountInfo.Phone ?? "",
                        wx_user_dir = accountInfo.WxUserDir ?? "",
                        unread_msg_count = accountInfo.UnreadMsgCount,
                        is_fake_device_id = accountInfo.IsFakeDeviceId,
                        pid = accountInfo.Pid
                    }
                };

                await _webSocketService.SendMessageAsync(syncData);
                Logger.LogInfo($"[全局服务] 账号信息已同步到app端: wxid={accountInfo.WeChatId}, phone={accountInfo.Phone}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[全局服务] 同步账号信息到app端失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重置单例（用于测试或特殊场景）
        /// </summary>
        public static void ResetInstance()
        {
            lock (_instanceLock)
            {
                if (_instance != null)
                {
                    _instance.Dispose();
                    _instance = null;
                }
            }
        }
    }
}

