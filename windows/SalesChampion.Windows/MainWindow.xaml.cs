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
                    return;
                }

                // 从账号列表中查找当前登录的账号
                // 优先查找有昵称和头像的账号（从WebSocket同步过来的）
                int clientId = _connectionManager.ClientId;
                string weChatId = clientId.ToString();
                
                AccountInfo? currentAccount = null;
                
                // 首先尝试通过clientId匹配
                foreach (var account in _accountList)
                {
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
                            break;
                        }
                    }
                }

                if (currentAccount != null)
                {
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
                    UpdateAccountList();
                    // 更新账号信息显示
                    UpdateAccountInfoDisplay();
                }
                else
                {
                    // 断开连接时，清空账号列表
                    _accountList.Clear();
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
                    // 清理消息：移除可能的额外字符和空白
                    string cleanMessage = message?.Trim() ?? string.Empty;
                    if (string.IsNullOrEmpty(cleanMessage))
                    {
                        return;
                    }

                    // 尝试修复不完整的JSON（如果消息被截断）
                    // 如果消息以 "type":1112 结尾，可能是 11120 或 11121 被截断了
                    if (cleanMessage.EndsWith("\"type\":1112") || cleanMessage.EndsWith("\"type\":11120"))
                    {
                        // 尝试补全JSON
                        if (cleanMessage.EndsWith("\"type\":1112"))
                        {
                            cleanMessage = cleanMessage.Replace("\"type\":1112", "\"type\":11120}");
                        }
                        else if (cleanMessage.EndsWith("\"type\":11120"))
                        {
                            cleanMessage = cleanMessage + "}";
                        }
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
                        
                        // 尝试提取JSON对象（如果消息包含多个JSON对象）
                        int firstBrace = cleanMessage.IndexOf('{');
                        int lastBrace = cleanMessage.LastIndexOf('}');
                        if (firstBrace >= 0 && lastBrace > firstBrace)
                        {
                            cleanMessage = cleanMessage.Substring(firstBrace, lastBrace - firstBrace + 1);
                            try
                            {
                                messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(cleanMessage) ?? null;
                            }
                            catch
                            {
                                Logger.LogError($"无法解析消息: {cleanMessage}");
                                return;
                            }
                        }
                        else
                        {
                            Logger.LogError($"无法提取有效的JSON: {cleanMessage}");
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

                    // 根据原项目和日志，消息类型 11120 和 11121 都表示登录回调
                    // 11120 可能是初始化消息，11121 是登录成功消息
                    // 从日志看，11120 消息包含账号信息（wxid, nickname, avatar等）
                    if (messageType == 11120 || messageType == 11121)
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

                            string wxid = loginInfo.wxid?.ToString() ?? "";
                            string nickname = loginInfo.nickname?.ToString() ?? "";
                            string avatar = loginInfo.avatar?.ToString() ?? "";
                            string account = loginInfo.account?.ToString() ?? wxid;
                            
                            // 如果wxid为空，使用clientId作为fallback
                            if (string.IsNullOrEmpty(wxid))
                            {
                                wxid = clientId.ToString();
                            }

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

                            AddLog($"收到登录回调: wxid={wxid}, nickname={nickname}", "SUCCESS");
                            
                            // 更新UI显示
                            UpdateAccountInfoDisplay();
                            
                            // 实时同步我的信息到服务器
                            SyncMyInfoToServer(wxid, nickname, avatar, account);
                            
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
                    else if (messageObj.nickname != null || messageObj.avatar != null || messageObj.wxid != null)
                    {
                        int clientId = _connectionManager?.ClientId ?? 0;
                        string weChatId = messageObj.wxid?.ToString() ?? clientId.ToString();
                        
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
                        
                        // 更新昵称和头像
                        if (messageObj.nickname != null)
                        {
                            account.NickName = messageObj.nickname.ToString();
                        }
                        
                        if (messageObj.avatar != null)
                        {
                            account.Avatar = messageObj.avatar.ToString();
                        }
                        
                        if (messageObj.wxid != null)
                        {
                            account.WeChatId = messageObj.wxid.ToString();
                            account.BoundAccount = messageObj.wxid.ToString();
                        }
                        
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
            Dispatcher.Invoke(() =>
            {
                try
                {
                    FlowDocument document = LogRichTextBox.Document;
                    
                    // 每次都创建新段落，确保正常换行
                    Paragraph paragraph = new Paragraph
                    {
                        Margin = new Thickness(0)
                    };
                    
                    // 时间戳（灰色）
                    Run timeRun = new Run($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ")
                    {
                        Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 128))
                    };
                    paragraph.Inlines.Add(timeRun);
                    
                    // 根据日志级别设置颜色
                    Brush messageBrush = GetLogColor(level);
                    Run messageRun = new Run(message)
                    {
                        Foreground = messageBrush
                    };
                    paragraph.Inlines.Add(messageRun);
                    
                    document.Blocks.Add(paragraph);
                    
                    // 自动滚动到底部
                    LogRichTextBox.ScrollToEnd();
                    
                    // 限制日志块数量（保留最后500个块）
                    if (document.Blocks.Count > 500)
                    {
                        while (document.Blocks.Count > 500)
                        {
                            document.Blocks.Remove(document.Blocks.FirstBlock);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 如果RichTextBox出错，回退到简单文本
                    Logger.LogError($"添加日志失败: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 根据日志级别获取颜色
        /// </summary>
        private Brush GetLogColor(string level)
        {
            switch (level.ToUpper())
            {
                case "ERROR":
                    return new SolidColorBrush(Color.FromRgb(220, 53, 69)); // 红色
                case "WARN":
                case "WARNING":
                    return new SolidColorBrush(Color.FromRgb(255, 193, 7)); // 黄色/橙色
                case "INFO":
                    return new SolidColorBrush(Color.FromRgb(0, 123, 255)); // 蓝色
                case "SUCCESS":
                    return new SolidColorBrush(Color.FromRgb(40, 167, 69)); // 绿色
                default:
                    return new SolidColorBrush(Color.FromRgb(33, 37, 41)); // 深灰色/黑色
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

