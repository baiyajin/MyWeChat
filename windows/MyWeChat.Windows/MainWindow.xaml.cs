using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Services;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public partial class MainWindow : Window
    {
        // 使用全局单例服务，不再需要 _weChatManager 字段
        private bool _isInitializing = false;
        private bool _isInitialized = false; // 标记是否已完成初始化
        private readonly object _initLock = new object(); // 初始化锁
        private WebSocketService? _webSocketService;
        private ContactSyncService? _contactSyncService;
        private MomentsSyncService? _momentsSyncService;
        private TagSyncService? _tagSyncService;
        private ChatMessageSyncService? _chatMessageSyncService;
        private OfficialAccountSyncService? _officialAccountSyncService;
        private CommandService? _commandService;
        private ObservableCollection<AccountInfo>? _accountList;
        private ApiService? _apiService;
        
        // 定时器：获取微信账号信息
        private DispatcherTimer? _accountInfoFetchTimer;
        
        // 窗口关闭处理器
        private WindowCloseHandler? _closeHandler;
        
        // 关闭进度遮罩辅助类
        private ClosingProgressHelper? _closingProgressHelper;
        
        // 系统托盘服务
        private TrayIconService? _trayIconService;
        
        

        /// <summary>
        /// 判断是否是进程ID（纯数字），而不是真正的微信ID
        /// </summary>
        private static bool IsProcessId(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            // 如果整个字符串都是数字，则认为是进程ID
            return int.TryParse(value, out _);
        }

        /// <summary>
        /// 判断是否是真正的微信ID（不是进程ID）
        /// </summary>
        private static bool IsRealWeChatId(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            // 如果不是纯数字，则认为是真正的微信ID
            return !IsProcessId(value);
        }

        private string? _loggedInWxid; // 当前登录的微信账号ID

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainWindow(string? wxid = null)
        {
            InitializeComponent();
            // 设置窗口标题为"w"
            this.Title = "w";
            _accountList = new ObservableCollection<AccountInfo>();
            _loggedInWxid = wxid;
            
            // 延迟初始化服务，避免在构造函数中初始化导致崩溃
            UpdateUI();
        }

        /// <summary>
        /// 窗口加载完成事件
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 确保窗口标题为"w"（在Loaded事件中再次设置，确保覆盖任何默认值）
                this.Title = "w";
                
                // 防止 Loaded 事件被多次触发导致重复初始化
                if (_isInitialized || _isInitializing)
                {
                    Logger.LogWarning("窗口已初始化或正在初始化，跳过重复初始化");
                    return;
                }
                
                // 窗口加载完成后再初始化服务
                InitializeServices();
            }
            catch (Exception ex)
            {
                Logger.LogError($"窗口加载时初始化服务失败: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        private void InitializeServices()
        {
            // 使用锁防止多线程重复初始化
            lock (_initLock)
            {
                // 防止重复初始化
                if (_isInitializing || _isInitialized)
                {
                    Logger.LogWarning("服务正在初始化中或已初始化，跳过重复初始化");
                    return;
                }
                
                // 检查全局服务是否已初始化
                if (WeChatInitializationService.Instance.IsInitialized)
                {
                    Logger.LogWarning("微信管理器已初始化，跳过重复初始化");
                    _isInitialized = true;
                    return;
                }
                
                _isInitializing = true;
            }
            
            try
            {
                // 延迟初始化连接管理器，避免在UI线程中直接初始化导致崩溃
                Task.Run(() =>
                {
                    try
                    {
                        // 使用全局单例服务
                        var service = WeChatInitializationService.Instance;
                        
                        // 使用 BeginInvoke 异步调用，避免阻塞后台线程
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            AddLog("正在初始化微信管理器...", "INFO");
                        }));

                        // 如果已经初始化，直接订阅事件
                        if (service.IsInitialized)
                        {
                            Logger.LogInfo("微信管理器已在登录窗口初始化，直接订阅事件");
                        }
                        else
                        {
                            // 订阅事件
                            service.OnConnectionStateChanged += OnConnectionStateChanged;
                            service.OnMessageReceived += OnWeChatMessageReceived;
                            
                            // 初始化微信管理器
                            service.InitializeAsync(Dispatcher, (log) =>
                            {
                                Logger.LogInfo(log);
                            });
                            
                            // 等待初始化完成
                            Thread.Sleep(1000);
                        }
                        
                        // 订阅事件（无论是否已初始化，都需要订阅）
                        service.OnConnectionStateChanged += OnConnectionStateChanged;
                        service.OnMessageReceived += OnWeChatMessageReceived;
                        
                        // 获取微信管理器
                        var weChatManager = service.WeChatManager;
                        if (weChatManager == null)
                        {
                            Logger.LogError("无法获取微信管理器");
                            return;
                        }
                        
                        // 初始化成功，立即更新UI显示版本号
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateUI();
                        }));

                        // 初始化WebSocket服务
                        string webSocketUrl = ConfigurationManager.AppSettings["WebSocketUrl"] ?? "ws://localhost:8000/ws";
                        _webSocketService = new WebSocketService(webSocketUrl);
                        _webSocketService.OnMessageReceived += OnWebSocketMessageReceived;
                        _webSocketService.OnConnectionStateChanged += OnWebSocketConnectionStateChanged;

                        // 初始化API服务（用于查询数据库中的账号信息）
                        string serverUrl = ConfigHelper.GetServerUrl();
                        _apiService = new ApiService(serverUrl);

                        // 获取连接管理器
                        WeChatConnectionManager? connectionManager = weChatManager.ConnectionManager;
                        if (connectionManager == null)
                        {
                            Logger.LogError("无法获取微信连接管理器");
                            return;
                        }

                        // 初始化同步服务
                        _contactSyncService = new ContactSyncService(connectionManager, _webSocketService, GetCurrentWeChatId);
                        _momentsSyncService = new MomentsSyncService(connectionManager, _webSocketService, GetCurrentWeChatId);
                        _tagSyncService = new TagSyncService(connectionManager, _webSocketService, GetCurrentWeChatId);
                        _chatMessageSyncService = new ChatMessageSyncService(connectionManager, _webSocketService, GetCurrentWeChatId);
                        _officialAccountSyncService = new OfficialAccountSyncService(connectionManager, _webSocketService, GetCurrentWeChatId);

                        // 初始化命令服务
                        _commandService = new CommandService(connectionManager);
                        
                        // 设置同步服务到命令服务（用于处理同步命令）
                        _commandService.SetSyncServices(_contactSyncService, _momentsSyncService, _tagSyncService);

                        // 连接WebSocket
                        _ = Task.Run(async () => await _webSocketService.ConnectAsync());

                        Logger.LogInfo("服务初始化完成");
                        
                        // 启动定时器检测微信进程（如果还没启动）
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 定时器由全局服务管理，如果已初始化则已启动
                            if (!service.IsInitialized)
                            {
                                // 如果还没初始化，等待一下再初始化窗口关闭处理器
                                Task.Delay(500).ContinueWith(_ =>
                                {
                                    Dispatcher.InvokeAsync(() =>
                                    {
                                        InitializeCloseHandler();
                                    });
                                });
                            }
                            else
                            {
                                // 服务初始化完成后，初始化窗口关闭处理器
                                InitializeCloseHandler();
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"初始化服务失败: {ex.Message}", ex);
                        _ = Dispatcher.BeginInvoke(new Action(() =>
                        {
                            System.Windows.MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }));
                    }
                    finally
                    {
                        lock (_initLock)
                        {
                            _isInitializing = false;
                            _isInitialized = true; // 标记为已初始化
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"初始化服务失败: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                lock (_initLock)
                {
                    _isInitializing = false;
                    // 初始化失败时不设置 _isInitialized，允许下次重试
                }
            }
        }
        

        // Windows API 用于显示窗口
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        
        private const int SW_RESTORE = 9; // 恢复窗口
        private const int SW_SHOW = 5; // 显示窗口
        
        /// <summary>
        /// 检查微信窗口是否可见（用于MainWindow特定的窗口可见性检查）
        /// </summary>
        private bool CheckWeChatWindowVisible()
        {
            try
            {
                Process? weChatProcess = null;
                
                // 检查 WeChat 进程
                Process[] weChatProcesses = Process.GetProcessesByName("WeChat");
                if (weChatProcesses.Length > 0)
                {
                    weChatProcess = weChatProcesses[0];
                }
                else
                {
                    // 检查 Weixin 进程（新版本）
                    Process[] weixinProcesses = Process.GetProcessesByName("Weixin");
                    if (weixinProcesses.Length > 0)
                    {
                        weChatProcess = weixinProcesses[0];
                    }
                }
                
                if (weChatProcess != null)
                {
                    // 检查窗口是否可见
                    try
                    {
                        weChatProcess.Refresh();
                        IntPtr mainWindowHandle = weChatProcess.MainWindowHandle;
                        
                        if (mainWindowHandle != IntPtr.Zero)
                        {
                            bool isVisible = IsWindowVisible(mainWindowHandle);
                            
                            if (!isVisible)
                            {
                                Logger.LogWarning($"检测到微信进程（PID: {weChatProcess.Id}），但窗口不可见，尝试显示窗口...");
                                AddLog($"微信窗口不可见，正在显示窗口...", "WARN");
                                
                                // 尝试显示窗口
                                ShowWindow(mainWindowHandle, SW_RESTORE);
                                ShowWindow(mainWindowHandle, SW_SHOW);
                                SetForegroundWindow(mainWindowHandle);
                                
                                // 等待一下让窗口显示
                                System.Threading.Thread.Sleep(500);
                                
                                // 再次检查
                                isVisible = IsWindowVisible(mainWindowHandle);
                                if (isVisible)
                                {
                                    Logger.LogInfo("微信窗口已显示");
                                    AddLog("微信窗口已显示", "SUCCESS");
                                }
                                else
                                {
                                    Logger.LogWarning("无法显示微信窗口，可能窗口被最小化或隐藏");
                                }
                            }
                            else
                            {
                                Logger.LogInfo($"检测到微信进程（PID: {weChatProcess.Id}），窗口可见");
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"检测到微信进程（PID: {weChatProcess.Id}），但无法获取主窗口句柄（可能窗口未创建）");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"检查微信窗口可见性失败: {ex.Message}");
                    }
                    
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
        /// 启动账号信息获取定时器
        /// </summary>
        private void StartAccountInfoFetchTimer()
        {
            try
            {
                if (_accountInfoFetchTimer != null)
                {
                    _accountInfoFetchTimer.Stop();
                    _accountInfoFetchTimer = null;
                }

                _accountInfoFetchTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5) // 每5秒获取一次账号信息
                };
                _accountInfoFetchTimer.Tick += AccountInfoFetchTimer_Tick;
                _accountInfoFetchTimer.Start();

                Logger.LogInfo("已启动账号信息获取定时器（每5秒获取一次）");
                
                // 立即执行一次
                AccountInfoFetchTimer_Tick(null, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Logger.LogError($"启动账号信息获取定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 停止账号信息获取定时器
        /// </summary>
        private void StopAccountInfoFetchTimer()
        {
            try
            {
                if (_accountInfoFetchTimer != null)
                {
                    _accountInfoFetchTimer.Stop();
                    _accountInfoFetchTimer = null;
                    Logger.LogInfo("已停止账号信息获取定时器");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止账号信息获取定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 账号信息获取定时器事件
        /// </summary>
        private void AccountInfoFetchTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                var weChatManager = GetWeChatManager();
                if (weChatManager == null || !weChatManager.IsConnected)
                {
                    Logger.LogInfo("[定时器] 微信未连接，跳过账号信息检查");
                    return;
                }

                // 输出定时器运行日志，让用户知道程序正在运行
                Logger.LogInfo("[定时器] 正在检查账号信息...");

                // 检查是否已有完整的账号信息（从账号列表中检查 account、nickname 等字段）
                bool hasAccountInfo = false;
                if (_accountList != null)
                {
                    foreach (var acc in _accountList)
                    {
                        // 检查关键字段：account、nickname
                        bool hasAccount = !string.IsNullOrEmpty(acc.BoundAccount) || !string.IsNullOrEmpty(acc.WeChatId);
                        bool hasNickname = !string.IsNullOrEmpty(acc.NickName);
                        
                        // 如果有关键字段（account 和 nickname），则认为账号信息完整
                        if (hasAccount && hasNickname)
                        {
                            hasAccountInfo = true;
                            Logger.LogInfo($"[定时器] 已检测到完整账号信息: account={acc.BoundAccount ?? acc.WeChatId}, nickname={acc.NickName}");
                            break;
                        }
                    }
                }

                // 如果已登录但未获取到账号信息，从数据库查询
                if (!hasAccountInfo && !string.IsNullOrEmpty(_loggedInWxid))
                {
                    Logger.LogInfo($"[定时器] 已登录但未获取到账号信息，从数据库查询: wxid={_loggedInWxid}");
                    
                    // 从数据库查询账号信息
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (_apiService != null)
                            {
                                var accountInfo = await _apiService.GetAccountInfoAsync(_loggedInWxid);
                                if (accountInfo != null)
                                {
                                    // 更新UI显示账号信息
                                    _ = Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (_accountList != null)
                                        {
                                            // 检查是否已存在该账号
                                            bool exists = false;
                                            foreach (var acc in _accountList)
                                            {
                                                if (acc.WeChatId == accountInfo.WeChatId)
                                                {
                                                    // 更新现有账号信息
                                                    acc.NickName = accountInfo.NickName;
                                                    acc.Avatar = accountInfo.Avatar;
                                                    acc.BoundAccount = accountInfo.BoundAccount;
                                                    acc.Phone = accountInfo.Phone;
                                                    acc.DeviceId = accountInfo.DeviceId;
                                                    acc.WxUserDir = accountInfo.WxUserDir;
                                                    acc.UnreadMsgCount = accountInfo.UnreadMsgCount;
                                                    acc.IsFakeDeviceId = accountInfo.IsFakeDeviceId;
                                                    acc.Pid = accountInfo.Pid;
                                                    exists = true;
                                                    break;
                                                }
                                            }
                                            
                                            if (!exists)
                                            {
                                                // 添加新账号信息
                                                _accountList.Add(accountInfo);
                                            }
                                            
                                            // 更新显示
                                            UpdateAccountInfoDisplay();
                                            StopTimersAfterAccountInfoReceived();
                                            
                                            Logger.LogInfo($"[定时器] 从数据库成功获取账号信息: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");
                                        }
                                    }));
                                }
                                else
                                {
                                    Logger.LogWarning($"[定时器] 从数据库未找到账号信息: wxid={_loggedInWxid}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[定时器] 从数据库查询账号信息失败: {ex.Message}", ex);
                        }
                    });
                }
                else if (!hasAccountInfo)
                {
                    // 还没有账号信息，继续等待 1112 回调消息
                    Logger.LogInfo("[定时器] 尚未收到账号信息，继续等待1112回调消息...");
                    // 不主动请求，只检查
                }
                else
                {
                    // 已有账号信息，更新显示并停止定时器
                    Logger.LogInfo("[定时器] 账号信息已完整，准备停止定时器");
                    UpdateAccountInfoDisplay();
                    StopTimersAfterAccountInfoReceived();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"定时获取账号信息失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查账号信息是否完整（从JSON消息中检查account、avatar、nickname等字段），如果完整则停止定时器
        /// </summary>
        private void StopTimersAfterAccountInfoReceived()
        {
            try
            {
                // 检查是否已有完整的账号信息（从账号列表中检查）
                bool hasCompleteAccountInfo = false;
                if (_accountList != null)
                {
                    foreach (var acc in _accountList)
                    {
                        // 检查关键字段：account、avatar、nickname 都不为空
                        bool hasAccount = !string.IsNullOrEmpty(acc.BoundAccount) || !string.IsNullOrEmpty(acc.WeChatId);
                        bool hasAvatar = !string.IsNullOrEmpty(acc.Avatar);
                        bool hasNickname = !string.IsNullOrEmpty(acc.NickName);
                        
                        // 如果有关键字段（account 和 nickname），则认为账号信息完整
                        // avatar 是可选的，但 account 和 nickname 是必需的
                        if (hasAccount && hasNickname)
                        {
                            hasCompleteAccountInfo = true;
                            Logger.LogInfo($"检测到完整账号信息: account={acc.BoundAccount ?? acc.WeChatId}, nickname={acc.NickName}, avatar={(!string.IsNullOrEmpty(acc.Avatar) ? "有" : "无")}");
                            break;
                        }
                    }
                }

                if (hasCompleteAccountInfo)
                {
                    // 定时器由全局服务管理，不需要手动停止
                    Logger.LogInfo("已获取到完整账号信息（account、nickname等字段）");
                    AddLog("已获取到完整账号信息", "SUCCESS");

                    // 停止账号信息获取定时器
                    StopAccountInfoFetchTimer();
                    Logger.LogInfo("已获取到完整账号信息（account、nickname等字段），停止账号信息获取定时器");
                    AddLog("已获取到完整账号信息，停止账号信息获取定时器", "SUCCESS");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查JSON消息中的账号信息字段是否完整（account、avatar、nickname等）
        /// </summary>
        private bool IsAccountInfoCompleteFromJson(dynamic? loginInfo)
        {
            try
            {
                if (loginInfo == null)
                {
                    return false;
                }

                // 检查关键字段：account、nickname
                // account 字段（尝试多种命名方式）
                string account = loginInfo.account?.ToString() ?? "";
                if (string.IsNullOrEmpty(account))
                {
                    account = loginInfo.Account?.ToString() ?? "";
                }
                if (string.IsNullOrEmpty(account))
                {
                    // 如果没有 account，尝试使用 wxid
                    account = loginInfo.wxid?.ToString() ?? "";
                    if (string.IsNullOrEmpty(account))
                    {
                        account = loginInfo.wxId?.ToString() ?? "";
                    }
                    if (string.IsNullOrEmpty(account))
                    {
                        account = loginInfo.WxId?.ToString() ?? "";
                    }
                }

                // nickname 字段（尝试多种命名方式）
                string nickname = loginInfo.nickname?.ToString() ?? "";
                if (string.IsNullOrEmpty(nickname))
                {
                    nickname = loginInfo.nickName?.ToString() ?? "";
                }
                if (string.IsNullOrEmpty(nickname))
                {
                    nickname = loginInfo.NickName?.ToString() ?? "";
                }

                // 检查关键字段是否都存在
                bool hasAccount = !string.IsNullOrEmpty(account);
                bool hasNickname = !string.IsNullOrEmpty(nickname);

                // avatar 是可选的，但 account 和 nickname 是必需的
                bool isComplete = hasAccount && hasNickname;

                if (isComplete)
                {
                    Logger.LogInfo($"JSON消息中的账号信息完整: account={account}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(loginInfo.avatar?.ToString() ?? loginInfo.Avatar?.ToString() ?? "") ? "有" : "无")}");
                }
                else
                {
                    Logger.LogWarning($"JSON消息中的账号信息不完整: account={(!string.IsNullOrEmpty(account) ? account : "空")}, nickname={(!string.IsNullOrEmpty(nickname) ? nickname : "空")}");
                }

                return isComplete;
            }
            catch (Exception ex)
            {
                Logger.LogError($"检查JSON消息中的账号信息失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 自动连接微信（检测已登录的微信）- 已废弃，改用定时器检测
        /// </summary>
        private void AutoConnectWeChat()
        {
            try
            {
                if (_weChatManager == null)
                {
                    AddLog("微信管理器未初始化，无法自动连接微信", "WARN");
                    return;
                }

                AddLog("步骤1: 检查连接管理器状态...", "INFO");
                
                var weChatManager = GetWeChatManager();
                if (weChatManager == null || !weChatManager.IsConnected)
                {
                    AddLog("步骤2: 微信未连接，尝试连接...", "INFO");
                    
                    var weChatManager2 = GetWeChatManager();
                    bool result = weChatManager2?.Connect() ?? false;
                    
                    if (result)
                    {
                        AddLog("========== 微信连接成功 ==========", "SUCCESS");
                            AddLog($"微信版本: {weChatManager2?.ConnectionManager?.WeChatVersion ?? "未知"}", "SUCCESS");
                        AddLog($"客户端ID: {weChatManager2?.ConnectionManager?.ClientId ?? 0}", "SUCCESS");
                        AddLog($"连接状态: {(weChatManager2?.IsConnected == true ? "已连接" : "未连接")}", weChatManager2?.IsConnected == true ? "SUCCESS" : "WARN");
                        
                        // 连接成功后，自动更新账号列表
                        UpdateAccountList();
                    }
                    else
                    {
                        AddLog("========== 微信连接失败 ==========", "WARN");
                        AddLog("可能的原因:", "WARN");
                        AddLog("  1. 微信未安装或安装路径不正确", "WARN");
                        AddLog("  2. 微信未运行或未登录", "WARN");
                        AddLog("  3. DLL文件不存在或版本不匹配", "WARN");
                        AddLog("  4. 未以管理员权限运行（需要管理员权限）", "WARN");
                        AddLog("  5. 微信版本不支持", "WARN");
                        var weChatManager = GetWeChatManager();
                        AddLog($"当前微信版本: {weChatManager?.ConnectionManager?.WeChatVersion ?? "未知"}", "WARN");
                        AddLog("提示: 如果微信已登录，请稍等片刻，程序会自动检测", "INFO");
                    }
                }
                else
                {
                    AddLog("微信已连接，正在获取账号信息...", "INFO");
                    UpdateAccountList();
                    
                    // 连接成功后，等待微信发送的 1112 回调消息
                    // 数据格式为: {"data":{"account":"...","avatar":"...","nickname":"...","wxid":"..."},"type":1112}
                    // 不主动请求，只等待回调消息
                    Logger.LogInfo("连接成功，等待微信发送1112回调消息...");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"自动连接微信失败: {ex.Message}", ex);
                Logger.LogError($"异常类型: {ex.GetType().Name}", ex);
                Logger.LogError($"堆栈跟踪: {ex.StackTrace}", ex);
                if (ex.InnerException != null)
                {
                    Logger.LogError($"内部异常: {ex.InnerException.Message}", ex);
                }
                
                AddLog($"自动连接微信失败: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// 更新UI
        /// </summary>
        private void UpdateUI()
        {
            try
            {
                // 获取微信版本（即使未连接，如果已检测到版本也显示）
                var weChatManager = GetWeChatManager();
                string weChatVersion = weChatManager?.ConnectionManager?.WeChatVersion ?? "未知";
                
                // 更新连接状态（UI元素已移除，不再更新）
                // 如果有账号信息，更新显示
                var weChatManager = GetWeChatManager();
                if (weChatManager != null && weChatManager.IsConnected)
                {
                    UpdateAccountInfoDisplay();
                }
                
                // 版本号显示已移除（之前显示在左上角连接状态区域）
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新UI失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 获取当前登录账号的真正wxid
        /// </summary>
        /// <returns>返回真正的wxid，如果未找到则返回空字符串</returns>
        private string GetCurrentWeChatId()
        {
            try
            {
                // 优先从WeChatManager获取wxid
                string? wxid = _weChatManager?.CurrentWxid;
                if (!string.IsNullOrEmpty(wxid) && IsRealWeChatId(wxid))
                {
                    Logger.LogInfo($"从WeChatManager获取到真正的wxid: {wxid}");
                    return wxid;
                }

                if (_accountList == null || _accountList.Count == 0)
                {
                    Logger.LogWarning("账号列表为空，无法获取真正的wxid");
                    return string.Empty;
                }

                // 优先查找有真正wxid的账号（不是进程ID）
                foreach (var account in _accountList)
                {
                    if (IsRealWeChatId(account.WeChatId))
                    {
                        Logger.LogInfo($"从账号列表获取到真正的wxid: {account.WeChatId}");
                        return account.WeChatId;
                    }
                }

                // 如果没找到，尝试查找任何有昵称的账号（可能是从WebSocket同步的）
                foreach (var account in _accountList)
                {
                    if (!string.IsNullOrEmpty(account.NickName) && !string.IsNullOrEmpty(account.WeChatId))
                    {
                        // 检查是否是真正的wxid（不是进程ID）
                        if (!IsProcessId(account.WeChatId))
                        {
                            Logger.LogInfo($"获取到真正的wxid（从有昵称的账号）: {account.WeChatId}");
                            return account.WeChatId;
                        }
                    }
                }

                Logger.LogWarning("未找到真正的wxid，账号列表中可能只有进程ID");
                return string.Empty;
            }
            catch (Exception ex)
            {
                Logger.LogError($"获取当前微信ID失败: {ex.Message}", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// 更新账号信息显示
        /// </summary>
        private void UpdateAccountInfoDisplay()
        {
            try
            {
                var weChatManager = GetWeChatManager();
                if (weChatManager == null || !weChatManager.IsConnected)
                {
                    Logger.LogWarning("连接管理器未初始化或未连接，无法更新账号信息显示");
                    // 显示等待状态
                    Dispatcher.Invoke(() =>
                    {
                        if (AccountNickNameText != null)
                        {
                            AccountNickNameText.Text = "等待连接...";
                        }
                        if (AccountWeChatIdText != null)
                        {
                            AccountWeChatIdText.Text = "";
                        }
                    });
                    return;
                }

                // 从账号列表中查找当前登录的账号
                // 优先查找有昵称和头像的账号（从WebSocket同步过来的）
                var weChatManager = GetWeChatManager();
                int clientId = weChatManager?.ConnectionManager?.ClientId ?? 0;
                
                if (_accountList == null)
                {
                    Logger.LogWarning("账号列表未初始化");
                    return;
                }
                
                Logger.LogInfo($"更新账号信息显示: clientId={clientId}, 账号列表数量={_accountList.Count}");
                
                AccountInfo? currentAccount = null;
                
                // 优先查找有真正wxid的账号（不是进程ID）
                foreach (var account in _accountList)
                {
                    Logger.LogInfo($"检查账号: WeChatId={account.WeChatId}, NickName={account.NickName}, Avatar={(!string.IsNullOrEmpty(account.Avatar) ? "有头像" : "无头像")}");
                    
                    // 优先匹配有真正wxid的账号（不是纯数字的进程ID）
                    if (IsRealWeChatId(account.WeChatId))
                    {
                        // 优先选择有昵称和头像的账号
                        if (currentAccount == null || 
                            (!string.IsNullOrEmpty(account.NickName) && !string.IsNullOrEmpty(account.Avatar)))
                        {
                            currentAccount = account;
                            Logger.LogInfo($"找到匹配账号: WeChatId={account.WeChatId}, NickName={account.NickName}");
                        }
                    }
                }
                
                // 如果没找到，尝试查找任何有昵称的账号（可能是从WebSocket同步的）
                if (currentAccount == null)
                {
                    foreach (var account in _accountList)
                    {
                        if (!string.IsNullOrEmpty(account.NickName))
                        {
                            currentAccount = account;
                            Logger.LogInfo($"找到有昵称的账号: WeChatId={account.WeChatId}, NickName={account.NickName}");
                            break;
                        }
                    }
                }

                if (currentAccount != null)
                {
                    Logger.LogInfo($"更新账号信息显示: NickName={currentAccount.NickName}, Avatar={(!string.IsNullOrEmpty(currentAccount.Avatar) ? "有头像" : "无头像")}");
                    
                    // 更新UI显示
                    Dispatcher.Invoke(() =>
                    {
                        // 更新昵称
                        if (AccountNickNameText != null)
                        {
                            AccountNickNameText.Text = !string.IsNullOrEmpty(currentAccount.NickName) 
                                ? currentAccount.NickName 
                                : "未知昵称";
                        }
                    
                    // 更新微信ID
                        if (AccountWeChatIdText != null)
                        {
                            string displayId = !string.IsNullOrEmpty(currentAccount.BoundAccount) 
                        ? currentAccount.BoundAccount 
                                : (!string.IsNullOrEmpty(currentAccount.WeChatId) ? currentAccount.WeChatId : "");
                            AccountWeChatIdText.Text = !string.IsNullOrEmpty(displayId) ? $"微信号: {displayId}" : "";
                        }
                        
                        // 更新头像
                        if (AccountAvatarImage != null)
                        {
                            UpdateAvatarImage(AccountAvatarImage, currentAccount.Avatar);
                        }
                    });
                }
                else
                {
                    // 没有找到账号信息
                    Logger.LogInfo("未找到账号信息");
                    Dispatcher.Invoke(() =>
                    {
                        if (AccountNickNameText != null)
                        {
                            AccountNickNameText.Text = "等待获取账号信息...";
                        }
                        if (AccountWeChatIdText != null)
                        {
                            AccountWeChatIdText.Text = "";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新账号信息显示失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新头像图片
        /// </summary>
        private void UpdateAvatarImage(System.Windows.Controls.Image imageControl, string? avatarUrl)
        {
            try
            {
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    try
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(avatarUrl, UriKind.Absolute);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        imageControl.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"加载头像失败: {ex.Message}");
                        // 使用默认头像
                        imageControl.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon.ico"));
                    }
                }
                else
                {
                    // 使用默认头像
                    imageControl.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon.ico"));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新头像失败: {ex.Message}", ex);
                imageControl.Source = new BitmapImage(new Uri("pack://application:,,,/Resources/favicon.ico"));
            }
        }


        /// <summary>
        /// 连接状态变化事件处理
        /// </summary>
        private void OnConnectionStateChanged(object? sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateUI();
                
                // 连接状态变化时，更新账号列表
                if (isConnected)
                {
                    Logger.LogInfo("微信连接状态变化：已连接");
                    UpdateAccountList();
                    // 更新账号信息显示
                    UpdateAccountInfoDisplay();
                    
                    // 连接成功后，启动账号信息获取定时器
                    // 不再自动同步其他数据（好友、朋友圈、标签等），这些数据通过 app 端点击时，通过服务端请求
                    Logger.LogInfo("连接成功，启动账号信息获取定时器");
                    StartAccountInfoFetchTimer();
                }
                else
                {
                    Logger.LogInfo("微信连接状态变化：已断开");
                    // 断开连接时，清空账号列表
                    _accountList?.Clear();
                    // UI元素已移除，不再更新显示
                }
            });
        }
        
        /// <summary>
        /// 更新账号列表
        /// </summary>
        private void UpdateAccountList()
        {
            try
            {
                var weChatManager = GetWeChatManager();
                if (weChatManager == null || !weChatManager.IsConnected)
                {
                    return;
                }

                // 检查是否已存在该账号
                // 注意：不要使用clientId（进程ID）作为WeChatId，应该等待真正的wxid
                var weChatManager = GetWeChatManager();
                int clientId = weChatManager?.ConnectionManager?.ClientId ?? 0;
                
                // 只查找有真正wxid的账号（不是进程ID），不查找以进程ID作为WeChatId的账号
                bool exists = false;
                if (_accountList != null)
                {
                foreach (var account in _accountList)
                {
                        // 只匹配有真正wxid的账号（不是纯数字的进程ID）
                        if (IsRealWeChatId(account.WeChatId))
                    {
                        exists = true;
                            Logger.LogInfo($"已存在账号: WeChatId={account.WeChatId}");
                        break;
                        }
                    }
                }

                // 如果没有真正的wxid，不创建账号（等待1112回调提供真正的wxid）
                if (!exists)
                {
                    Logger.LogInfo("账号列表中暂无真正的wxid，等待1112回调提供账号信息");
                    // 不创建以进程ID作为WeChatId的账号
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新账号列表失败: {ex.Message}", ex);
                AddLog($"更新账号列表失败: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// 检查账号信息
        /// 不主动请求，只等待微信发送的 1112 回调消息
        /// 数据格式为: {"data":{"account":"...","avatar":"...","nickname":"...","wxid":"..."},"type":1112}
        /// 好友、标签、朋友圈等数据由 app 端触发
        /// </summary>
        private void CheckAccountInfo()
        {
            try
            {
                var weChatManager = GetWeChatManager();
                if (weChatManager == null || !weChatManager.IsConnected)
                {
                    Logger.LogWarning("微信未连接，无法检查账号信息");
                    return;
                }

                // 只检查是否已收到账号信息，不主动请求
                // 账号信息应该通过微信发送的 1112 回调消息获取
                // 数据格式为: {"data":{"account":"...","avatar":"...","nickname":"...","wxid":"..."},"type":1112}
                Logger.LogInfo("检查是否已收到账号信息（等待1112回调消息）");
                
                // 检查账号列表中是否已有完整的账号信息
                bool hasAccountInfo = false;
                if (_accountList != null)
                {
                    foreach (var acc in _accountList)
                    {
                        // 检查关键字段：account、nickname
                        bool hasAccount = !string.IsNullOrEmpty(acc.BoundAccount) || !string.IsNullOrEmpty(acc.WeChatId);
                        bool hasNickname = !string.IsNullOrEmpty(acc.NickName);
                        
                        if (hasAccount && hasNickname)
                        {
                            hasAccountInfo = true;
                            Logger.LogInfo($"已检测到完整账号信息: account={acc.BoundAccount ?? acc.WeChatId}, nickname={acc.NickName}");
                            break;
                        }
                    }
                }
                
                if (hasAccountInfo)
                {
                    // 已有账号信息，更新显示并停止定时器
                    UpdateAccountInfoDisplay();
                    StopTimersAfterAccountInfoReceived();
                }
                else
                {
                    // 还没有账号信息，继续等待 1112 回调消息
                    Logger.LogInfo("尚未收到账号信息，继续等待1112回调消息...");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"检查账号信息失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// WebSocket连接状态变化事件处理
        /// </summary>
        private void OnWebSocketConnectionStateChanged(object? sender, bool isConnected)
        {
            Dispatcher.Invoke(() =>
            {
                AddLog(isConnected ? "App连接: 已连接" : "App连接: 未连接", isConnected ? "SUCCESS" : "WARN");
                
                // WebSocket连接成功后，如果本地有账号信息，主动同步到app端
                if (isConnected)
                {
                    Logger.LogInfo("WebSocket连接成功");
                }
            });
        }

        /// <summary>
        /// MyWeChat消息接收事件处理
        /// </summary>
        private void OnWeChatMessageReceived(object? sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 记录收到消息的详细信息，用于调试
                    Logger.LogInfo($"========== 收到微信消息 ==========");
                    Logger.LogInfo($"消息长度: {message?.Length ?? 0}");
                    Logger.LogInfo($"消息内容: {message}");
                    Logger.LogInfo($"收到时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                    
                    AddLog($"收到微信消息: {message}", "INFO");
                    
                    // 清理消息：移除可能的额外字符和空白
                    string cleanMessage = message?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(cleanMessage))
                    {
                        Logger.LogWarning("收到空消息，忽略");
                        return;
                    }

                    // 清理无效字符（包括问号、控制字符等），这些字符可能导致JSON解析失败
                    // 1. 修复URL路径中的问号问题（/e 或 /? 应该是 /0）
                    // 从截图看，问号出现在avatar URL中，如 /e 或 /? 而不是 /0
                    // 匹配URL路径末尾的 /e 或 /? 后跟引号、逗号或右括号
                    cleanMessage = System.Text.RegularExpressions.Regex.Replace(
                        cleanMessage, 
                        @"(https?://[^""]+)/([?e])(["",}])", 
                        match => match.Groups[1].Value + "/0" + match.Groups[3].Value
                    );
                    
                    // 2. 清理控制字符（但保留JSON必需的控制字符）
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (char c in cleanMessage)
                    {
                        // 保留可打印字符和JSON必需的控制字符（换行、制表符等）
                        if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                        {
                            // 跳过其他控制字符
                            continue;
                        }
                        // 如果问号在URL路径末尾（如 /? 应该是 /0），替换为 0
                        if (c == '?' && sb.Length > 0 && sb[sb.Length - 1] == '/')
                        {
                            sb.Append('0');
                            continue;
                        }
                        sb.Append(c);
                    }
                    cleanMessage = sb.ToString();
                    
                    // 记录原始消息长度
                    int originalLength = cleanMessage.Length;

                    // 3. 移除消息末尾的乱码字符（非JSON字符）
                    // 找到最后一个有效的 `}` 位置
                    int lastValidBrace = cleanMessage.LastIndexOf('}');
                    if (lastValidBrace >= 0)
                    {
                        // 如果 `}` 后面还有字符，检查是否是乱码
                        if (lastValidBrace < cleanMessage.Length - 1)
                        {
                            string afterBrace = cleanMessage.Substring(lastValidBrace + 1);
                            // 检查是否是乱码（包含控制字符、非ASCII字符等）
                            bool isGarbage = false;
                            foreach (char c in afterBrace)
                            {
                                // 控制字符（除了换行、制表符等）或非ASCII字符（可能是乱码）
                                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                                {
                                    isGarbage = true;
                                    break;
                                }
                                // 如果字符不是有效的JSON字符（}、]、数字、字母、标点），可能是乱码
                                if (!char.IsLetterOrDigit(c) && c != '}' && c != ']' && c != ' ' && c != '\t' && c != '\n' && c != '\r')
                                {
                                    // 检查是否是有效的JSON标点符号
                                    if (!"{}[]:,\"".Contains(c))
                                    {
                                        isGarbage = true;
                                        break;
                                    }
                                }
                            }
                            if (isGarbage)
                            {
                                Logger.LogWarning($"检测到消息末尾有乱码字符，移除: {System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(afterBrace))}");
                                cleanMessage = cleanMessage.Substring(0, lastValidBrace + 1);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有找到 `}`，尝试找到第一个 `{` 和最后一个可能的结束位置
                        int firstBrace = cleanMessage.IndexOf('{');
                        if (firstBrace >= 0)
                        {
                            // 从后往前查找，找到最后一个可能是 `}` 的位置
                            for (int i = cleanMessage.Length - 1; i > firstBrace; i--)
                            {
                                if (cleanMessage[i] == '}')
                                {
                                    // 检查后面是否有乱码
                                    if (i < cleanMessage.Length - 1)
                                    {
                                        string afterBrace = cleanMessage.Substring(i + 1);
                                        // 如果后面都是控制字符或乱码，截断到这里
                                        bool allGarbage = true;
                                        foreach (char c in afterBrace)
                                        {
                                            if (!char.IsControl(c) || c == '\n' || c == '\r' || c == '\t')
                                            {
                                                allGarbage = false;
                                                break;
                                            }
                                        }
                                        if (allGarbage)
                                        {
                                            cleanMessage = cleanMessage.Substring(0, i + 1);
                                            Logger.LogWarning("检测到消息末尾有乱码，已截断到最后一个有效 `}`");
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // 4. 修复被截断的type字段
                    // 注意：type:1112 是正确的类型，表示账号信息回调，不需要修复
                    // 情况1: "type":111200 后面有乱码，如 "type":111200（11120后面有乱码）
                    if (cleanMessage.Contains("\"type\":111200"))
                    {
                        Logger.LogWarning("检测到type字段包含乱码（111200），尝试修复为11120");
                        cleanMessage = cleanMessage.Replace("\"type\":111200", "\"type\":11120");
                    }
                    // 情况3: "type":11126 后面有乱码，如 "type":111206
                    if (cleanMessage.Contains("\"type\":111206"))
                    {
                        Logger.LogWarning("检测到type字段包含乱码（111206），尝试修复为11126");
                        cleanMessage = cleanMessage.Replace("\"type\":111206", "\"type\":11126");
                    }
                    // 情况4: "type":11238 后面有乱码
                    if (cleanMessage.Contains("\"type\":11238") && cleanMessage.IndexOf("\"type\":11238") < cleanMessage.Length - 10)
                    {
                        int typeIndex = cleanMessage.IndexOf("\"type\":11238");
                        string afterType = cleanMessage.Substring(typeIndex + "\"type\":11238".Length);
                        // 如果type后面不是 } 或 ]，可能有乱码
                        if (!afterType.TrimStart().StartsWith("}") && !afterType.TrimStart().StartsWith("]"))
                        {
                            // 找到type后面的第一个有效字符位置
                            int nextBrace = afterType.IndexOf('}');
                            if (nextBrace > 0)
                            {
                                string garbage = afterType.Substring(0, nextBrace);
                                if (garbage.Contains("") || garbage.Length > 5)
                                {
                                    Logger.LogWarning($"检测到type字段后有乱码，移除: {garbage}");
                                    cleanMessage = cleanMessage.Substring(0, typeIndex + "\"type\":11238".Length) + afterType.Substring(nextBrace);
                                }
                            }
                        }
                    }

                    // 5. 确保消息以 `}` 结尾
                    if (!cleanMessage.EndsWith("}") && cleanMessage.StartsWith("{"))
                    {
                        Logger.LogWarning("检测到JSON消息不完整（缺少闭合括号），尝试修复");
                            cleanMessage = cleanMessage + "}";
                        }
                    
                    // 记录清理后的消息长度
                    if (originalLength != cleanMessage.Length)
                    {
                        Logger.LogInfo($"原始消息长度: {originalLength}, 清理后长度: {cleanMessage.Length}");
                    }

                    // 解析消息
                    dynamic? messageObj = null;
                    try
                    {
                        messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(cleanMessage) ?? null;
                    }
                    catch (Newtonsoft.Json.JsonException ex)
                    {
                        Logger.LogWarning($"JSON解析失败，尝试修复: {ex.Message}");
                        Logger.LogWarning($"原始消息: {message}");
                        Logger.LogWarning($"清理后的消息: {cleanMessage}");
                        
                        // 尝试多种修复策略
                        bool isFixed = false;
                        
                        // 策略1: 提取第一个完整的JSON对象
                        int firstBrace = cleanMessage.IndexOf('{');
                        int lastBrace = cleanMessage.LastIndexOf('}');
                        if (firstBrace >= 0 && lastBrace > firstBrace)
                        {
                            string extractedJson = cleanMessage.Substring(firstBrace, lastBrace - firstBrace + 1);
                            try
                            {
                                messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(extractedJson) ?? null;
                                if (messageObj != null)
                                {
                                    Logger.LogInfo("通过提取JSON对象成功解析");
                                    isFixed = true;
                                }
                            }
                            catch
                            {
                                // 继续尝试其他策略
                            }
                        }
                        
                        // 策略2: 如果消息包含 "type":1112，这是正确的类型，不需要修复
                        // type:1112 表示账号信息回调，数据格式为 {"data":{...},"type":1112}
                        // 如果消息包含 "type":111200，可能是11120后面有乱码，尝试修复
                        if (!isFixed && cleanMessage.Contains("\"type\":111200"))
                        {
                            string fixedJson = cleanMessage.Replace("\"type\":111200", "\"type\":11120");
                            if (!fixedJson.EndsWith("}"))
                            {
                                fixedJson = fixedJson + "}";
                            }
                            try
                            {
                                messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(fixedJson) ?? null;
                                if (messageObj != null)
                                {
                                    Logger.LogInfo("通过修复type字段（111200->11120）成功解析");
                                    cleanMessage = fixedJson;
                                    isFixed = true;
                                }
                            }
                            catch
                            {
                                // 继续尝试其他策略
                            }
                        }
                        
                        // 策略3: 修复被截断的字符串字段（如msg字段）
                        if (!isFixed && ex.Message.Contains("Unterminated string"))
                        {
                            try
                            {
                                Logger.LogWarning("检测到未闭合的字符串，尝试修复");
                                
                                // 找到最后一个冒号后的引号位置（这应该是字符串字段的开始）
                                int lastColon = cleanMessage.LastIndexOf(':');
                                int lastQuote = cleanMessage.LastIndexOf('"');
                                
                                if (lastColon > 0 && lastQuote > lastColon)
                                {
                                    // 检查从最后一个引号到末尾是否有闭合引号
                                    string afterLastQuote = cleanMessage.Substring(lastQuote + 1);
                                    if (!afterLastQuote.Contains("\"") || afterLastQuote.IndexOf("\"") > afterLastQuote.Length - 10)
                                    {
                                        // 字符串未闭合，需要修复
                                        string fixedJson = cleanMessage;
                                        
                                        // 转义字符串中的特殊字符（如果还没有转义）
                                        // 注意：不要重复转义已经转义的字符
                                        string unclosedString = afterLastQuote;
                                        
                                        // 在字符串末尾添加闭合引号
                                        // 找到字符串开始的位置（最后一个引号）
                                        int stringStart = lastQuote;
                                        
                                        // 在消息末尾添加闭合引号
                                        if (!fixedJson.EndsWith("\""))
                                        {
                                            fixedJson = fixedJson + "\"";
                                        }
                                        
                                        // 补全JSON结构
                                        if (!fixedJson.EndsWith("}"))
                                        {
                                            // 计算需要补全的括号数量
                                            int openBraces = fixedJson.Count(c => c == '{');
                                            int closeBraces = fixedJson.Count(c => c == '}');
                                            int openBrackets = fixedJson.Count(c => c == '[');
                                            int closeBrackets = fixedJson.Count(c => c == ']');
                                            
                                            // 先补全数组
                                            while (closeBrackets < openBrackets)
                                            {
                                                fixedJson += "]";
                                                closeBrackets++;
                                            }
                                            // 再补全对象
                                            while (closeBraces < openBraces)
                                            {
                                                fixedJson += "}";
                                                closeBraces++;
                                            }
                                        }
                                        
                                        try
                                        {
                                            messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(fixedJson) ?? null;
                                            if (messageObj != null)
                                            {
                                                Logger.LogInfo("通过修复被截断的字符串字段成功解析");
                                                cleanMessage = fixedJson;
                                                isFixed = true;
                                            }
                                        }
                                        catch (Exception fixEx)
                                        {
                                            Logger.LogWarning($"修复字符串字段后仍解析失败: {fixEx.Message}");
                                            // 继续尝试其他策略
                                        }
                                    }
                                }
                            }
                            catch (Exception fixEx)
                            {
                                Logger.LogWarning($"修复被截断字符串字段时出错: {fixEx.Message}");
                                // 继续尝试其他策略
                            }
                        }
                        
                        // 策略4: 如果消息不完整，尝试补全
                        if (!isFixed && cleanMessage.StartsWith("{") && !cleanMessage.EndsWith("}"))
                        {
                            // 尝试找到最后一个逗号或冒号，然后补全
                            int lastComma = cleanMessage.LastIndexOf(',');
                            int lastColon = cleanMessage.LastIndexOf(':');
                            int lastQuote = cleanMessage.LastIndexOf('"');
                            
                            if (lastQuote > 0)
                            {
                                // 如果最后是字符串值，尝试补全
                                string fixedJson = cleanMessage;
                                
                                // 检查最后一个引号是否是字符串的开始（前面有冒号）
                                int quoteBeforeColon = cleanMessage.LastIndexOf(':', lastQuote);
                                if (quoteBeforeColon > 0 && quoteBeforeColon < lastQuote)
                                {
                                    // 这是一个字符串字段，需要闭合引号
                                    if (!cleanMessage.Substring(lastQuote + 1).Contains("\""))
                                    {
                                        fixedJson = cleanMessage + "\"";
                                    }
                                }
                                
                                // 补全JSON结构
                                if (!fixedJson.EndsWith("}"))
                                {
                                    // 计算需要补全的括号数量
                                    int openBraces = fixedJson.Count(c => c == '{');
                                    int closeBraces = fixedJson.Count(c => c == '}');
                                    int openBrackets = fixedJson.Count(c => c == '[');
                                    int closeBrackets = fixedJson.Count(c => c == ']');
                                    
                                    // 先补全数组
                                    while (closeBrackets < openBrackets)
                                    {
                                        fixedJson += "]";
                                        closeBrackets++;
                                    }
                                    // 再补全对象
                                    while (closeBraces < openBraces)
                                    {
                                        fixedJson += "}";
                                        closeBraces++;
                                    }
                                }
                                
                                try
                                {
                                    messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(fixedJson) ?? null;
                                    if (messageObj != null)
                                    {
                                        Logger.LogInfo("通过补全JSON成功解析");
                                        cleanMessage = fixedJson;
                                        isFixed = true;
                                    }
                                }
                                catch
                                {
                                    // 继续尝试其他策略
                                }
                            }
                        }
                        
                        if (!isFixed)
                        {
                            Logger.LogError($"无法解析消息，已尝试多种修复策略");
                            Logger.LogError($"原始消息: {message}");
                            Logger.LogError($"清理后的消息: {cleanMessage}");
                            Logger.LogError($"原始消息长度: {message?.Length ?? 0}, 清理后长度: {cleanMessage.Length}");
                            // 即使解析失败，也尝试提取基本信息
                            if (cleanMessage.Contains("\"type\":"))
                            {
                                // 尝试提取type值
                                var typeMatch = System.Text.RegularExpressions.Regex.Match(cleanMessage, @"""type"":(\d+)");
                                if (typeMatch.Success)
                                {
                                    Logger.LogWarning($"从失败的消息中提取到type: {typeMatch.Groups[1].Value}");
                                }
                            }
                            return;
                        }
                    }

                    if (messageObj == null)
                    {
                        return;
                    }

                    // 获取消息类型
                    int messageType = 0;
                    if (messageObj.type != null)
                    {
                        int.TryParse(messageObj.type.ToString(), out messageType);
                    }

                    // 记录所有消息类型，用于调试
                    Logger.LogInfo($"收到消息类型: {messageType}, 消息内容: {message}");

                    // 根据实际日志，消息类型 1112 表示账号信息回调
                    // 数据格式为: {"data":{"account":"...","avatar":"...","nickname":"...","wxid":"..."},"type":1112}
                    // 判断是否获取到账号数据的标准：data中有account（或wxid）和nickname
                    if (messageType == 1112)
                    {
                        // 解析账号信息
                        // 数据格式为: {"data":{"account":"...","avatar":"...","nickname":"...","wxid":"..."},"type":1112}
                        // data字段是对象，包含account、avatar、nickname、wxid等字段
                        dynamic? loginInfo = null;
                        
                        if (messageObj?.data != null)
                        {
                            // 如果data是字符串，需要解析
                            string dataJson = messageObj.data?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(dataJson) && dataJson.TrimStart().StartsWith("{"))
                            {
                                try
                                {
                                    loginInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(dataJson) ?? null;
                                }
                                catch
                                {
                                    // 如果解析失败，直接使用data对象
                                    loginInfo = messageObj.data;
                                }
                            }
                            else
                            {
                                // data已经是对象，直接使用
                                loginInfo = messageObj.data;
                            }
                        }

                        if (loginInfo != null)
                        {
                            var weChatManager = GetWeChatManager();
                int clientId = weChatManager?.ConnectionManager?.ClientId ?? 0;
                            if (loginInfo.clientId != null)
                            {
                                int.TryParse(loginInfo.clientId.ToString(), out clientId);
                            }
                            // 如果loginInfo中有pid，也可以使用
                            if (loginInfo.pid != null)
                            {
                                int.TryParse(loginInfo.pid.ToString(), out clientId);
                            }

                            // 尝试多种方式获取wxid
                            string wxid = loginInfo.wxid?.ToString() ?? "";
                            if (string.IsNullOrEmpty(wxid))
                            {
                                wxid = loginInfo.wxId?.ToString() ?? "";  // 尝试wxId（驼峰命名）
                            }
                            if (string.IsNullOrEmpty(wxid))
                            {
                                wxid = loginInfo.WxId?.ToString() ?? "";  // 尝试WxId（首字母大写）
                            }
                            
                            // 尝试多种方式获取nickname
                            string nickname = loginInfo.nickname?.ToString() ?? "";
                            if (string.IsNullOrEmpty(nickname))
                            {
                                nickname = loginInfo.nickName?.ToString() ?? "";  // 尝试nickName（驼峰命名）
                            }
                            if (string.IsNullOrEmpty(nickname))
                            {
                                nickname = loginInfo.NickName?.ToString() ?? "";  // 尝试NickName（首字母大写）
                            }
                            
                            // 尝试多种方式获取avatar
                            string avatar = loginInfo.avatar?.ToString() ?? "";
                            if (string.IsNullOrEmpty(avatar))
                            {
                                avatar = loginInfo.Avatar?.ToString() ?? "";  // 尝试Avatar（首字母大写）
                            }
                            
                            string account = loginInfo.account?.ToString() ?? wxid;
                            if (string.IsNullOrEmpty(account))
                            {
                                account = loginInfo.Account?.ToString() ?? wxid;  // 尝试Account（首字母大写）
                            }
                            
                            // 提取其他字段
                            string deviceId = loginInfo.device_id?.ToString() ?? "";
                            if (string.IsNullOrEmpty(deviceId))
                            {
                                deviceId = loginInfo.deviceId?.ToString() ?? "";
                            }
                            if (string.IsNullOrEmpty(deviceId))
                            {
                                deviceId = loginInfo.DeviceId?.ToString() ?? "";
                            }
                            
                            string phone = loginInfo.phone?.ToString() ?? "";
                            if (string.IsNullOrEmpty(phone))
                            {
                                phone = loginInfo.Phone?.ToString() ?? "";
                            }
                            
                            string wxUserDir = loginInfo.wx_user_dir?.ToString() ?? "";
                            if (string.IsNullOrEmpty(wxUserDir))
                            {
                                wxUserDir = loginInfo.wxUserDir?.ToString() ?? "";
                            }
                            if (string.IsNullOrEmpty(wxUserDir))
                            {
                                wxUserDir = loginInfo.WxUserDir?.ToString() ?? "";
                            }
                            
                            int unreadMsgCount = 0;
                            if (loginInfo.unread_msg_count != null)
                            {
                                int.TryParse(loginInfo.unread_msg_count.ToString(), out unreadMsgCount);
                            }
                            else if (loginInfo.unreadMsgCount != null)
                            {
                                int.TryParse(loginInfo.unreadMsgCount.ToString(), out unreadMsgCount);
                            }
                            else if (loginInfo.UnreadMsgCount != null)
                            {
                                int.TryParse(loginInfo.UnreadMsgCount.ToString(), out unreadMsgCount);
                            }
                            
                            int isFakeDeviceId = 0;
                            if (loginInfo.is_fake_device_id != null)
                            {
                                int.TryParse(loginInfo.is_fake_device_id.ToString(), out isFakeDeviceId);
                            }
                            else if (loginInfo.isFakeDeviceId != null)
                            {
                                int.TryParse(loginInfo.isFakeDeviceId.ToString(), out isFakeDeviceId);
                            }
                            else if (loginInfo.IsFakeDeviceId != null)
                            {
                                int.TryParse(loginInfo.IsFakeDeviceId.ToString(), out isFakeDeviceId);
                            }
                            
                            int pid = 0;
                            if (loginInfo.pid != null)
                            {
                                int.TryParse(loginInfo.pid.ToString(), out pid);
                            }
                            else if (loginInfo.Pid != null)
                            {
                                int.TryParse(loginInfo.Pid.ToString(), out pid);
                            }
                            
                            // 如果wxid为空，记录警告但不使用clientId作为fallback
                            // wxid应该是真正的微信ID，而不是进程ID（纯数字）
                            if (string.IsNullOrEmpty(wxid))
                            {
                                Logger.LogWarning($"解析登录信息时wxid为空，等待真正的wxid（不使用进程ID {clientId} 作为fallback）");
                            }
                            // 如果wxid是进程ID（纯数字），也记录警告
                            else if (IsProcessId(wxid))
                            {
                                Logger.LogWarning($"解析到的wxid是进程ID（{wxid}），这不是真正的微信ID，等待真正的wxid");
                                wxid = string.Empty; // 清空，等待真正的wxid
                            }
                            
                            Logger.LogInfo($"解析登录信息: wxid={(!string.IsNullOrEmpty(wxid) ? wxid : "空")}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(avatar) ? "有头像" : "无头像")}, account={account}, deviceId={deviceId}, phone={phone}, pid={pid}");

                            // 如果wxid为空，不创建账号信息（等待真正的wxid）
                            if (string.IsNullOrEmpty(wxid))
                            {
                                Logger.LogWarning("wxid为空，跳过账号信息更新，等待1112回调提供真正的wxid");
                                return;
                            }

                            // 更新账号信息
                            AccountInfo? accountInfo = null;
                            if (_accountList != null)
                            {
                            foreach (var acc in _accountList)
                            {
                                    // 只匹配真正的wxid（不是进程ID），不匹配clientId
                                    if (acc.WeChatId == wxid || (IsRealWeChatId(acc.WeChatId) && IsRealWeChatId(wxid)))
                                {
                                    accountInfo = acc;
                                    break;
                                }
                            }

                            if (accountInfo == null)
                            {
                                accountInfo = new AccountInfo
                                {
                                    WeChatId = wxid,
                                    BoundAccount = account
                                };
                                _accountList.Add(accountInfo);
                                }
                            }

                            // 更新账号信息
                            if (accountInfo != null)
                            {
                            accountInfo.WeChatId = wxid;
                            accountInfo.BoundAccount = account;
                            
                            if (!string.IsNullOrEmpty(nickname))
                            {
                                accountInfo.NickName = nickname;
                                }
                            
                            if (!string.IsNullOrEmpty(avatar))
                            {
                                accountInfo.Avatar = avatar;
                            }
                            
                            if (!string.IsNullOrEmpty(deviceId))
                            {
                                accountInfo.DeviceId = deviceId;
                            }
                            
                            if (!string.IsNullOrEmpty(phone))
                            {
                                accountInfo.Phone = phone;
                            }
                            
                            if (!string.IsNullOrEmpty(wxUserDir))
                            {
                                accountInfo.WxUserDir = wxUserDir;
                            }
                            
                            accountInfo.UnreadMsgCount = unreadMsgCount;
                            accountInfo.IsFakeDeviceId = isFakeDeviceId;
                            accountInfo.Pid = pid;
                            }

                            Logger.LogInfo($"收到登录回调: wxid={wxid}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(avatar) ? "有头像" : "无头像")}, account={account}");
                            AddLog($"收到登录回调: wxid={wxid}, nickname={nickname}", "SUCCESS");
                            
                            // 更新UI显示
                            UpdateAccountInfoDisplay();
                            
                            // 实时同步我的信息到服务器（发送所有字段）
                            // 延迟一点时间，确保WebSocket连接稳定
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                SyncMyInfoToServer(accountInfo);
                            });
                            
                            // 检查JSON消息中的账号信息字段是否完整（account、nickname等）
                            if (IsAccountInfoCompleteFromJson(loginInfo))
                            {
                                // 如果JSON消息中的账号信息完整，停止定时器
                                StopTimersAfterAccountInfoReceived();
                            }
                            
                            // 再次更新UI显示，确保头像显示
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateAccountInfoDisplay();
                                });
                            });
                            
                            // 不自动同步其他数据（好友、标签、朋友圈等）
                            // 这些数据由 app 端点击时，通过服务端请求获取
                        }
                    }
                    // 消息类型 11126 表示好友列表回调
                    else if (messageType == 11126)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _contactSyncService?.ProcessContactsCallback(dataJson);
                        }
                    }
                    // 消息类型 11132 表示文本消息
                    else if (messageType == 11132)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _chatMessageSyncService?.ProcessChatMessageCallback(messageType, dataJson);
                        }
                    }
                    // 消息类型 11144 表示语音消息
                    else if (messageType == 11144)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _chatMessageSyncService?.ProcessChatMessageCallback(messageType, dataJson);
                        }
                    }
                    // 消息类型 11241 表示朋友圈回调
                    else if (messageType == 11241)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _momentsSyncService?.ProcessMomentsCallback(dataJson);
                        }
                    }
                    // 消息类型 11238 表示标签列表回调
                    else if (messageType == 11238)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _tagSyncService?.ProcessTagsCallback(dataJson);
                        }
                    }
                    // 消息类型 5 表示公众号推送消息
                    else if (messageType == 5)
                    {
                        string dataJson = messageObj.data?.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(dataJson))
                        {
                            _officialAccountSyncService?.ProcessOfficialAccountCallback(dataJson);
                        }
                    }
                    // 也检查是否是直接的账号信息消息（兼容其他格式）
                    // 尝试多种字段名（支持驼峰命名和首字母大写）
                    else if (messageObj.nickname != null || messageObj.avatar != null || messageObj.wxid != null ||
                             messageObj.nickName != null || messageObj.NickName != null ||
                             messageObj.Avatar != null || messageObj.wxId != null || messageObj.WxId != null)
                    {
                        var weChatManager = GetWeChatManager();
                int clientId = weChatManager?.ConnectionManager?.ClientId ?? 0;
                        
                        // 尝试多种方式获取wxid
                        string weChatId = messageObj.wxid?.ToString() ?? 
                                         messageObj.wxId?.ToString() ?? 
                                         messageObj.WxId?.ToString() ?? 
                                         clientId.ToString();
                        
                        // 更新账号信息
                        AccountInfo? account = null;
                        if (_accountList != null)
                        {
                        foreach (var acc in _accountList)
                        {
                            if (acc.WeChatId == weChatId || acc.WeChatId == clientId.ToString())
                            {
                                account = acc;
                                break;
                            }
                        }
                        
                        if (account == null)
                        {
                            account = new AccountInfo
                            {
                                WeChatId = weChatId,
                                BoundAccount = weChatId
                            };
                            _accountList.Add(account);
                            }
                        }
                        
                        // 更新昵称（尝试多种字段名）
                        if (account != null)
                        {
                        if (messageObj.nickname != null)
                        {
                            account.NickName = messageObj.nickname.ToString();
                            }
                            else if (messageObj.nickName != null)
                            {
                                account.NickName = messageObj.nickName.ToString();
                            }
                            else if (messageObj.NickName != null)
                            {
                                account.NickName = messageObj.NickName.ToString();
                            }
                        }
                        
                        // 更新头像（尝试多种字段名）
                        if (account != null && messageObj.avatar != null)
                        {
                            account!.Avatar = messageObj.avatar?.ToString() ?? string.Empty;
                        }
                        else if (account != null && messageObj.Avatar != null)
                        {
                            account!.Avatar = messageObj.Avatar?.ToString() ?? string.Empty;
                        }
                        
                        // 更新wxid（尝试多种字段名）
                        if (account != null)
                        {
                        if (messageObj.wxid != null)
                        {
                            account.WeChatId = messageObj.wxid.ToString();
                            account.BoundAccount = messageObj.wxid.ToString();
                            }
                            else if (messageObj.wxId != null)
                            {
                                account.WeChatId = messageObj.wxId.ToString();
                                account.BoundAccount = messageObj.wxId.ToString();
                            }
                            else if (messageObj.WxId != null)
                            {
                                account.WeChatId = messageObj.WxId.ToString();
                                account.BoundAccount = messageObj.WxId.ToString();
                            }
                        }
                        
                        if (account != null)
                        {
                            Logger.LogInfo($"从消息中更新账号信息: wxid={account.WeChatId}, nickname={account.NickName}, avatar={(!string.IsNullOrEmpty(account.Avatar) ? "有头像" : "无头像")}, account={account.BoundAccount}");
                        
                        // 更新UI显示
                        UpdateAccountInfoDisplay();
                            
                            // 检查账号信息是否完整（account、nickname等字段）
                            bool hasAccount = !string.IsNullOrEmpty(account.BoundAccount) || !string.IsNullOrEmpty(account.WeChatId);
                            bool hasNickname = !string.IsNullOrEmpty(account.NickName);
                            if (hasAccount && hasNickname)
                            {
                                // 如果账号信息完整（有account和nickname），停止定时器
                                StopTimersAfterAccountInfoReceived();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"处理MyWeChat消息失败: {ex.Message}", ex);
                    AddLog($"处理MyWeChat消息失败: {ex.Message}", "ERROR");
                }
            });
        }




        /// <summary>
        /// 同步我的信息到服务器（发送所有字段）
        /// </summary>
        private void SyncMyInfoToServer(AccountInfo? accountInfo)
        {
            try
            {
                if (accountInfo == null)
                {
                    Logger.LogWarning("账号信息为空，无法同步到服务器");
                    return;
                }

                // 只同步有真正wxid的账号信息
                if (string.IsNullOrEmpty(accountInfo.WeChatId) || IsProcessId(accountInfo.WeChatId))
                {
                    Logger.LogWarning($"账号信息wxid无效（{accountInfo.WeChatId}），无法同步到服务器");
                    return;
                }

                // 检查WebSocket是否已连接
                if (_webSocketService == null || !_webSocketService.IsConnected)
                {
                    Logger.LogWarning("WebSocket未连接，无法同步账号信息到服务器，将在连接后重试");
                    // 如果WebSocket未连接，延迟重试
                    Task.Delay(1000).ContinueWith(_ =>
                    {
                        if (_webSocketService != null && _webSocketService.IsConnected)
                        {
                            Logger.LogInfo("WebSocket已连接，重试同步账号信息");
                            SyncMyInfoToServer(accountInfo);
                        }
                    });
                    return;
                }

                Logger.LogInfo($"开始同步我的信息到服务器: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");

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
                        pid = accountInfo.Pid,
                        clientId = _weChatManager?.ConnectionManager?.ClientId ?? 0
                    }
                };

                _ = _webSocketService?.SendMessageAsync(syncData);

                Logger.LogInfo("我的信息同步完成（已发送所有字段）");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步我的信息到服务器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// WebSocket消息接收事件处理
        /// </summary>
        private void OnWebSocketMessageReceived(object? sender, string message)
        {
            // 使用BeginInvoke异步调用，避免阻塞UI线程
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Logger.LogInfo($"收到WebSocket消息: {message}");
                    AddLog($"收到WebSocket消息: {message}", "INFO");
                    
                    var messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(message);
                    string messageType = messageObj?.type?.ToString() ?? "";
                    
                    Logger.LogInfo($"消息类型: {messageType}");
                    
                    if (messageType == "command")
                    {
                        // 处理App端发送的命令
                        string commandJson = Newtonsoft.Json.JsonConvert.SerializeObject(messageObj);
                        bool result = _commandService?.ProcessCommand(commandJson) ?? false;
                        AddLog($"命令执行结果: {(result ? "成功" : "失败")}", result ? "SUCCESS" : "ERROR");
                    }
                    else if (messageType == "sync_my_info")
                    {
                        // 处理我的信息同步
                        Logger.LogInfo("处理我的信息同步");
                        try
                        {
                            dynamic? data = messageObj?.data;
                            if (data != null)
                            {
                                Logger.LogInfo($"收到我的信息同步: {Newtonsoft.Json.JsonConvert.SerializeObject(data)}");
                                
                                string wxid = data.wxid?.ToString() ?? "";
                                string nickname = data.nickname?.ToString() ?? "";
                                string avatar = data.avatar?.ToString() ?? "";
                                string account = data.account?.ToString() ?? "";
                                int clientId = 0;
                                
                                if (data.clientId != null)
                                {
                                    int.TryParse(data.clientId.ToString(), out clientId);
                                }
                                
                                // 更新账号信息
                                AccountInfo? accountInfo = null;
                                if (_accountList != null)
                                {
                                foreach (var acc in _accountList)
                                {
                                    if (acc.WeChatId == wxid || 
                                        acc.WeChatId == clientId.ToString() ||
                                        (!string.IsNullOrEmpty(wxid) && acc.WeChatId == wxid))
                                    {
                                        accountInfo = acc;
                                        break;
                                    }
                                }
                                    
                                    // 如果wxid为空，不创建账号信息（等待真正的wxid）
                                    if (string.IsNullOrEmpty(wxid))
                                    {
                                        Logger.LogWarning("从好友列表回调中获取的wxid为空，跳过账号信息更新");
                                        return;
                                }
                                
                                if (accountInfo == null)
                                {
                                        // 创建新账号信息（只使用真正的wxid，不使用clientId）
                                    accountInfo = new AccountInfo
                                    {
                                            WeChatId = wxid,
                                        BoundAccount = !string.IsNullOrEmpty(account) ? account : wxid
                                    };
                                    _accountList.Add(accountInfo);
                                    }
                                }
                                
                                // 更新账号信息
                                if (accountInfo != null)
                                {
                                if (!string.IsNullOrEmpty(nickname))
                                {
                                    accountInfo.NickName = nickname;
                                }
                                
                                if (!string.IsNullOrEmpty(avatar))
                                {
                                    accountInfo.Avatar = avatar;
                                }
                                
                                    // 确保WeChatId是真正的wxid（不是进程ID）
                                    if (IsRealWeChatId(wxid))
                                {
                                    accountInfo.WeChatId = wxid;
                                }
                                
                                if (!string.IsNullOrEmpty(account))
                                {
                                    accountInfo.BoundAccount = account;
                                }
                                
                                // 更新UI显示
                                UpdateAccountInfoDisplay();
                                
                                Logger.LogInfo($"账号信息已更新: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"处理我的信息同步失败: {ex.Message}", ex);
                            AddLog($"处理我的信息同步失败: {ex.Message}", "ERROR");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"处理WebSocket消息失败: {ex.Message}", ex);
                    AddLog($"处理消息失败: {ex.Message}", "ERROR");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// 添加日志（统一使用Logger输出到Logs目录）
        /// 简化设计：直接调用Logger，不再需要AddLog包装
        /// </summary>
        private void AddLog(string message, string level = "INFO")
        {
            // 直接调用Logger，简化设计
            switch (level.ToUpper())
            {
                case "ERROR":
                    Logger.LogError(message);
                    break;
                case "WARN":
                case "WARNING":
                    Logger.LogWarning(message);
                    break;
                case "SUCCESS":
                    Logger.LogSuccess(message);
                    break;
                case "INFO":
                default:
                    Logger.LogInfo(message);
                    break;
            }
        }

        // UI日志显示已移除，相关按钮事件处理已删除

        #region 按钮事件处理

        /// <summary>
        /// 登录微信按钮点击事件
        /// </summary>
        private void BtnLoginWeChat_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("========== 开始登录微信 ==========", "INFO");
                AddLog($"当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", "INFO");
                AddLog($"应用程序路径: {AppDomain.CurrentDomain.BaseDirectory}", "INFO");
                AddLog($"是否管理员权限: {IsAdministrator()}", IsAdministrator() ? "SUCCESS" : "WARN");
                
                // 检查微信管理器是否初始化
                if (_weChatManager == null)
                {
                    string errorMsg = "微信管理器未初始化，请检查初始化过程";
                    Logger.LogError(errorMsg);
                    AddLog($"错误: {errorMsg}", "ERROR");
                    AddLog("提示: 连接管理器在应用程序启动时应该已经初始化", "WARN");
                    return;
                }

                AddLog("步骤1: 检查连接管理器状态...", "INFO");
                AddLog($"微信管理器已创建: {_weChatManager != null}", _weChatManager != null ? "SUCCESS" : "ERROR");
                AddLog($"当前连接状态: {(_weChatManager?.IsConnected == true ? "已连接" : "未连接")}", _weChatManager?.IsConnected == true ? "SUCCESS" : "WARN");
                AddLog($"当前微信版本: {_weChatManager?.ConnectionManager?.WeChatVersion ?? "未知"}", "INFO");

                AddLog("步骤2: 初始化连接管理器...", "INFO");
                bool initResult = _weChatManager?.Initialize() ?? false;
                AddLog($"初始化结果: {(initResult ? "成功" : "失败")}", initResult ? "SUCCESS" : "ERROR");
                
                if (!initResult)
                {
                    AddLog("错误: 连接管理器初始化失败", "ERROR");
                    AddLog("可能的原因:", "ERROR");
                    AddLog("  1. 微信未安装或未正确安装", "ERROR");
                    AddLog("  2. 无法检测到微信版本", "ERROR");
                    AddLog("  3. DLL文件不存在或路径不正确", "ERROR");
                    AddLog("  4. DLL版本与微信版本不匹配", "ERROR");
                    AddLog($"当前检测到的微信版本: {_weChatManager?.ConnectionManager?.WeChatVersion ?? "未检测到"}", "ERROR");
                    
                    // 检查DLL目录
                    string dllsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DLLs");
                    AddLog($"DLLs目录路径: {dllsPath}", "INFO");
                    AddLog($"DLLs目录是否存在: {System.IO.Directory.Exists(dllsPath)}", System.IO.Directory.Exists(dllsPath) ? "SUCCESS" : "ERROR");
                    
                    if (System.IO.Directory.Exists(dllsPath))
                    {
                        string[] subDirs = System.IO.Directory.GetDirectories(dllsPath);
                        AddLog($"找到的版本目录数量: {subDirs.Length}", "INFO");
                        foreach (string dir in subDirs)
                        {
                            string dirName = System.IO.Path.GetFileName(dir);
                            AddLog($"  - 版本目录: {dirName}", "INFO");
                        }
                    }
                    
                    return;
                }

                AddLog($"步骤3: 微信管理器初始化成功，微信版本: {_weChatManager?.ConnectionManager?.WeChatVersion ?? "未检测到"}", "SUCCESS");
                AddLog("步骤4: 正在打开微信...", "INFO");
                
                if (_weChatManager == null)
                {
                    AddLog("微信管理器未初始化", "ERROR");
                    return;
                }
                bool result = _weChatManager?.Connect() ?? false;
                
                if (result)
                {
                    AddLog("========== 微信登录成功 ==========", "SUCCESS");
                    AddLog($"微信版本: {_weChatManager?.ConnectionManager?.WeChatVersion ?? "未知"}", "SUCCESS");
                    AddLog($"客户端ID: {_weChatManager?.ConnectionManager?.ClientId ?? 0}", "SUCCESS");
                    AddLog($"连接状态: {(_weChatManager?.IsConnected == true ? "已连接" : "未连接")}", _weChatManager?.IsConnected == true ? "SUCCESS" : "WARN");
                    
                    // 登录成功后，自动更新账号列表
                    UpdateAccountList();
                }
                else
                {
                    AddLog("========== 微信登录失败 ==========", "ERROR");
                    AddLog("可能的原因:", "ERROR");
                    AddLog("  1. 微信未安装或安装路径不正确", "ERROR");
                    AddLog("  2. DLL文件不存在或版本不匹配", "ERROR");
                    AddLog("  3. 未以管理员权限运行（需要管理员权限）", "ERROR");
                    AddLog("  4. 微信进程已运行，需要先关闭", "ERROR");
                    AddLog("  5. 微信版本不支持", "ERROR");
                    AddLog($"当前微信版本: {_weChatManager?.ConnectionManager?.WeChatVersion ?? "未知"}", "ERROR");
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"登录微信异常: {ex.Message}";
                string stackTrace = ex.StackTrace ?? "";
                string innerException = ex.InnerException != null ? $"内部异常: {ex.InnerException.Message}" : "";
                
                Logger.LogError($"{errorMsg}\n堆栈跟踪: {stackTrace}\n{innerException}", ex);
                AddLog("========== 发生异常 ==========", "ERROR");
                AddLog($"错误类型: {ex.GetType().Name}", "ERROR");
                AddLog($"错误消息: {errorMsg}", "ERROR");
                if (!string.IsNullOrEmpty(innerException))
                {
                    AddLog(innerException, "ERROR");
                }
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    AddLog($"堆栈跟踪: {stackTrace}", "ERROR");
                }
            }
        }

        /// <summary>
        /// 检查是否以管理员权限运行
        /// </summary>
        private bool IsAdministrator()
        {
            try
            {
                System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                System.Security.Principal.WindowsPrincipal principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 同步好友按钮点击事件
        /// </summary>
        private void BtnSyncContacts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("开始同步好友...", "INFO");
                bool result = _contactSyncService?.SyncContacts() ?? false;
                if (result)
                {
                    AddLog("好友同步命令已发送", "SUCCESS");
                }
                else
                {
                    AddLog("好友同步命令发送失败", "ERROR");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步好友失败: {ex.Message}", ex);
                AddLog($"同步好友失败: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// 同步朋友圈按钮点击事件
        /// </summary>
        private void BtnSyncMoments_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("开始同步朋友圈...", "INFO");
                bool result = _momentsSyncService?.SyncMoments() ?? false;
                if (result)
                {
                    AddLog("朋友圈同步命令已发送", "SUCCESS");
                }
                else
                {
                    AddLog("朋友圈同步命令发送失败", "ERROR");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步朋友圈失败: {ex.Message}", ex);
                AddLog($"同步朋友圈失败: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// 同步标签按钮点击事件
        /// </summary>
        private void BtnSyncTags_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddLog("开始同步标签...", "INFO");
                bool result = _tagSyncService?.SyncTags() ?? false;
                if (result)
                {
                    AddLog("标签同步命令已发送", "SUCCESS");
                }
                else
                {
                    AddLog("标签同步命令发送失败", "ERROR");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步标签失败: {ex.Message}", ex);
                AddLog($"同步标签失败: {ex.Message}", "ERROR");
            }
        }

        #endregion

        /// <summary>
        /// 初始化窗口关闭处理器
        /// </summary>
        private void InitializeCloseHandler()
            {
            // 初始化系统托盘服务
            _trayIconService = new TrayIconService();
            _trayIconService.Initialize(this);

            // 初始化关闭进度遮罩辅助类
            _closingProgressHelper = new ClosingProgressHelper(
                ClosingOverlayCanvas,
                ClosingOverlayBorder,
                ClosingProgressArc,
                ClosingProgressText,
                ClosingStatusText,
                "主窗口"
            );

            var service = WeChatInitializationService.Instance;
            var config = new WindowCloseHandler.CleanupConfig
            {
                WeChatManager = service.WeChatManager,
                WebSocketService = _webSocketService,
                StopAllTimersCallback = StopAllTimers,
                UnsubscribeEventsCallback = UnsubscribeEvents,
                CleanupSyncServicesCallback = CleanupSyncServices,
                ClearAccountListCallback = ClearAccountList,
                UpdateProgressCallback = (progress, status) => _closingProgressHelper?.UpdateClosingProgress(progress, status),
                ShowProgressOverlayCallback = (show) => _closingProgressHelper?.ShowProgressOverlay(show)
            };

            _closeHandler = new WindowCloseHandler(this, config);
            
            // 设置最小化到托盘的回调
            _closeHandler.MinimizeToTrayCallback = () =>
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            };
        }

        // 注意：ShowProgressOverlay、UpdateClosingProgress、UpdateClosingProgressRing 方法已移至 ClosingProgressHelper 类
        // 这些方法现在通过 _closingProgressHelper 调用

        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeEvents()
                        {
                            // WeChatManager的事件订阅在Dispose时自动清理
                            
                            // 取消WebSocket服务事件订阅
                            if (_webSocketService != null)
                            {
                                _webSocketService.OnMessageReceived -= OnWebSocketMessageReceived;
                                _webSocketService.OnConnectionStateChanged -= OnWebSocketConnectionStateChanged;
                            }
        }

        /// <summary>
        /// 清理同步服务
        /// </summary>
        private void CleanupSyncServices()
                        {
                            // 清理服务对象（如果实现了IDisposable，会自动释放）
                            _contactSyncService = null;
                            _momentsSyncService = null;
                            _tagSyncService = null;
                            _chatMessageSyncService = null;
                            _officialAccountSyncService = null;
                            _commandService = null;
        }

        /// <summary>
        /// 清空账号列表
        /// </summary>
        private void ClearAccountList()
                        {
                            if (_accountList != null)
                            {
                                _accountList.Clear();
                            }
        }

        /// <summary>
        /// 停止所有定时器
        /// </summary>
        private void StopAllTimers()
        {
                    try
                    {
                // 停止微信进程检测定时器
                if (_weChatManager != null)
                    {
                    _weChatManager.StopProcessCheckTimer();
                    Logger.LogInfo("已停止微信进程检测定时器");
                }

                // 停止账号信息获取定时器
                StopAccountInfoFetchTimer();
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止定时器失败: {ex.Message}", ex);
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_closeHandler != null)
            {
                _closeHandler.HandleClosing(e);
            }
            else
            {
                // 如果关闭处理器未初始化，直接关闭
                base.OnClosing(e);
            }
        }
        

        /// <summary>
        /// 窗口已关闭事件（窗口关闭后执行）
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // 最终清理（确保所有引用都被清空）
                Logger.LogInfo("========== 最终清理资源 ==========");
                
                // 清空所有服务引用
                _weChatManager?.Dispose();
                _weChatManager = null;
                _webSocketService = null;
                _contactSyncService = null;
                _momentsSyncService = null;
                _tagSyncService = null;
                _chatMessageSyncService = null;
                _officialAccountSyncService = null;
                _commandService = null;
                
                // 清空账号列表（双重保险）
                if (_accountList != null)
                {
                    _accountList.Clear();
                    _accountList = null;
                }
                
                // 释放系统托盘服务
                _trayIconService?.Dispose();
                _trayIconService = null;
                
                Logger.LogInfo("窗口已关闭，所有资源已清理");
                Logger.LogInfo("========== 资源清理完成 ==========");
            }
            catch (Exception ex)
            {
                Logger.LogError($"窗口关闭后清理时出错: {ex.Message}", ex);
            }
            finally
            {
            base.OnClosed(e);
            }
        }
    }
}

