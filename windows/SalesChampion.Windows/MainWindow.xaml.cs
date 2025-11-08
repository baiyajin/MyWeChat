using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SalesChampion.Windows.Core.Connection;
using SalesChampion.Windows.Models;
using SalesChampion.Windows.Services;
using SalesChampion.Windows.Services.WebSocket;
using SalesChampion.Windows.Utils;

namespace SalesChampion.Windows
{
    /// <summary>
    /// 主窗口
    /// </summary>
    public partial class MainWindow : Window
    {
        private WeChatConnectionManager? _connectionManager;
        private WebSocketService? _webSocketService;
        private ContactSyncService? _contactSyncService;
        private MomentsSyncService? _momentsSyncService;
        private TagSyncService? _tagSyncService;
        private ChatMessageSyncService? _chatMessageSyncService;
        private CommandService? _commandService;
        private ObservableCollection<AccountInfo>? _accountList;
        
        // 保存事件订阅的委托引用，以便在关闭时取消订阅
        private Action<string>? _loggerEventHandler;
        
        // 定时器：检测微信进程
        private DispatcherTimer? _weChatProcessCheckTimer;
        
        // 定时器：获取微信账号信息
        private DispatcherTimer? _accountInfoFetchTimer;
        
        // 标记是否已连接微信
        private bool _isWeChatConnected = false;
        
        // 标记是否正在关闭
        private bool _isClosing = false;
        
        // 日志颜色Brush缓存，避免频繁创建
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0, 123, 255));
        private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69));
        private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(33, 37, 41));
        private static readonly Brush TimeBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));
        
        // 日志文件路径
        private readonly string _logFilePath;

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

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _accountList = new ObservableCollection<AccountInfo>();
            
            // 初始化日志文件路径：MyWeChat/windows.log
            try
            {
                // 获取当前程序所在目录（bin\x86\Debug\net9.0-windows）
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                
                // 向上查找 MyWeChat 目录
                // 路径结构：MyWeChat/windows/SalesChampion.Windows/bin/x86/Debug/net9.0-windows/
                // 需要向上4级才能到 MyWeChat
                DirectoryInfo? dir = new DirectoryInfo(currentDir);
                string? myWeChatDir = null;
                
                // 向上查找，直到找到包含 "MyWeChat" 的目录或到达根目录
                for (int i = 0; i < 10 && dir != null; i++)
                {
                    if (dir.Name.Equals("MyWeChat", StringComparison.OrdinalIgnoreCase))
                    {
                        myWeChatDir = dir.FullName;
                        break;
                    }
                    dir = dir.Parent;
                }
                
                // 如果找不到 MyWeChat 目录，使用当前目录向上4级（假设标准结构）
                if (string.IsNullOrEmpty(myWeChatDir))
                {
                    dir = new DirectoryInfo(currentDir);
                    for (int i = 0; i < 4 && dir != null; i++)
                    {
                        dir = dir.Parent;
                    }
                    myWeChatDir = dir?.FullName ?? currentDir;
                }
                
                _logFilePath = Path.Combine(myWeChatDir, "windows.log");
                Logger.LogInfo($"日志文件路径: {_logFilePath}");
            }
            catch (Exception ex)
            {
                // 如果初始化失败，使用默认路径
                _logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "windows.log");
                Logger.LogError($"初始化日志文件路径失败: {ex.Message}", ex);
            }
            
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
                // 窗口加载完成后再初始化服务
                InitializeServices();
            }
            catch (Exception ex)
            {
                Logger.LogError($"窗口加载时初始化服务失败: {ex.Message}", ex);
                MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 初始化服务
        /// </summary>
        private void InitializeServices()
        {
            try
            {
                // 订阅Logger日志事件，输出到UI（带颜色）
                // 保存委托引用，以便在关闭时取消订阅
                _loggerEventHandler = (message) =>
                {
                    try
                    {
                        // 使用BeginInvoke异步调用，避免阻塞UI线程
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            // 从消息中提取日志级别
                            string level = "INFO";
                            if (message.StartsWith("[ERROR]"))
                            {
                                level = "ERROR";
                                message = message.Substring(8).TrimStart();
                            }
                            else if (message.StartsWith("[WARN]"))
                            {
                                level = "WARN";
                                message = message.Substring(7).TrimStart();
                            }
                            else if (message.StartsWith("[INFO]"))
                            {
                                level = "INFO";
                                message = message.Substring(7).TrimStart();
                            }
                            AddLog(message, level);
                        }), System.Windows.Threading.DispatcherPriority.Normal);
                    }
                    catch
                    {
                        // 忽略Dispatcher调用失败
                    }
                };
                Logger.OnLogMessage += _loggerEventHandler;

                // 延迟初始化连接管理器，避免在UI线程中直接初始化导致崩溃
                Task.Run(() =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AddLog("正在初始化连接管理器...", "INFO");
                        });

                        // 初始化连接管理器
                        _connectionManager = new WeChatConnectionManager();
                        _connectionManager.OnConnectionStateChanged += OnConnectionStateChanged;
                        _connectionManager.OnMessageReceived += OnWeChatMessageReceived;
                        
                        if (!_connectionManager.Initialize())
                        {
                            Logger.LogError("连接管理器初始化失败");
                            Dispatcher.Invoke(() =>
                            {
                                UpdateUI(); // 即使初始化失败，也更新UI显示
                            });
                            return;
                        }
                        
                        // 初始化成功，立即更新UI显示版本号
                        Dispatcher.Invoke(() =>
                        {
                            UpdateUI();
                        });

                        // 初始化WebSocket服务
                        string webSocketUrl = ConfigurationManager.AppSettings["WebSocketUrl"] ?? "ws://localhost:8000/ws";
                        _webSocketService = new WebSocketService(webSocketUrl);
                        _webSocketService.OnMessageReceived += OnWebSocketMessageReceived;
                        _webSocketService.OnConnectionStateChanged += OnWebSocketConnectionStateChanged;

                        // 初始化同步服务
                        _contactSyncService = new ContactSyncService(_connectionManager, _webSocketService);
                        _momentsSyncService = new MomentsSyncService(_connectionManager, _webSocketService);
                        _tagSyncService = new TagSyncService(_connectionManager, _webSocketService);
                        _chatMessageSyncService = new ChatMessageSyncService(_connectionManager, _webSocketService);
                        
                        // 订阅好友列表回调中的账号信息提取事件
                        _contactSyncService.OnAccountInfoExtracted += OnAccountInfoExtractedFromContacts;

                        // 初始化命令服务
                        _commandService = new CommandService(_connectionManager);

                        // 连接WebSocket
                        _ = Task.Run(async () => await _webSocketService.ConnectAsync());

                        Logger.LogInfo("服务初始化完成");
                        
                        // 启动定时器检测微信进程
                        Dispatcher.Invoke(() =>
                        {
                            StartWeChatProcessCheckTimer();
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"初始化服务失败: {ex.Message}", ex);
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                                "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"初始化服务失败: {ex.Message}", ex);
                MessageBox.Show($"初始化失败: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", 
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// 启动定时器检测微信进程
        /// </summary>
        private void StartWeChatProcessCheckTimer()
        {
            try
            {
                if (_weChatProcessCheckTimer != null)
                {
                    _weChatProcessCheckTimer.Stop();
                    _weChatProcessCheckTimer = null;
                }

                _weChatProcessCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3) // 每3秒检测一次
                };
                _weChatProcessCheckTimer.Tick += WeChatProcessCheckTimer_Tick;
                _weChatProcessCheckTimer.Start();

                Logger.LogInfo("已启动微信进程检测定时器（每3秒检测一次）");
                AddLog("已启动微信进程检测定时器...", "INFO");
            }
            catch (Exception ex)
            {
                Logger.LogError($"启动微信进程检测定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 微信进程检测定时器事件
        /// </summary>
        private void WeChatProcessCheckTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // 输出定时器运行日志，让用户知道程序正在运行
                Logger.LogInfo("[定时器] 正在检测微信进程...");
                
                // 检查微信进程是否运行
                bool weChatRunning = IsWeChatProcessRunning();
                
                if (weChatRunning && !_isWeChatConnected)
                {
                    // 发现微信进程，但未连接，尝试连接
                    Logger.LogInfo("检测到微信进程，尝试连接...");
                    AddLog("检测到微信进程，正在连接...", "INFO");
                    
                    if (_connectionManager != null)
                    {
                        bool result = _connectionManager.Connect();
                        if (result)
                        {
                            _isWeChatConnected = true;
                            Logger.LogInfo("微信连接成功");
                            AddLog("微信连接成功", "SUCCESS");
                            
                            // 启动账号信息获取定时器
                            StartAccountInfoFetchTimer();
                        }
                        else
                        {
                            Logger.LogWarning("微信连接失败，将在下次检测时重试");
                        }
                    }
                }
                else if (!weChatRunning && _isWeChatConnected)
                {
                    // 微信进程已退出，断开连接
                    Logger.LogInfo("微信进程已退出，断开连接");
                    AddLog("微信进程已退出", "WARN");
                    
                    _isWeChatConnected = false;
                    if (_connectionManager != null && _connectionManager.IsConnected)
                    {
                        _connectionManager.Disconnect();
                    }
                    
                    // 停止账号信息获取定时器
                    StopAccountInfoFetchTimer();
                }
                else if (!weChatRunning)
                {
                    // 微信进程未运行，自动启动微信
                    Logger.LogInfo("[定时器] 微信进程未运行，正在自动启动微信...");
                    AddLog("微信进程未运行，正在自动启动微信...", "INFO");
                    
                    if (_connectionManager != null && !_isWeChatConnected)
                    {
                        try
                        {
                            bool result = _connectionManager.Connect();
                            if (result)
                            {
                                _isWeChatConnected = true;
                                Logger.LogInfo("[定时器] 微信自动启动成功");
                                AddLog("微信自动启动成功", "SUCCESS");
                                
                                // 启动账号信息获取定时器
                                StartAccountInfoFetchTimer();
                            }
                            else
                            {
                                Logger.LogWarning("[定时器] 微信自动启动失败，将在下次检测时重试");
                                AddLog("微信自动启动失败，将在下次检测时重试", "WARN");
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[定时器] 微信自动启动异常: {ex.Message}", ex);
                            AddLog($"微信自动启动异常: {ex.Message}", "ERROR");
                        }
                    }
                    else if (_isWeChatConnected)
                    {
                        // 如果已连接但进程不存在，可能是进程刚退出，等待下次检测
                        Logger.LogInfo("[定时器] 微信进程未运行，但连接状态仍为已连接，等待下次检测...");
                    }
                }
                else if (_isWeChatConnected)
                {
                    // 微信已连接，输出日志让用户知道定时器正在工作
                    Logger.LogInfo("[定时器] 微信已连接，继续监控...");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"微信进程检测失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查微信进程是否运行
        /// </summary>
        private bool IsWeChatProcessRunning()
        {
            try
            {
                // 检查 WeChat 进程
                Process[] weChatProcesses = Process.GetProcessesByName("WeChat");
                if (weChatProcesses.Length > 0)
                {
                    return true;
                }

                // 检查 Weixin 进程（新版本）
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
                if (_connectionManager == null || !_connectionManager.IsConnected)
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

                // 只检查是否已收到账号信息，不主动请求
                // 账号信息应该通过微信发送的 11120/11121 回调消息获取
                if (!hasAccountInfo)
                {
                    // 还没有账号信息，继续等待 11120/11121 回调消息
                    Logger.LogInfo("[定时器] 尚未收到账号信息，继续等待11120/11121回调消息...");
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
                    // 停止微信进程检测定时器
                    if (_weChatProcessCheckTimer != null)
                    {
                        _weChatProcessCheckTimer.Stop();
                        _weChatProcessCheckTimer = null;
                        Logger.LogInfo("已获取到完整账号信息（account、nickname等字段），停止微信进程检测定时器");
                        AddLog("已获取到完整账号信息，停止微信进程检测定时器", "SUCCESS");
                    }

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
                if (_connectionManager == null)
                {
                    AddLog("连接管理器未初始化，无法自动连接微信", "WARN");
                    return;
                }

                AddLog("步骤1: 检查连接管理器状态...", "INFO");
                
                if (_connectionManager == null || !_connectionManager.IsConnected)
                {
                    AddLog("步骤2: 微信未连接，尝试连接...", "INFO");
                    
                    bool result = _connectionManager?.Connect() ?? false;
                    
                    if (result)
                    {
                        AddLog("========== 微信连接成功 ==========", "SUCCESS");
                    AddLog($"微信版本: {_connectionManager?.WeChatVersion ?? "未知"}", "SUCCESS");
                    AddLog($"客户端ID: {_connectionManager?.ClientId ?? 0}", "SUCCESS");
                    AddLog($"连接状态: {(_connectionManager?.IsConnected == true ? "已连接" : "未连接")}", _connectionManager?.IsConnected == true ? "SUCCESS" : "WARN");
                        
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
                        AddLog($"当前微信版本: {_connectionManager?.WeChatVersion ?? "未知"}", "WARN");
                        AddLog("提示: 如果微信已登录，请稍等片刻，程序会自动检测", "INFO");
                    }
                }
                else
                {
                    AddLog("微信已连接，正在获取账号信息...", "INFO");
                    UpdateAccountList();
                    
                    // 连接成功后，等待微信发送的 11120/11121 回调消息
                    // 不主动请求，只等待回调消息
                    Logger.LogInfo("连接成功，等待微信发送11120/11121回调消息...");
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
                string weChatVersion = _connectionManager?.WeChatVersion ?? "未知";
                
                // 更新连接状态
                if (_connectionManager != null && _connectionManager.IsConnected)
                {
                    // 显示账号信息面板，隐藏未连接面板
                    AccountInfoPanel.Visibility = Visibility.Visible;
                    NotConnectedPanel.Visibility = Visibility.Collapsed;
                    
                    // 更新版本信息（显示在账号信息下方）
                    VersionText.Text = $"版本: {weChatVersion}";
                    
                    // 如果有账号信息，更新显示
                    UpdateAccountInfoDisplay();
                }
                else
                {
                    // 隐藏账号信息面板，显示未连接面板
                    AccountInfoPanel.Visibility = Visibility.Collapsed;
                    NotConnectedPanel.Visibility = Visibility.Visible;
                    
                    StatusText.Text = "微信: 未连接";
                    // 即使未连接，如果已经检测到版本，也显示版本号
                    VersionText.Text = $"版本: {weChatVersion}";
                }

                // 更新左侧Logo下方的版本号
                if (AppVersionText != null)
                {
                    AppVersionText.Text = "版本号: 1.0.0";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新UI失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 更新账号信息显示
        /// </summary>
        private void UpdateAccountInfoDisplay()
        {
            try
            {
                if (_connectionManager == null || !_connectionManager.IsConnected)
                {
                    Logger.LogWarning("连接管理器未初始化或未连接，无法更新账号信息显示");
                    return;
                }

                // 从账号列表中查找当前登录的账号
                // 优先查找有昵称和头像的账号（从WebSocket同步过来的）
                int clientId = _connectionManager.ClientId;
                
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
                    
                    // 更新连接状态区域的头像和昵称
                    AccountNickName.Text = string.IsNullOrEmpty(currentAccount.NickName) 
                        ? "微信用户" 
                        : currentAccount.NickName;
                    
                    // 更新微信ID
                    AccountWeChatId.Text = $"微信ID: {currentAccount.WeChatId}";
                    
                    // 更新连接状态区域的头像
                    UpdateAvatarImage(AccountAvatar, currentAccount.Avatar);
                    
                    // 更新按钮上方的头像和昵称
                    TopNickName.Text = string.IsNullOrEmpty(currentAccount.NickName) 
                        ? "微信用户" 
                        : currentAccount.NickName;
                    
                    TopWeChatId.Text = !string.IsNullOrEmpty(currentAccount.BoundAccount) 
                        ? currentAccount.BoundAccount 
                        : currentAccount.WeChatId;
                    
                    UpdateAvatarImage(TopAvatar, currentAccount.Avatar);
                    
                    // 显示头像面板
                    AvatarPanel.Visibility = Visibility.Visible;
                    
                    Logger.LogInfo("账号信息显示已更新，头像面板已显示");
                }
                else
                {
                    // 没有找到账号信息，显示默认信息
                    AccountNickName.Text = "微信用户";
                    AccountWeChatId.Text = "微信ID: 未知";
                    UpdateAvatarImage(AccountAvatar, null);
                    
                    TopNickName.Text = "微信用户";
                    TopWeChatId.Text = "未知";
                    UpdateAvatarImage(TopAvatar, null);
                    
                    // 隐藏头像面板
                    AvatarPanel.Visibility = Visibility.Collapsed;
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
        private void UpdateAvatarImage(Image imageControl, string? avatarUrl)
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
                    _isWeChatConnected = true;
                    StartAccountInfoFetchTimer();
                }
                else
                {
                    Logger.LogInfo("微信连接状态变化：已断开");
                    // 断开连接时，清空账号列表
                    _accountList?.Clear();
                    // 隐藏头像面板
                    AvatarPanel.Visibility = Visibility.Collapsed;
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
                if (_connectionManager == null || !_connectionManager.IsConnected)
                {
                    return;
                }

                // 检查是否已存在该账号
                // 注意：不要使用clientId（进程ID）作为WeChatId，应该等待真正的wxid
                int clientId = _connectionManager.ClientId;
                
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

                // 如果没有真正的wxid，不创建账号（等待11120/11121回调提供真正的wxid）
                if (!exists)
                {
                    Logger.LogInfo("账号列表中暂无真正的wxid，等待11120/11121回调提供账号信息");
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
        /// 不主动请求，只等待微信发送的 11120/11121 回调消息
        /// 好友、标签、朋友圈等数据由 app 端触发
        /// </summary>
        private void CheckAccountInfo()
        {
            try
            {
                if (_connectionManager == null || !_connectionManager.IsConnected)
                {
                    Logger.LogWarning("微信未连接，无法检查账号信息");
                    return;
                }

                // 只检查是否已收到账号信息，不主动请求
                // 账号信息应该通过微信发送的 11120/11121 回调消息获取
                // 这些消息中包含 account、avatar、nickname 等字段
                Logger.LogInfo("检查是否已收到账号信息（等待11120/11121回调消息）");
                
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
                    // 还没有账号信息，继续等待 11120/11121 回调消息
                    Logger.LogInfo("尚未收到账号信息，继续等待11120/11121回调消息...");
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
                    if (lastValidBrace >= 0 && lastValidBrace < cleanMessage.Length - 1)
                    {
                        // 如果 `}` 后面还有字符，可能是乱码，移除它们
                        string afterBrace = cleanMessage.Substring(lastValidBrace + 1);
                        // 检查是否是乱码（包含非可打印字符或特殊字符）
                        bool isGarbage = false;
                        foreach (char c in afterBrace)
                        {
                            if (char.IsControl(c) || (c != '}' && c != ']' && !char.IsLetterOrDigit(c) && !char.IsPunctuation(c) && !char.IsSymbol(c)))
                            {
                                isGarbage = true;
                                break;
                            }
                        }
                        if (isGarbage)
                        {
                            Logger.LogWarning($"检测到消息末尾有乱码字符，移除: {afterBrace}");
                            cleanMessage = cleanMessage.Substring(0, lastValidBrace + 1);
                        }
                    }

                    // 4. 修复被截断的type字段
                    // 情况1: "type":1112 应该是 "type":11120 或 "type":11121
                    if (cleanMessage.Contains("\"type\":1112") && !cleanMessage.Contains("\"type\":11120") && !cleanMessage.Contains("\"type\":11121"))
                    {
                        Logger.LogWarning("检测到type字段被截断（1112），尝试修复为11120");
                        cleanMessage = cleanMessage.Replace("\"type\":1112", "\"type\":11120");
                    }
                    // 情况2: "type":11120 后面有乱码，如 "type":111200
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
                        
                        // 策略2: 如果消息包含 "type":1112，尝试修复为 11120
                        if (!isFixed && cleanMessage.Contains("\"type\":1112"))
                        {
                            string fixedJson = cleanMessage.Replace("\"type\":1112", "\"type\":11120");
                            if (!fixedJson.EndsWith("}"))
                            {
                                fixedJson = fixedJson + "}";
                            }
                            try
                            {
                                messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(fixedJson) ?? null;
                                if (messageObj != null)
                                {
                                    Logger.LogInfo("通过修复type字段成功解析");
                                    cleanMessage = fixedJson;
                                    isFixed = true;
                                }
                            }
                            catch
                            {
                                // 继续尝试其他策略
                            }
                        }
                        
                        // 策略3: 如果消息不完整，尝试补全
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
                                if (fixedJson.EndsWith("\""))
                                {
                                    fixedJson = fixedJson + "}";
                                }
                                else if (fixedJson.EndsWith("\"type\":1112"))
                                {
                                    fixedJson = fixedJson.Replace("\"type\":1112", "\"type\":11120}");
                        }
                        else
                        {
                                    fixedJson = fixedJson + "}";
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
                    
                    // 特殊处理：如果type是1112，可能是11120被截断了
                    if (messageType == 1112)
                    {
                        Logger.LogWarning("检测到消息类型1112，可能是11120被截断，尝试修复");
                        messageType = 11120;
                    }

                    // 记录所有消息类型，用于调试
                    Logger.LogInfo($"收到消息类型: {messageType}, 消息内容: {message}");

                    // 根据原项目和日志，消息类型 11120 和 11121 都表示登录回调
                    // 11120 可能是初始化消息，11121 是登录成功消息
                    // 从日志看，11120 消息包含账号信息（wxid, nickname, avatar等）
                    // 注意：1112 可能是 11120 被截断的情况
                    if (messageType == 11120 || messageType == 11121 || messageType == 1112)
                    {
                        // 解析登录信息
                        // 注意：11120消息的data可能直接是对象，不需要再次解析
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
                            int clientId = _connectionManager?.ClientId ?? 0;
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
                            
                            Logger.LogInfo($"解析登录信息: wxid={(!string.IsNullOrEmpty(wxid) ? wxid : "空")}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(avatar) ? "有头像" : "无头像")}, account={account}");

                            // 如果wxid为空，不创建账号信息（等待真正的wxid）
                            if (string.IsNullOrEmpty(wxid))
                            {
                                Logger.LogWarning("wxid为空，跳过账号信息更新，等待11120/11121回调提供真正的wxid");
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
                                        Client = $"客户端{clientId}",
                                        WeChatId = wxid,
                                        BoundAccount = account
                                    };
                                    _accountList.Add(accountInfo);
                                }
                            }

                            // 更新账号信息
                            if (accountInfo != null)
                            {
                                accountInfo.Client = $"客户端{clientId}";
                                accountInfo.WeChatId = wxid;
                                accountInfo.BoundAccount = account;
                                
                                if (!string.IsNullOrEmpty(nickname))
                                {
                                    accountInfo.NickName = nickname;
                                }
                            }
                            
                            if (accountInfo != null && !string.IsNullOrEmpty(avatar))
                            {
                                accountInfo.Avatar = avatar;
                            }

                            Logger.LogInfo($"收到登录回调: wxid={wxid}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(avatar) ? "有头像" : "无头像")}, account={account}");
                            AddLog($"收到登录回调: wxid={wxid}, nickname={nickname}", "SUCCESS");
                            
                            // 更新UI显示
                            UpdateAccountInfoDisplay();
                            
                            // 实时同步我的信息到服务器
                            SyncMyInfoToServer(wxid, nickname, avatar, account);
                            
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
                    // 也检查是否是直接的账号信息消息（兼容其他格式）
                    // 尝试多种字段名（支持驼峰命名和首字母大写）
                    else if (messageObj.nickname != null || messageObj.avatar != null || messageObj.wxid != null ||
                             messageObj.nickName != null || messageObj.NickName != null ||
                             messageObj.Avatar != null || messageObj.wxId != null || messageObj.WxId != null)
                    {
                        int clientId = _connectionManager?.ClientId ?? 0;
                        
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
                                    Client = $"客户端{clientId}",
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
                            account.Avatar = messageObj.avatar?.ToString() ?? string.Empty;
                        }
                        else if (account != null && messageObj.Avatar != null)
                        {
                            account.Avatar = messageObj.Avatar?.ToString() ?? string.Empty;
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
        /// 从好友列表回调中提取账号信息的事件处理
        /// </summary>
        private void OnAccountInfoExtractedFromContacts(object? sender, (string wxid, string nickname, string avatar, string account) accountInfo)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    Logger.LogInfo($"从好友列表回调提取到账号信息: wxid={accountInfo.wxid}, nickname={accountInfo.nickname}, account={accountInfo.account}");
                    
                    int clientId = _connectionManager?.ClientId ?? 0;
                    
                    // 更新或创建账号信息
                    AccountInfo? accountInfoObj = null;
                    if (_accountList != null)
                    {
                        foreach (var acc in _accountList)
                        {
                            if (IsRealWeChatId(acc.WeChatId) && acc.WeChatId == accountInfo.wxid)
                            {
                                accountInfoObj = acc;
                                break;
                            }
                        }
                        
                        if (accountInfoObj == null)
                        {
                            accountInfoObj = new AccountInfo
                            {
                                Client = $"客户端{clientId}",
                                WeChatId = accountInfo.wxid,
                                BoundAccount = accountInfo.account
                            };
                            _accountList.Add(accountInfoObj);
                        }
                    }
                    
                    // 更新账号信息
                    if (accountInfoObj != null)
                    {
                        if (!string.IsNullOrEmpty(accountInfo.nickname))
                        {
                            accountInfoObj.NickName = accountInfo.nickname;
                        }
                        
                        if (!string.IsNullOrEmpty(accountInfo.avatar))
                        {
                            accountInfoObj.Avatar = accountInfo.avatar;
                        }
                        
                        accountInfoObj.WeChatId = accountInfo.wxid;
                        accountInfoObj.BoundAccount = accountInfo.account;
                        
                        Logger.LogInfo($"账号信息已更新: wxid={accountInfoObj.WeChatId}, nickname={accountInfoObj.NickName}");
                        AddLog($"从好友列表获取到账号信息: wxid={accountInfoObj.WeChatId}, nickname={accountInfoObj.NickName}", "SUCCESS");
                    }
                    
                    // 更新UI显示
                    UpdateAccountInfoDisplay();
                    
                    // 同步到服务器
                    SyncMyInfoToServer(accountInfo.wxid, accountInfo.nickname, accountInfo.avatar, accountInfo.account);
                    
                    // 获取到账号信息后，停止定时器
                    StopTimersAfterAccountInfoReceived();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"处理从好友列表提取的账号信息失败: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// 同步我的信息到服务器
        /// </summary>
        private void SyncMyInfoToServer(string wxid, string nickname, string avatar, string account)
        {
            try
            {
                Logger.LogInfo($"开始同步我的信息到服务器: wxid={wxid}, nickname={nickname}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_my_info",
                    data = new
                    {
                        wxid = wxid,
                        nickname = nickname,
                        avatar = avatar,
                        account = account,
                        clientId = _connectionManager?.ClientId ?? 0
                    }
                };

                _ = _webSocketService?.SendMessageAsync(syncData);

                Logger.LogInfo("我的信息同步完成");
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
                                            Client = $"客户端{clientId}",
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
        /// 添加日志（带颜色）
        /// </summary>
        private void AddLog(string message, string level = "INFO")
        {
            // 格式化日志文本（用于文件保存）
            string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
            
            // 异步写入日志文件，避免阻塞UI线程
            _ = Task.Run(() =>
            {
                try
                {
                    // 使用文件流写入，支持文件共享，避免文件被锁定
                    // 最多重试3次，每次间隔100ms
                    int retryCount = 0;
                    int maxRetries = 3;
                    bool success = false;
                    
                    while (!success && retryCount < maxRetries)
                    {
                        try
                        {
                            // 使用 FileShare.ReadWrite 允许其他进程读取和写入
                            using (var fileStream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                            using (var writer = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
                            {
                                writer.WriteLine(logText);
                                writer.Flush();
                            }
                            success = true;
                        }
                        catch (IOException ioEx) when (ioEx.Message.Contains("being used by another process"))
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                // 等待一段时间后重试
                                System.Threading.Thread.Sleep(100);
                            }
                            else
                            {
                                // 最后一次重试失败，记录错误但不抛出异常
                                Logger.LogError($"写入日志文件失败（已重试{maxRetries}次）: {ioEx.Message}", ioEx);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 其他异常，记录错误但不抛出
                            Logger.LogError($"写入日志文件失败: {ex.Message}", ex);
                            success = true; // 避免无限重试
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 文件写入失败不影响UI显示，只记录错误
                    Logger.LogError($"写入日志文件失败: {ex.Message}", ex);
                }
            });
            
            // 使用BeginInvoke异步处理，避免阻塞UI线程
            // 使用Normal优先级，确保日志能够及时显示，不会被鼠标交互阻塞
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    FlowDocument document = LogRichTextBox.Document;
                    
                    // 每次都创建新段落，确保正常换行
                    Paragraph paragraph = new Paragraph
                    {
                        Margin = new Thickness(0)
                    };
                    
                    // 时间戳（灰色）- 使用缓存的Brush
                    Run timeRun = new Run($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ")
                    {
                        Foreground = TimeBrush
                    };
                    paragraph.Inlines.Add(timeRun);
                    
                    // 根据日志级别设置颜色 - 使用缓存的Brush
                    Brush messageBrush = GetLogColor(level);
                    Run messageRun = new Run(message)
                    {
                        Foreground = messageBrush
                    };
                    paragraph.Inlines.Add(messageRun);
                    
                    // 将新日志插入到最前面（最新日志显示在最上面）
                    if (document.Blocks.FirstBlock != null)
                    {
                        document.Blocks.InsertBefore(document.Blocks.FirstBlock, paragraph);
                    }
                    else
                    {
                        document.Blocks.Add(paragraph);
                    }
                    
                    // 限制日志块数量（保留最新200个块，减少内存占用和性能开销）
                    if (document.Blocks.Count > 200)
                    {
                        // 批量删除，避免频繁操作
                        int removeCount = document.Blocks.Count - 200;
                        for (int i = 0; i < removeCount; i++)
                        {
                            document.Blocks.Remove(document.Blocks.LastBlock);
                        }
                    }
                    
                    // 不自动滚动，避免频繁滚动导致卡顿
                    // 用户可以通过滚动条手动查看最新日志
                }
                catch (Exception ex)
                {
                    // 如果RichTextBox出错，回退到简单文本
                    Logger.LogError($"添加日志失败: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Normal);
        }

        /// <summary>
        /// 根据日志级别获取颜色（使用缓存的Brush）
        /// </summary>
        private Brush GetLogColor(string level)
        {
            switch (level.ToUpper())
            {
                case "ERROR":
                    return ErrorBrush;
                case "WARN":
                case "WARNING":
                    return WarnBrush;
                case "INFO":
                    return InfoBrush;
                case "SUCCESS":
                    return SuccessBrush;
                default:
                    return DefaultBrush;
            }
        }

        /// <summary>
        /// 复制日志按钮点击事件
        /// </summary>
        private void BtnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FlowDocument document = LogRichTextBox.Document;
                
                // 使用更可靠的方法：直接获取整个Document的文本范围
                TextRange fullRange = new TextRange(
                    document.ContentStart,
                    document.ContentEnd
                );
                
                string allText = fullRange.Text;
                
                // 如果直接获取失败，尝试遍历所有段落
                if (string.IsNullOrEmpty(allText) || allText.Trim().Length == 0)
                {
                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    foreach (Block block in document.Blocks)
                    {
                        if (block is Paragraph paragraph)
                        {
                            TextRange paragraphRange = new TextRange(
                                paragraph.ContentStart,
                                paragraph.ContentEnd
                            );
                            string paragraphText = paragraphRange.Text;
                            if (!string.IsNullOrEmpty(paragraphText))
                            {
                                sb.AppendLine(paragraphText);
                            }
                        }
                    }
                    allText = sb.ToString();
                }
                
                if (!string.IsNullOrEmpty(allText) && allText.Trim().Length > 0)
                {
                    Clipboard.SetText(allText);
                    int lineCount = allText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    AddLog($"日志已复制到剪贴板（共 {lineCount} 行，{document.Blocks.Count} 个段落）", "SUCCESS");
                }
                else
                {
                    AddLog("日志为空，无法复制", "WARN");
                }
            }
            catch (Exception ex)
            {
                AddLog($"复制日志失败: {ex.Message}", "ERROR");
                Logger.LogError($"复制日志失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 清空日志按钮点击事件
        /// </summary>
        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogRichTextBox.Document.Blocks.Clear();
            LogRichTextBox.Document.Blocks.Add(new Paragraph());
            AddLog("日志已清空", "INFO");
        }

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
                
                // 检查连接管理器是否初始化
                if (_connectionManager == null)
                {
                    string errorMsg = "连接管理器未初始化，请检查初始化过程";
                    Logger.LogError(errorMsg);
                    AddLog($"错误: {errorMsg}", "ERROR");
                    AddLog("提示: 连接管理器在应用程序启动时应该已经初始化", "WARN");
                    return;
                }

                AddLog("步骤1: 检查连接管理器状态...", "INFO");
                AddLog($"连接管理器已创建: {_connectionManager != null}", _connectionManager != null ? "SUCCESS" : "ERROR");
                AddLog($"当前连接状态: {(_connectionManager?.IsConnected == true ? "已连接" : "未连接")}", _connectionManager?.IsConnected == true ? "SUCCESS" : "WARN");
                AddLog($"当前微信版本: {_connectionManager?.WeChatVersion ?? "未知"}", "INFO");

                AddLog("步骤2: 初始化连接管理器...", "INFO");
                bool initResult = _connectionManager?.Initialize() ?? false;
                AddLog($"初始化结果: {(initResult ? "成功" : "失败")}", initResult ? "SUCCESS" : "ERROR");
                
                if (!initResult)
                {
                    AddLog("错误: 连接管理器初始化失败", "ERROR");
                    AddLog("可能的原因:", "ERROR");
                    AddLog("  1. 微信未安装或未正确安装", "ERROR");
                    AddLog("  2. 无法检测到微信版本", "ERROR");
                    AddLog("  3. DLL文件不存在或路径不正确", "ERROR");
                    AddLog("  4. DLL版本与微信版本不匹配", "ERROR");
                    AddLog($"当前检测到的微信版本: {_connectionManager?.WeChatVersion ?? "未检测到"}", "ERROR");
                    
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

                AddLog($"步骤3: 连接管理器初始化成功，微信版本: {_connectionManager?.WeChatVersion ?? "未检测到"}", "SUCCESS");
                AddLog("步骤4: 正在打开微信...", "INFO");
                
                if (_connectionManager == null)
                {
                    AddLog("连接管理器未初始化", "ERROR");
                    return;
                }
                bool result = _connectionManager.Connect();
                
                if (result)
                {
                    AddLog("========== 微信登录成功 ==========", "SUCCESS");
                    AddLog($"微信版本: {_connectionManager?.WeChatVersion ?? "未知"}", "SUCCESS");
                    AddLog($"客户端ID: {_connectionManager?.ClientId ?? 0}", "SUCCESS");
                    AddLog($"连接状态: {(_connectionManager?.IsConnected == true ? "已连接" : "未连接")}", _connectionManager?.IsConnected == true ? "SUCCESS" : "WARN");
                    
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
                    AddLog($"当前微信版本: {_connectionManager.WeChatVersion ?? "未知"}", "ERROR");
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
        /// 停止所有定时器
        /// </summary>
        private void StopAllTimers()
        {
            try
            {
                // 停止微信进程检测定时器
                if (_weChatProcessCheckTimer != null)
                {
                    _weChatProcessCheckTimer.Stop();
                    _weChatProcessCheckTimer = null;
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
            // 如果已经在关闭中，直接允许关闭
            if (_isClosing)
            {
                base.OnClosing(e);
                return;
            }
            
            // 阻止窗口立即关闭
            e.Cancel = true;
            _isClosing = true;
            
            // 显示关闭进度遮罩
            Dispatcher.Invoke(() =>
            {
                ClosingOverlay.Visibility = Visibility.Visible;
                ClosingProgressBar.Value = 0;
                ClosingStatusText.Text = "准备关闭...";
                ClosingProgressText.Text = "0%";
            });
            
            // 异步执行资源清理
            Task.Run(async () =>
            {
                try
                {
                    Logger.LogInfo("========== 开始清理资源 ==========");
                    
                    // 0. 停止所有定时器
                    UpdateClosingProgress(5, "正在停止所有定时器...");
                    Logger.LogInfo("正在停止所有定时器...");
                    try
                    {
                        Dispatcher.Invoke(() => StopAllTimers());
                        Logger.LogInfo("所有定时器已停止");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"停止定时器时出错: {ex.Message}");
                    }
                    
                    // 1. 取消事件订阅（防止内存泄漏）
                    UpdateClosingProgress(15, "正在取消事件订阅...");
                    Logger.LogInfo("正在取消事件订阅...");
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 取消Logger事件订阅
                            if (_loggerEventHandler != null)
                            {
                                Logger.OnLogMessage -= _loggerEventHandler;
                                _loggerEventHandler = null;
                            }
                            
                            // 取消连接管理器事件订阅
                            if (_connectionManager != null)
                            {
                                _connectionManager.OnConnectionStateChanged -= OnConnectionStateChanged;
                                _connectionManager.OnMessageReceived -= OnWeChatMessageReceived;
                            }
                            
                            // 取消WebSocket服务事件订阅
                            if (_webSocketService != null)
                            {
                                _webSocketService.OnMessageReceived -= OnWebSocketMessageReceived;
                                _webSocketService.OnConnectionStateChanged -= OnWebSocketConnectionStateChanged;
                            }
                            
                            // 取消联系人同步服务事件订阅
                            if (_contactSyncService != null)
                            {
                                _contactSyncService.OnAccountInfoExtracted -= OnAccountInfoExtractedFromContacts;
                            }
                        });
                        Logger.LogInfo("事件订阅已取消");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"取消事件订阅时出错: {ex.Message}");
                    }
                    
                    // 2. 断开WebSocket连接
                    UpdateClosingProgress(30, "正在断开WebSocket连接...");
                    if (_webSocketService != null)
                    {
                        Logger.LogInfo("正在断开WebSocket连接...");
                        try
                        {
                            await _webSocketService.DisconnectAsync().ConfigureAwait(false);
                            Logger.LogInfo("WebSocket连接已断开");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"断开WebSocket连接时出错: {ex.Message}");
                        }
                    }
                    
                    // 3. 关闭Hook连接（撤回DLL注入）
                    UpdateClosingProgress(50, "正在关闭Hook连接（撤回DLL注入）...");
                    if (_connectionManager != null)
                    {
                        Logger.LogInfo("正在关闭Hook连接（撤回DLL注入）...");
                        try
                        {
                            Dispatcher.Invoke(() => _connectionManager.Disconnect());
                            Logger.LogInfo("Hook连接已关闭");
                            
                            // 等待DLL注入完全清理（给系统时间释放文件句柄）
                            UpdateClosingProgress(60, "等待DLL注入资源释放（2秒）...");
                            Logger.LogInfo("等待DLL注入资源释放（2秒）...");
                            await Task.Delay(2000).ConfigureAwait(false);
                            Logger.LogInfo("DLL注入资源已释放");
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"关闭Hook连接时出错: {ex.Message}", ex);
                        }
                    }
                    
                    // 4. 清理同步服务（释放服务资源）
                    UpdateClosingProgress(75, "正在清理同步服务...");
                    Logger.LogInfo("正在清理同步服务...");
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            // 清理服务对象（如果实现了IDisposable，会自动释放）
                            _contactSyncService = null;
                            _momentsSyncService = null;
                            _tagSyncService = null;
                            _chatMessageSyncService = null;
                            _commandService = null;
                        });
                        Logger.LogInfo("同步服务已清理");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清理同步服务时出错: {ex.Message}");
                    }
                    
                    // 5. 清空账号列表（释放集合资源）
                    UpdateClosingProgress(85, "正在清空账号列表...");
                    Logger.LogInfo("正在清空账号列表...");
                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_accountList != null)
                            {
                                _accountList.Clear();
                            }
                        });
                        Logger.LogInfo("账号列表已清空");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"清空账号列表时出错: {ex.Message}");
                    }
                    
                    // 6. 等待后台任务完成（给正在运行的任务时间完成）
                    UpdateClosingProgress(90, "等待后台任务完成（1秒）...");
                    Logger.LogInfo("等待后台任务完成（1秒）...");
                    try
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        Logger.LogInfo("后台任务等待完成");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"等待后台任务时出错: {ex.Message}");
                    }
                    
                    UpdateClosingProgress(100, "资源清理完成，正在关闭窗口...");
                    Logger.LogInfo("========== 资源清理完成 ==========");
                    
                    // 等待一小段时间让用户看到完成状态
                    await Task.Delay(300).ConfigureAwait(false);
                    
                    // 关闭窗口
                    Dispatcher.Invoke(() =>
                    {
                        ClosingOverlay.Visibility = Visibility.Collapsed;
                        Close();
                    });
                }
                catch (Exception ex)
                {
                    Logger.LogError($"关闭窗口时出错: {ex.Message}", ex);
                    UpdateClosingProgress(100, $"关闭时出错: {ex.Message}");
                    
                    // 即使出错也关闭窗口
                    await Task.Delay(1000).ConfigureAwait(false);
                    Dispatcher.Invoke(() =>
                    {
                        ClosingOverlay.Visibility = Visibility.Collapsed;
                        Close();
                    });
                }
            });
        }
        
        /// <summary>
        /// 更新关闭进度
        /// </summary>
        private void UpdateClosingProgress(int progress, string status)
        {
            Dispatcher.Invoke(() =>
            {
                ClosingProgressBar.Value = progress;
                ClosingStatusText.Text = status;
                ClosingProgressText.Text = $"{progress}%";
            });
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
                _connectionManager = null;
                _webSocketService = null;
                _contactSyncService = null;
                _momentsSyncService = null;
                _tagSyncService = null;
                _chatMessageSyncService = null;
                _commandService = null;
                
                // 清空账号列表（双重保险）
                if (_accountList != null)
                {
                    _accountList.Clear();
                    _accountList = null;
                }
                
                // 清空事件处理程序引用
                _loggerEventHandler = null;
                
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

