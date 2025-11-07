using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private ObservableCollection<AccountInfo> _accountList;
        
        // 日志颜色Brush缓存，避免频繁创建
        private static readonly Brush ErrorBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69));
        private static readonly Brush WarnBrush = new SolidColorBrush(Color.FromRgb(255, 193, 7));
        private static readonly Brush InfoBrush = new SolidColorBrush(Color.FromRgb(0, 123, 255));
        private static readonly Brush SuccessBrush = new SolidColorBrush(Color.FromRgb(40, 167, 69));
        private static readonly Brush DefaultBrush = new SolidColorBrush(Color.FromRgb(33, 37, 41));
        private static readonly Brush TimeBrush = new SolidColorBrush(Color.FromRgb(128, 128, 128));

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            _accountList = new ObservableCollection<AccountInfo>();
            AccountGrid.ItemsSource = _accountList;
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
                Logger.OnLogMessage += (message) =>
                {
                    try
                    {
                        Dispatcher.Invoke(() =>
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
                        });
                    }
                    catch
                    {
                        // 忽略Dispatcher调用失败
                    }
                };

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

                        // 初始化命令服务
                        _commandService = new CommandService(_connectionManager);

                        // 连接WebSocket
                        _ = Task.Run(async () => await _webSocketService.ConnectAsync());

                        Logger.LogInfo("服务初始化完成");
                        
                        // 自动尝试连接微信（如果微信已登录，会自动获取账号信息）
                        _ = Task.Run(() =>
                        {
                            // 延迟一下，确保UI已完全加载
                            System.Threading.Thread.Sleep(1000);
                            Dispatcher.Invoke(() =>
                            {
                                AddLog("正在自动检测已登录的微信...", "INFO");
                                AutoConnectWeChat();
                            });
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
        /// 自动连接微信（检测已登录的微信）
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
                    
                    // 连接成功后，延迟一段时间主动获取账号信息
                    // 因为11120/11121回调可能不会立即触发
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            RequestAccountInfo();
                        });
                    });
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

                // 更新机器码
                MachineCodeText.Text = $"机器码: {GetMachineCode()}";
                
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
                string weChatId = clientId.ToString();
                
                Logger.LogInfo($"更新账号信息显示: clientId={clientId}, 账号列表数量={_accountList.Count}");
                
                AccountInfo? currentAccount = null;
                
                // 首先尝试通过clientId匹配
                foreach (var account in _accountList)
                {
                    Logger.LogInfo($"检查账号: WeChatId={account.WeChatId}, NickName={account.NickName}, Avatar={(!string.IsNullOrEmpty(account.Avatar) ? "有头像" : "无头像")}");
                    
                    // 匹配WeChatId或clientId
                    if (account.WeChatId == weChatId || 
                        account.WeChatId == clientId.ToString() ||
                        (!string.IsNullOrEmpty(account.NickName) && !string.IsNullOrEmpty(account.WeChatId) && account.WeChatId.StartsWith("wxid_")))
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
                    AccountWeChatId.Text = $"微信ID: {weChatId}";
                    UpdateAvatarImage(AccountAvatar, null);
                    
                    TopNickName.Text = "微信用户";
                    TopWeChatId.Text = weChatId;
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
        /// 获取机器码
        /// </summary>
        private string GetMachineCode()
        {
            // 简化实现，实际应该获取硬件信息
            return Environment.MachineName.GetHashCode().ToString("X");
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
                    
                    // 延迟一下，等待可能的登录回调消息
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            Logger.LogInfo("延迟更新账号信息显示");
                            UpdateAccountInfoDisplay();
                        });
                    });
                }
                else
                {
                    Logger.LogInfo("微信连接状态变化：已断开");
                    // 断开连接时，清空账号列表
                    _accountList.Clear();
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
                int clientId = _connectionManager.ClientId;
                string weChatId = clientId.ToString();
                
                bool exists = false;
                foreach (var account in _accountList)
                {
                    if (account.WeChatId == weChatId)
                    {
                        // 更新现有账号信息
                        account.Client = $"客户端{clientId}";
                        account.BoundAccount = weChatId;
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    // 添加新账号
                    var accountInfo = new AccountInfo
                    {
                        Client = $"客户端{clientId}",
                        CompanyName = "", // 暂时为空，后续可从服务器获取
                        NickName = "", // 暂时为空，后续可从微信获取
                        Remark = "", // 暂时为空
                        BoundAccount = weChatId,
                        WeChatId = weChatId,
                        Avatar = "" // 暂时为空，后续可从微信获取
                    };
                    
                    _accountList.Add(accountInfo);
                    AddLog($"账号列表已更新，当前账号数: {_accountList.Count}", "SUCCESS");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"更新账号列表失败: {ex.Message}", ex);
                AddLog($"更新账号列表失败: {ex.Message}", "ERROR");
            }
        }

        /// <summary>
        /// 主动请求账号信息
        /// 尝试通过同步好友列表来获取自己的账号信息
        /// </summary>
        private void RequestAccountInfo()
        {
            try
            {
                if (_connectionManager == null || !_connectionManager.IsConnected)
                {
                    Logger.LogWarning("微信未连接，无法请求账号信息");
                    return;
                }

                Logger.LogInfo("开始主动请求账号信息");
                AddLog("正在获取账号信息...", "INFO");
                
                // 方法1: 尝试同步好友列表，好友列表中可能包含自己的信息
                // 延迟一下，确保Hook已完全建立连接
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Logger.LogInfo("通过同步好友列表获取账号信息");
                        _contactSyncService?.SyncContacts();
                    });
                });
                
                // 方法2: 如果好友列表同步后仍然没有账号信息，尝试从服务器获取
                Task.Delay(5000).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // 检查账号信息是否已更新
                        int clientId = _connectionManager?.ClientId ?? 0;
                        AccountInfo? accountInfo = null;
                        foreach (var acc in _accountList)
                        {
                            if (acc.WeChatId == clientId.ToString())
                            {
                                accountInfo = acc;
                                break;
                            }
                        }
                        
                        // 如果仍然没有昵称和头像，记录警告
                        if (accountInfo != null && (string.IsNullOrEmpty(accountInfo.NickName) || string.IsNullOrEmpty(accountInfo.Avatar)))
                        {
                            Logger.LogWarning($"账号信息不完整: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}, avatar={(!string.IsNullOrEmpty(accountInfo.Avatar) ? "有头像" : "无头像")}");
                            AddLog("账号信息获取不完整，可能11120/11121回调未触发", "WARN");
                        }
                        else if (accountInfo != null)
                        {
                            Logger.LogInfo($"账号信息已成功获取: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");
                            AddLog("账号信息获取成功", "SUCCESS");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"请求账号信息失败: {ex.Message}", ex);
                AddLog($"请求账号信息失败: {ex.Message}", "ERROR");
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
                    Logger.LogInfo($"收到微信消息: {message}");
                    AddLog($"收到微信消息: {message}", "INFO");
                    
                    // 清理消息：移除可能的额外字符和空白
                    string cleanMessage = message?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(cleanMessage))
                    {
                        Logger.LogWarning("收到空消息，忽略");
                        return;
                    }

                    // 尝试修复不完整的JSON（如果消息被截断）
                    // 情况1: 如果消息以 "type":1112 结尾，可能是 11120 或 11121 被截断了
                        if (cleanMessage.EndsWith("\"type\":1112"))
                        {
                        Logger.LogWarning("检测到JSON消息被截断（type:1112），尝试修复为11120");
                            cleanMessage = cleanMessage.Replace("\"type\":1112", "\"type\":11120}");
                        }
                    // 情况2: 如果消息以 "type":11120 结尾但没有闭合括号
                    else if (cleanMessage.EndsWith("\"type\":11120") && !cleanMessage.EndsWith("}"))
                        {
                        Logger.LogWarning("检测到JSON消息不完整（缺少闭合括号），尝试修复");
                            cleanMessage = cleanMessage + "}";
                        }
                    // 情况3: 如果消息以 "type":1112 结尾（可能是11120或11121）
                    else if (cleanMessage.Contains("\"type\":1112") && !cleanMessage.EndsWith("}"))
                    {
                        Logger.LogWarning("检测到JSON消息包含截断的type字段，尝试修复");
                        // 尝试修复为11120
                        cleanMessage = cleanMessage.Replace("\"type\":1112", "\"type\":11120");
                        // 如果还没有闭合括号，添加
                        if (!cleanMessage.EndsWith("}"))
                        {
                            cleanMessage = cleanMessage + "}";
                        }
                    }
                    // 情况4: 如果消息看起来不完整（没有闭合括号）
                    else if (!cleanMessage.EndsWith("}") && cleanMessage.StartsWith("{"))
                    {
                        Logger.LogWarning("检测到JSON消息不完整（缺少闭合括号），尝试修复");
                        cleanMessage = cleanMessage + "}";
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
                            Logger.LogError($"无法解析消息，已尝试多种修复策略: {cleanMessage}");
                            Logger.LogError($"原始消息长度: {message?.Length ?? 0}, 清理后长度: {cleanMessage.Length}");
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
                            
                            // 如果wxid为空，使用clientId作为fallback
                            if (string.IsNullOrEmpty(wxid))
                            {
                                wxid = clientId.ToString();
                            }
                            
                            Logger.LogInfo($"解析登录信息: wxid={wxid}, nickname={nickname}, avatar={(!string.IsNullOrEmpty(avatar) ? "有头像" : "无头像")}, account={account}");

                            // 更新账号信息
                            AccountInfo? accountInfo = null;
                            foreach (var acc in _accountList)
                            {
                                if (acc.WeChatId == wxid || acc.WeChatId == clientId.ToString())
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

                            // 更新账号信息
                            accountInfo.Client = $"客户端{clientId}";
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

                            Logger.LogInfo($"收到登录回调: wxid={wxid}, nickname={nickname}, avatar={avatar}");
                            AddLog($"收到登录回调: wxid={wxid}, nickname={nickname}", "SUCCESS");
                            
                            // 更新UI显示
                            UpdateAccountInfoDisplay();
                            
                            // 实时同步我的信息到服务器
                            SyncMyInfoToServer(wxid, nickname, avatar, account);
                            
                            // 再次更新UI显示，确保头像显示
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    UpdateAccountInfoDisplay();
                                });
                            });
                            
                            // 根据原项目，登录成功后自动触发同步
                            // 延迟1.5秒后开始同步标签和好友列表
                            Task.Delay(1500).ContinueWith(_ =>
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    AddLog("登录成功，开始自动同步数据...", "INFO");
                                    
                                    // 先同步标签
                                    _tagSyncService?.SyncTags();
                                    
                                    // 延迟3秒后同步好友列表
                                    Task.Delay(3000).ContinueWith(__ =>
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            _contactSyncService?.SyncContacts();
                                        });
                                    });
                                });
                            });
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
                        
                        // 更新昵称（尝试多种字段名）
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
                        
                        // 更新头像（尝试多种字段名）
                        if (messageObj.avatar != null)
                        {
                            account.Avatar = messageObj.avatar.ToString();
                        }
                        else if (messageObj.Avatar != null)
                        {
                            account.Avatar = messageObj.Avatar.ToString();
                        }
                        
                        // 更新wxid（尝试多种字段名）
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
                        
                        Logger.LogInfo($"从消息中更新账号信息: wxid={account.WeChatId}, nickname={account.NickName}, avatar={(!string.IsNullOrEmpty(account.Avatar) ? "有头像" : "无头像")}");
                        
                        // 更新UI显示
                        UpdateAccountInfoDisplay();
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
            Dispatcher.Invoke(() =>
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
                                
                                if (accountInfo == null)
                                {
                                    // 创建新账号信息
                                    accountInfo = new AccountInfo
                                    {
                                        Client = $"客户端{clientId}",
                                        WeChatId = !string.IsNullOrEmpty(wxid) ? wxid : clientId.ToString(),
                                        BoundAccount = !string.IsNullOrEmpty(account) ? account : wxid
                                    };
                                    _accountList.Add(accountInfo);
                                }
                                
                                // 更新账号信息
                                if (!string.IsNullOrEmpty(nickname))
                                {
                                    accountInfo.NickName = nickname;
                                }
                                
                                if (!string.IsNullOrEmpty(avatar))
                                {
                                    accountInfo.Avatar = avatar;
                                }
                                
                                if (!string.IsNullOrEmpty(wxid))
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
            });
        }

        /// <summary>
        /// 添加日志（带颜色）
        /// </summary>
        private void AddLog(string message, string level = "INFO")
        {
            // 使用BeginInvoke异步处理，避免阻塞UI线程
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
            }), System.Windows.Threading.DispatcherPriority.Background);
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
                TextRange textRange = new TextRange(
                    LogRichTextBox.Document.ContentStart,
                    LogRichTextBox.Document.ContentEnd
                );
                
                if (!string.IsNullOrEmpty(textRange.Text))
                {
                    Clipboard.SetText(textRange.Text);
                    AddLog("日志已复制到剪贴板", "SUCCESS");
                }
                else
                {
                    AddLog("日志为空，无法复制", "WARN");
                }
            }
            catch (Exception ex)
            {
                AddLog($"复制日志失败: {ex.Message}", "ERROR");
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
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _connectionManager?.Disconnect();
                _webSocketService?.DisconnectAsync().Wait();
            }
            catch (Exception ex)
            {
                Logger.LogError($"关闭窗口时出错: {ex.Message}", ex);
            }

            base.OnClosed(e);
        }
    }
}

