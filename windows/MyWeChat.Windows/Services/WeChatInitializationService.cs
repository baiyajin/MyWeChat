using System;
using System.Threading.Tasks;
using System.Windows.Threading;
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

                        // 初始化微信管理器
                        if (!weChatManager.Initialize())
                        {
                            onLog?.Invoke("微信管理器初始化失败");
                            return;
                        }

                        _weChatManager = weChatManager;
                        _isInitialized = true;

                        onLog?.Invoke("微信管理器初始化成功");

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
                                        onLog?.Invoke("微信进程检测定时器已启动");
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

                if (_weChatManager != null)
                {
                    _weChatManager.StopProcessCheckTimer();
                    _weChatManager.Dispose();
                    _weChatManager = null;
                }
                
                _isInitialized = false;
                _isDisposed = true;
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

