using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Services;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.UI.Controls;
using MyWeChat.Windows.Utils;
using Newtonsoft.Json;

namespace MyWeChat.Windows
{
    /// <summary>
    /// 登录窗口
    /// </summary>
    public partial class LoginWindow : Window
    {
        private WebSocketService? _webSocketService;
        private string _serverUrl;
        private List<AccountInfo> _loginHistory = new List<AccountInfo>();
        private readonly object _loginHistoryLock = new object();
        private string _loginHistoryFilePath;
        private string _rememberedAccountFilePath;
        
        // 微信连接相关（使用全局单例服务）
        private CommandService? _commandService;
        
        // API服务（用于查询数据库中的账号信息）
        private ApiService? _apiService;

        // 统一窗口关闭服务
        private UnifiedWindowCloseService? _unifiedCloseService;
        
        // 启动进度圆环是否已关闭的标志
        private bool _startupProgressClosed = false;
        
        // 强制关闭标志（登录成功后不显示关闭确认对话框）
        private bool _forceClose = false;
        
        private bool _isUpdatingPhoneText = false;
        
        // 启动进度相关
        private const int _totalSteps = 15; // 总步骤数：15个步骤
        
        // 保存全局服务事件处理函数引用，以便在关闭时取消订阅
        private EventHandler<bool>? _connectionStateChangedHandler;
        private EventHandler<string>? _wxidReceivedHandler;
        private EventHandler<string>? _messageReceivedHandler;
        
        // 微信启动进度事件处理
        private EventHandler<(int step, int totalSteps, string status)>? _weChatProgressHandler;

        public LoginWindow()
        {
            InitializeComponent();
            this.Title = "w";
            _serverUrl = ConfigHelper.GetServerUrl();
            _loginHistoryFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_history.json");
            _rememberedAccountFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remembered_account.json");
            
            PhoneTextBox.IsEnabled = true;
            LicenseKeyTextBox.IsEnabled = true;
            PhoneTextBox.Focusable = true;
            LicenseKeyTextBox.Focusable = true;
            PhoneTextBox.IsReadOnly = false;
            LicenseKeyTextBox.IsReadOnly = false;
            PhoneTextBox.IsHitTestVisible = true;
            LicenseKeyTextBox.IsHitTestVisible = true;
            
            var textInputScope = new System.Windows.Input.InputScope();
            textInputScope.Names.Add(new System.Windows.Input.InputScopeName { NameValue = System.Windows.Input.InputScopeNameValue.Default });
            LicenseKeyTextBox.InputScope = textInputScope;
            
            // 登录页不需要托盘图标（避免出现两个托盘图标）
            // _trayIconService = new TrayIconService();
            // _trayIconService.Initialize(this);
            
            Loaded += LoginWindow_Loaded;
            
            // 不设置窗口图标（反检测措施）
        }

        /// <summary>
        /// 设置窗口图标（使用logo.ico）
        /// </summary>
        private void SetWindowIcon()
        {
            try
            {
                // 优先从Resources文件夹加载 logo.ico
                string logoIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.ico");
                if (File.Exists(logoIconPath))
                {
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(logoIconPath));
                    return;
                }
                
                // 从程序集资源加载 logo.ico
                try
                {
                    var resourceUri = new Uri("pack://application:,,,/Resources/logo.ico");
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(resourceUri);
                    return;
                }
                catch
                {
                    // 如果失败，继续尝试其他方式
                }
                
                // 最后尝试加载 favicon.ico
                try
                {
                    var faviconUri = new Uri("pack://application:,,,/Resources/favicon.ico");
                    this.Icon = new System.Windows.Media.Imaging.BitmapImage(faviconUri);
                }
                catch
                {
                    string faviconIconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "favicon.ico");
                    if (File.Exists(faviconIconPath))
                    {
                        this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(faviconIconPath));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"设置登录窗口图标失败: {ex.Message}");
            }
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = "w";
            
            PhoneTextBox.IsEnabled = true;
            LicenseKeyTextBox.IsEnabled = true;
            PhoneTextBox.Focusable = true;
            LicenseKeyTextBox.Focusable = true;
            PhoneTextBox.IsReadOnly = false;
            LicenseKeyTextBox.IsReadOnly = false;
            PhoneTextBox.IsHitTestVisible = true;
            LicenseKeyTextBox.IsHitTestVisible = true;
            
            // 立即显示启动进度圆环
            CloseOverlay.ShowStartupProgress();
            
            _ = Task.Run(async () =>
            {
                try
                {
                    // 步骤1：加载登录历史和记住的账号
                    Dispatcher.Invoke(() => CloseOverlay.UpdateStartupProgress(1, _totalSteps, "正在加载登录历史..."));
                    await LoadLoginHistoryAsync();
                    await LoadRememberedAccountAsync();
                    
                    // 步骤2：初始化WebSocket连接
                    Dispatcher.Invoke(() => CloseOverlay.UpdateStartupProgress(2, _totalSteps, "正在初始化WebSocket连接..."));
                    await Task.Delay(300);
                    await InitializeWebSocketAsync();
                    
                    // 步骤3：初始化微信管理器（不自动启动微信，需要用户手动点击"登录微信"按钮）
                    Dispatcher.Invoke(() => CloseOverlay.UpdateStartupProgress(3, _totalSteps, "正在初始化微信管理器..."));
                    
                    // 订阅微信启动进度事件
                    var hookManager = GetWeChatHookManager();
                    if (hookManager != null)
                    {
                        _weChatProgressHandler = (sender, args) =>
                        {
                            Dispatcher.Invoke(() => CloseOverlay.UpdateStartupProgress(args.step, args.totalSteps, args.status));
                        };
                        hookManager.OnProgressUpdate += _weChatProgressHandler;
                    }
                    
                    await Task.Run(() => InitializeWeChatManagerAsync());
                    
                    // 注意：不再在这里关闭进度圆环，等待微信连接成功后再关闭
                    // 进度圆环的关闭将在OnConnectionStateChanged事件中处理
                }
                catch (Exception ex)
                {
                    Logger.LogError($"启动流程异常: {ex.Message}", ex);
                    Dispatcher.Invoke(() =>
                    {
                        CloseOverlay.UpdateStartupProgress(_totalSteps, _totalSteps, "启动失败");
                        ShowError($"启动失败: {ex.Message}");
                        // 延迟2秒后关闭进度圆环，让用户看到错误信息
                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                CloseOverlay.HideOverlay();
                                _startupProgressClosed = true;
                            });
                        });
                    });
                }
            });
        }

        /// <summary>
        /// 初始化WebSocket连接（异步方法，不阻塞UI）
        /// </summary>
        private async Task InitializeWebSocketAsync()
        {
            try
            {
                string wsUrl = _serverUrl.Replace("http://", "ws://").Replace("https://", "wss://");
                if (!wsUrl.EndsWith("/ws"))
                {
                    wsUrl = wsUrl.EndsWith("/") ? $"{wsUrl}ws" : $"{wsUrl}/ws";
                }

                _webSocketService = new WebSocketService(wsUrl);
                _webSocketService.OnMessageReceived += OnWebSocketMessageReceived;
                
                bool connected = await _webSocketService.ConnectAsync();
                if (connected)
                {
                    // 注意：WebSocket连接成功的日志已在WebSocketService中输出，这里不再重复输出
                }
                else
                {
                    Logger.LogWarning("WebSocket连接失败");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"初始化WebSocket连接失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// WebSocket消息接收事件处理
        /// </summary>
        private void OnWebSocketMessageReceived(object? sender, string message)
        {
            try
            {
                var messageObj = JsonConvert.DeserializeObject<dynamic>(message);
                string messageType = messageObj?.type?.ToString() ?? "";

                // 使用 InvokeAsync 避免阻塞
                _ = Dispatcher.InvokeAsync(() =>
                {
                    switch (messageType)
                    {
                        case "login_response":
                            HandleLoginResponse(messageObj);
                            break;
                        case "quick_login_response":
                            HandleQuickLoginResponse(messageObj);
                            break;
                        case "command":
                            // 处理服务器端发送的命令（如发送验证码消息）
                            HandleCommandMessage(messageObj);
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理WebSocket消息失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 处理命令消息（用于接收服务器端发送的验证码消息命令）
        /// </summary>
        private void HandleCommandMessage(dynamic? messageObj)
        {
            try
            {
                string commandType = messageObj?.command_type?.ToString() ?? "";
                
                // 如果是发送消息命令，且是发送给自己的，可以在这里处理
                       if (commandType == "send_text_message" || commandType == "send_message")
                       {
                           dynamic? commandData = messageObj?.command_data;
                           if (commandData != null)
                           {
                               string? targetWxid = commandData?.target_wxid?.ToString() ?? commandData?.to_wxid?.ToString();
                               string? content = commandData?.content?.ToString();
                               
                               // 如果是发送给自己的消息，且包含验证码，可以在这里处理
                               string? currentWxid = WeChatInitializationService.Instance.WeChatManager?.CurrentWxid;
                               if (!string.IsNullOrEmpty(targetWxid) && targetWxid == currentWxid && !string.IsNullOrEmpty(content))
                               {
                                   Logger.LogInfo($"收到发送给自己的消息命令: {content}");
                                   // 这里可以显示验证码消息，或者直接通过CommandService发送
                                   var service = WeChatInitializationService.Instance;
                                   if (_commandService != null && service.WeChatManager?.ConnectionManager != null && service.WeChatManager.IsConnected)
                                   {
                                       // 通过CommandService发送消息
                                       string commandJson = JsonConvert.SerializeObject(new
                                       {
                                           command_type = "send_message",
                                           command_data = JsonConvert.SerializeObject(new
                                           {
                                               to_wxid = targetWxid,
                                               content = content
                                           }),
                                           target_wechat_id = targetWxid
                                       });
                                       
                                       bool result = _commandService.ProcessCommand(commandJson);
                                       if (result)
                                       {
                                           Logger.LogInfo("验证码消息已发送给自己");
                                       }
                                   }
                               }
                           }
                       }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理命令消息失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理登录响应
        /// </summary>
        private void HandleLoginResponse(dynamic? response)
        {
            bool success = response?.success == true;
            string message = response?.message?.ToString() ?? "";

            if (success)
            {
                // 登录成功，打开主窗口
                ShowSuccess("登录成功");
                Logger.LogInfo("登录成功");
                
                // 标记为强制关闭，不显示关闭确认对话框
                _forceClose = true;
                
                // 打开主窗口（使用空字符串作为wxid，主窗口会从服务器获取）
                var mainWindow = new MainWindow("");
                mainWindow.Show();
                
                // 强制关闭登录页
                this.Close();
            }
            else
            {
                ShowError(message);
            }
        }

        /// <summary>
        /// 处理快速登录响应
        /// </summary>
        private void HandleQuickLoginResponse(dynamic? response)
        {
            bool success = response?.success == true;
            string message = response?.message?.ToString() ?? "";

            if (success)
            {
                // 获取账号信息
                var accountInfoData = response?.account_info;
                if (accountInfoData != null)
                {
                    var accountInfo = new AccountInfo
                    {
                        WeChatId = accountInfoData.wxid?.ToString() ?? "",
                        NickName = accountInfoData.nickname?.ToString() ?? "",
                        Avatar = accountInfoData.avatar?.ToString() ?? "",
                        BoundAccount = accountInfoData.account?.ToString() ?? "",
                        Phone = accountInfoData.phone?.ToString() ?? "",
                        DeviceId = accountInfoData.device_id?.ToString() ?? "",
                        WxUserDir = accountInfoData.wx_user_dir?.ToString() ?? "",
                        UnreadMsgCount = accountInfoData.unread_msg_count ?? 0,
                        IsFakeDeviceId = accountInfoData.is_fake_device_id ?? 0,
                        Pid = accountInfoData.pid ?? 0
                    };

                    _ = Task.Run(async () =>
                    {
                        await SaveLoginStateAsync(accountInfo.WeChatId);
                        await SaveLoginHistoryAsync(accountInfo);
                    });

                    // 标记为强制关闭，不显示关闭确认对话框
                    _forceClose = true;
                    
                    // 打开主窗口
                    var mainWindow = new MainWindow(accountInfo.WeChatId);
                    mainWindow.Show();
                    
                    // 强制关闭登录页
                    this.Close();
                }
            }
            else
            {
                ShowError(message);
            }
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string phone = PhoneTextBox.Text.Trim();
            string licenseKey = LicenseKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                ShowError("请输入手机号");
                return;
            }

            if (string.IsNullOrEmpty(licenseKey))
            {
                ShowError("请输入授权码");
                return;
            }

            // 如果勾选了记住账号，保存账号和密码
            if (RememberAccountCheckBox.IsChecked == true)
            {
                await SaveRememberedAccountAsync(phone, licenseKey);
            }
            else
            {
                // 如果取消勾选，删除保存的账号
                await DeleteRememberedAccountAsync();
            }

            LoginButton.IsEnabled = false;
            ErrorTextBlock.Visibility = Visibility.Collapsed;
            
            // 显示等待图标
            ShowLoadingSpinner(true);

            try
            {
                // 将手机号添加到全局服务的待匹配列表（用于后续1112回调匹配）
                WeChatInitializationService.Instance.AddPendingPhone(phone);
                Logger.LogInfo($"登录时添加手机号到待匹配列表: phone={phone}");
                
                // 【核心逻辑】先查询数据库，看是否有该手机号对应的账号信息
                // 如果数据库有wxid，立即同步到app端，让用户能够快速进行后续操作
                if (_apiService != null)
                {
                    Logger.LogInfo($"[登录] 查询数据库: phone={phone}");
                    var accountInfo = await _apiService.GetAccountInfoByPhoneAsync(phone);
                    if (accountInfo != null && !string.IsNullOrEmpty(accountInfo.WeChatId))
                    {
                        Logger.LogInfo($"[登录] 数据库中找到账号信息: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");
                        
                        // 确保手机号已设置（如果数据库中没有，使用登录时输入的手机号）
                        if (string.IsNullOrEmpty(accountInfo.Phone))
                        {
                            accountInfo.Phone = phone;
                            Logger.LogInfo($"[登录] 账号信息中手机号为空，使用登录时输入的手机号: {phone}");
                        }
                        
                        // 【关键】立即同步到app端，不需要等待1112回调
                        // 这样用户就能快速获取wxid，进行后续操作
                        // 其他字段（nickname、avatar等）可以等待1112回调慢慢更新
                        Logger.LogInfo($"[登录] 立即同步wxid到app端: wxid={accountInfo.WeChatId}, phone={accountInfo.Phone}");
                        SyncMyInfoToServer(accountInfo);
                        
                        // 注意：仍然保持监听1112消息，因为账号信息可能会更新
                        // 1112回调来了会更新其他字段，但不影响主流程
                    }
                    else
                    {
                        Logger.LogInfo($"[登录] 数据库未找到账号信息: phone={phone}，等待1112回调获取wxid");
                    }
                }

                if (_webSocketService != null && _webSocketService.IsConnected)
                {
                    await _webSocketService.SendMessageAsync(new
                    {
                        type = "login",
                        phone = phone,
                        license_key = licenseKey
                    });
                }
                else
                {
                    ShowError("WebSocket未连接，请稍后重试");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"登录失败: {ex.Message}", ex);
                ShowError($"登录失败: {ex.Message}");
            }
            finally
            {
                // 隐藏等待图标
                ShowLoadingSpinner(false);
                LoginButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 快速登录（点击历史记录）
        /// </summary>
        private async void HistoryItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is AccountInfo accountInfo)
            {
                string wxid = accountInfo.WeChatId;
                if (string.IsNullOrEmpty(wxid))
                {
                    ShowError("账号信息不完整");
                    return;
                }

                try
                {
                    // 通过WebSocket发送快速登录请求
                    if (_webSocketService != null && _webSocketService.IsConnected)
                    {
                        await _webSocketService.SendMessageAsync(new
                        {
                            type = "quick_login",
                            wxid = wxid
                        });
                    }
                    else
                    {
                        ShowError("WebSocket未连接，请稍后重试");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"快速登录失败: {ex.Message}", ex);
                    ShowError($"快速登录失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 显示/隐藏登录按钮的等待图标
        /// </summary>
        private void ShowLoadingSpinner(bool show)
        {
            Dispatcher.Invoke(() =>
            {
                if (LoginButton.Content is StackPanel stackPanel)
                {
                    foreach (var child in stackPanel.Children)
                    {
                        if (child is Ellipse ellipse)
                        {
                            if (show)
                            {
                                ellipse.Visibility = Visibility.Visible;
                                // 启动旋转动画
                                var transform = ellipse.RenderTransform as RotateTransform;
                                if (transform != null)
                                {
                                    var animation = new DoubleAnimation
                                    {
                                        From = 0,
                                        To = 360,
                                        Duration = TimeSpan.FromSeconds(1),
                                        RepeatBehavior = RepeatBehavior.Forever
                                    };
                                    transform.BeginAnimation(RotateTransform.AngleProperty, animation);
                                }
                            }
                            else
                            {
                                ellipse.Visibility = Visibility.Collapsed;
                                // 停止动画
                                var transform = ellipse.RenderTransform as RotateTransform;
                                if (transform != null)
                                {
                                    transform.BeginAnimation(RotateTransform.AngleProperty, null);
                                }
                            }
                            break;
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 显示错误信息
        /// </summary>
        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ErrorTextBlock.Text = message;
                ErrorTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                ErrorTextBlock.Visibility = Visibility.Visible;
            });
        }
        
        /// <summary>
        /// 显示成功信息
        /// </summary>
        private void ShowSuccess(string message)
        {
            Dispatcher.Invoke(() =>
            {
                ErrorTextBlock.Text = message;
                ErrorTextBlock.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#07C160"));
                ErrorTextBlock.Visibility = Visibility.Visible;
            });
        }

        private async Task SaveLoginStateAsync(string wxid)
        {
            try
            {
                string stateFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_state.json");
                var state = new { wxid = wxid, loginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                await File.WriteAllTextAsync(stateFilePath, json, Encoding.UTF8);
                Logger.LogInfo($"登录状态已保存: {wxid}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"保存登录状态失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载登录历史
        /// </summary>
        private void LoadLoginHistory()
        {
            // 同步版本保留，但改为异步调用
            _ = LoadLoginHistoryAsync();
        }

        /// <summary>
        /// 异步加载登录历史（不阻塞UI）
        /// </summary>
        private async Task LoadLoginHistoryAsync()
        {
            try
            {
                if (File.Exists(_loginHistoryFilePath))
                {
                    // 在后台线程读取文件
                    string json = await Task.Run(() => File.ReadAllText(_loginHistoryFilePath, Encoding.UTF8));
                    var history = JsonConvert.DeserializeObject<List<AccountInfo>>(json);
                    
                    // 在UI线程更新界面
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (history != null)
                        {
                            lock (_loginHistoryLock)
                            {
                                _loginHistory = history;
                            }
                            HistoryItemsControl.ItemsSource = _loginHistory;
                            
                            if (HistoryTitleTextBlock != null)
                            {
                                HistoryTitleTextBlock.Visibility = _loginHistory.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载登录历史失败: {ex.Message}", ex);
            }
        }

        private async Task SaveLoginHistoryAsync(AccountInfo accountInfo)
        {
            try
            {
                List<AccountInfo> loginHistoryCopy;
                lock (_loginHistoryLock)
                {
                    _loginHistory.RemoveAll(a => a.WeChatId == accountInfo.WeChatId);
                    _loginHistory.Insert(0, accountInfo);
                    if (_loginHistory.Count > 10)
                    {
                        _loginHistory = _loginHistory.Take(10).ToList();
                    }
                    loginHistoryCopy = new List<AccountInfo>(_loginHistory);
                }
                
                string json = JsonConvert.SerializeObject(loginHistoryCopy, Formatting.Indented);
                await File.WriteAllTextAsync(_loginHistoryFilePath, json, Encoding.UTF8);
                
                await Dispatcher.InvokeAsync(() =>
                {
                    List<AccountInfo> historyCopy;
                    lock (_loginHistoryLock)
                    {
                        historyCopy = new List<AccountInfo>(_loginHistory);
                    }
                    HistoryItemsControl.ItemsSource = null;
                    HistoryItemsControl.ItemsSource = historyCopy;
                    if (HistoryTitleTextBlock != null)
                    {
                        HistoryTitleTextBlock.Visibility = historyCopy.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                });
                
                Logger.LogInfo($"登录历史已保存: {accountInfo.WeChatId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"保存登录历史失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 手机号输入框获得焦点
        /// </summary>
        private void PhoneTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                // 找到父级Border
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#07C160"));
                }
            }
        }

        /// <summary>
        /// 手机号输入框失去焦点
        /// </summary>
        private void PhoneTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E5E5"));
                }
            }
        }

        private void LicenseKeyTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#07C160"));
                }
            }
        }

        private void LicenseKeyTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E5E5E5"));
                }
            }
        }

        /// <summary>
        /// 手机号输入框文本变化事件 - 自动去除非数字字符
        /// </summary>
        private void PhoneTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingPhoneText) return; // 防止递归调用
            
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                string originalText = textBox.Text;
                // 只保留数字
                string filteredText = new string(originalText.Where(char.IsDigit).ToArray());
                
                // 如果文本被过滤了，更新文本框（避免光标位置问题）
                if (originalText != filteredText)
                {
                    _isUpdatingPhoneText = true;
                    try
                    {
                        int selectionStart = textBox.SelectionStart;
                        textBox.Text = filteredText;
                        // 调整光标位置
                        textBox.SelectionStart = Math.Min(selectionStart - (originalText.Length - filteredText.Length), filteredText.Length);
                        textBox.SelectionLength = 0;
                    }
                    finally
                    {
                        _isUpdatingPhoneText = false;
                    }
                }
            }
        }


        /// <summary>
        /// 获取微信Hook管理器（用于订阅进度事件）
        /// </summary>
        private Core.Hook.WeChatHookManager? GetWeChatHookManager()
        {
            try
            {
                var service = Services.WeChatInitializationService.Instance;
                var connectionManager = service?.WeChatManager?.ConnectionManager;
                if (connectionManager != null)
                {
                    // 通过反射或公共属性获取HookManager
                    var hookManagerField = connectionManager.GetType().GetField("_hookManager", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hookManagerField != null)
                    {
                        return hookManagerField.GetValue(connectionManager) as Core.Hook.WeChatHookManager;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"获取微信Hook管理器失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 查找父级元素
        /// </summary>
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        /// <summary>
        /// 初始化微信管理器（异步方法，不阻塞UI）
        /// 使用全局单例服务，避免重复创建
        /// </summary>
        private void InitializeWeChatManagerAsync()
        {
            try
            {
                // 使用全局单例服务
                var service = WeChatInitializationService.Instance;
                
                // 保存事件处理函数引用，以便在关闭时取消订阅
                _connectionStateChangedHandler = (sender, isConnected) =>
                {
                    if (isConnected)
                    {
                        // 微信连接成功，关闭启动进度圆环
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            if (!_startupProgressClosed)
                            {
                                CloseOverlay.UpdateStartupProgress(_totalSteps, _totalSteps, "微信连接成功");
                                // 延迟1秒后关闭进度圆环
                                Task.Delay(1000).ContinueWith(_ =>
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        CloseOverlay.HideOverlay();
                                        _startupProgressClosed = true;
                                    });
                                });
                            }
                            ShowSuccess("微信连接成功");
                        }, DispatcherPriority.Background);
                    }
                    else
                    {
                        // 注意：连接断开的日志已在全局服务中输出，这里不再重复输出
                        // 使用 InvokeAsync 避免阻塞，使用低优先级确保不阻塞UI
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            ShowError("微信连接断开，请检查微信是否正常运行");
                        }, DispatcherPriority.Background);
                    }
                };
                
                // 订阅连接状态变化事件
                service.OnConnectionStateChanged += _connectionStateChangedHandler;
                
                // 保存事件处理函数引用
                _wxidReceivedHandler = (sender, wxid) =>
                {
                    Logger.LogInfo($"获取到微信ID（登录窗口）: {wxid}");
                    
                    // 初始化命令服务（在UI线程上执行，使用低优先级确保不阻塞UI）
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (service.WeChatManager?.ConnectionManager != null && _commandService == null)
                        {
                            _commandService = new CommandService(service.WeChatManager.ConnectionManager);
                        }
                    }, DispatcherPriority.Background);
                };
                
                // 订阅微信ID获取事件（1112回调）
                service.OnWxidReceived += _wxidReceivedHandler;
                
                // 保存消息接收事件处理函数引用
                _messageReceivedHandler = (sender, message) =>
                {
                    // 处理1112消息，保存到数据库
                    HandleWeChatMessageForAccountInfo(message);
                };
                
                // 订阅消息接收事件（用于处理1112消息并保存到数据库）
                service.OnMessageReceived += _messageReceivedHandler;
                
                // 初始化API服务（用于查询数据库中的账号信息）
                string serverUrl = ConfigHelper.GetServerUrl();
                _apiService = new ApiService(serverUrl);
                
                // 如果已经初始化，直接完成后续操作
                if (service.IsInitialized)
                {
                    Logger.LogInfo("微信管理器已在其他窗口初始化，直接完成后续操作");
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // 微信管理器初始化完成后，初始化窗口关闭处理器
                            InitializeCloseHandler();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"初始化窗口关闭处理器失败: {ex.Message}", ex);
                        }
                    }, DispatcherPriority.Normal);
                }
                else
                {
                    // 初始化微信管理器（如果还没初始化）
                    service.InitializeAsync(Dispatcher, (log) =>
                    {
                        Logger.LogInfo(log);
                    });
                    
                    // 等待初始化完成后，初始化窗口关闭处理器
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // 延迟一点时间，确保初始化完成
                            Task.Delay(500).ContinueWith(_ =>
                            {
                                Dispatcher.InvokeAsync(() =>
                                {
                                    InitializeCloseHandler();
                                });
                            });
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"初始化窗口关闭处理器失败: {ex.Message}", ex);
                        }
                    }, DispatcherPriority.Normal);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"初始化微信管理器失败: {ex.Message}", ex);
                // 使用 InvokeAsync 避免阻塞，使用低优先级确保不阻塞UI
                _ = Dispatcher.InvokeAsync(() =>
                {
                    ShowError($"初始化微信管理器失败: {ex.Message}");
                }, DispatcherPriority.Background);
            }
        }
        
        /// <summary>
        /// 发送验证码消息给自己（已废弃，不再使用）
        /// </summary>
        private Task SendVerificationCodeToSelf(string verificationCode)
        {
            try
            {
                string? currentWxid = WeChatInitializationService.Instance.WeChatManager?.CurrentWxid;
                if (string.IsNullOrEmpty(currentWxid))
                {
                    Logger.LogWarning("未获取到微信ID，无法发送验证码消息给自己");
                    return Task.CompletedTask;
                }
                
                if (_commandService == null)
                {
                    Logger.LogWarning("命令服务未初始化，无法发送验证码消息");
                    return Task.CompletedTask;
                }
                
                string message = $"您的验证码是：{verificationCode}，请勿泄露给他人。";
                
                var cmdData = new
                {
                    to_wxid = currentWxid,
                    content = message
                };
                
                // 通过CommandService发送消息
                string commandJson = JsonConvert.SerializeObject(new
                {
                    command_type = "send_message",
                    command_data = JsonConvert.SerializeObject(cmdData),
                    target_wechat_id = currentWxid
                });
                
                bool result = _commandService.ProcessCommand(commandJson);
                
                if (result)
                {
                    Logger.LogInfo($"验证码消息已发送给自己: {currentWxid}");
                }
                else
                {
                    Logger.LogWarning("发送验证码消息给自己失败");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送验证码消息给自己失败: {ex.Message}", ex);
            }
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// 初始化窗口关闭处理器
        /// </summary>
        private void InitializeCloseHandler()
        {
            // 使用全局单例服务
            var service = WeChatInitializationService.Instance;
            var config = new UnifiedWindowCloseService.CleanupConfig
            {
                WeChatManager = service.WeChatManager,
                WebSocketService = _webSocketService,
                StopAllTimersCallback = StopAllTimers,
                UnsubscribeEventsCallback = UnsubscribeEvents,
                CleanupSyncServicesCallback = null, // LoginWindow 没有同步服务
                ClearAccountListCallback = null // LoginWindow 没有账号列表
            };

            _unifiedCloseService = new UnifiedWindowCloseService(this, CloseOverlay, config);
            
            // 设置最小化到托盘的回调
            // 注意：LoginWindow 通常不需要托盘图标，因为登录成功后会自动关闭
            // 但如果需要，可以在这里初始化 TrayIconService
            _unifiedCloseService.MinimizeToTrayCallback = () =>
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            };
            
            // 初始化完成后，允许用户关闭窗口（显示关闭确认弹窗）
            _startupProgressClosed = true;
        }

        /// <summary>
        /// 停止所有定时器
        /// </summary>
        private void StopAllTimers()
        {
            try
            {
                // 注意：不停止全局服务的定时器，因为主页还要用
                // 全局服务的定时器会在应用退出时统一停止
                // 注意：定时器管理的详细信息是内部实现细节，不再输出日志
            }
            catch (Exception ex)
            {
                Logger.LogError($"停止定时器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 取消事件订阅
        /// </summary>
        private void UnsubscribeEvents()
        {
            // 注意：只取消登录窗口自己的事件订阅，不取消全局服务的事件订阅
            // 因为主页需要继续监听微信消息，全局服务的事件订阅必须保持活跃
            
            // 取消全局服务的事件订阅（登录窗口自己的订阅）
            var service = WeChatInitializationService.Instance;
            if (_connectionStateChangedHandler != null)
            {
                service.OnConnectionStateChanged -= _connectionStateChangedHandler;
                _connectionStateChangedHandler = null;
            }
            
            if (_wxidReceivedHandler != null)
            {
                service.OnWxidReceived -= _wxidReceivedHandler;
                _wxidReceivedHandler = null;
            }
            
            if (_messageReceivedHandler != null)
            {
                service.OnMessageReceived -= _messageReceivedHandler;
                _messageReceivedHandler = null;
            }
            
            // 取消微信启动进度事件订阅
            var hookManager = GetWeChatHookManager();
            if (hookManager != null && _weChatProgressHandler != null)
            {
                hookManager.OnProgressUpdate -= _weChatProgressHandler;
                _weChatProgressHandler = null;
            }
            
            // 取消WebSocket服务事件订阅
            if (_webSocketService != null)
            {
                _webSocketService.OnMessageReceived -= OnWebSocketMessageReceived;
                // LoginWindow 可能没有订阅 OnConnectionStateChanged 事件
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // 如果是强制关闭（登录成功后），直接关闭，不显示确认对话框
            if (_forceClose)
            {
                e.Cancel = false;
                Logger.LogInfo("登录窗口：强制关闭，直接关闭窗口");
                return;
            }
            
            // 如果启动进度圆环还在显示，说明初始化未完成，阻止关闭
            if (!_startupProgressClosed)
            {
                Logger.LogWarning("登录窗口：初始化未完成，阻止关闭窗口");
                e.Cancel = true;
                return;
            }
            
            // 如果统一关闭服务已初始化，使用它处理关闭
            if (_unifiedCloseService != null)
            {
                Logger.LogInfo("登录窗口：使用统一关闭服务处理关闭");
                _unifiedCloseService.HandleClosing(e);
            }
            else
            {
                // 如果关闭服务未初始化，直接关闭窗口
                Logger.LogInfo("登录窗口：关闭服务未初始化，直接关闭窗口");
                e.Cancel = false;
            }
        }

        /// <summary>
        /// 处理微信消息（用于处理1112消息并保存到数据库）
        /// </summary>
        private void HandleWeChatMessageForAccountInfo(string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                // 清理消息
                string cleanMessage = message.Trim();
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
                    Logger.LogWarning($"JSON解析失败: {ex.Message}");
                    return;
                }

                if (messageObj == null) return;

                // 获取消息类型
                int messageType = 0;
                if (messageObj.type != null)
                {
                    int.TryParse(messageObj.type.ToString(), out messageType);
                }

                // 只处理1112消息（账号信息回调）
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
                            // 解析账号信息
                            string nickname = loginInfo.nickname?.ToString() ?? "";
                            string avatar = loginInfo.avatar?.ToString() ?? "";
                            string account = loginInfo.account?.ToString() ?? wxid;
                            string deviceId = loginInfo.device_id?.ToString() ?? loginInfo.deviceId?.ToString() ?? "";
                            string phone = loginInfo.phone?.ToString() ?? "";
                            string wxUserDir = loginInfo.wx_user_dir?.ToString() ?? loginInfo.wxUserDir?.ToString() ?? "";
                            int unreadMsgCount = 0;
                            int.TryParse(loginInfo.unread_msg_count?.ToString() ?? loginInfo.unreadMsgCount?.ToString() ?? "0", out unreadMsgCount);
                            int isFakeDeviceId = 0;
                            int.TryParse(loginInfo.is_fake_device_id?.ToString() ?? loginInfo.isFakeDeviceId?.ToString() ?? "0", out isFakeDeviceId);
                            int pid = 0;
                            int.TryParse(loginInfo.pid?.ToString() ?? "0", out pid);

                            // 创建AccountInfo对象
                            var accountInfo = new AccountInfo
                            {
                                WeChatId = wxid,
                                NickName = nickname,
                                Avatar = avatar,
                                BoundAccount = account,
                                Phone = phone,
                                DeviceId = deviceId,
                                WxUserDir = wxUserDir,
                                UnreadMsgCount = unreadMsgCount,
                                IsFakeDeviceId = isFakeDeviceId,
                                Pid = pid
                            };

                            Logger.LogInfo($"登录窗口收到1112消息，准备保存到数据库: wxid={wxid}, nickname={nickname}");

                            // 保存到数据库（通过WebSocket同步到服务器）
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(500); // 延迟一点时间，确保WebSocket连接稳定
                                SyncMyInfoToServer(accountInfo);
                            });
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
                if (string.IsNullOrEmpty(accountInfo.WeChatId) || int.TryParse(accountInfo.WeChatId, out _))
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

                Logger.LogInfo($"同步账号信息到服务器: wxid={accountInfo.WeChatId}, nickname={accountInfo.NickName}");

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

                _ = _webSocketService.SendMessageAsync(syncData);
                Logger.LogInfo($"账号信息已同步到服务器: wxid={accountInfo.WeChatId}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步账号信息到服务器失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 加载记住的账号信息
        /// </summary>
        private async Task LoadRememberedAccountAsync()
        {
            try
            {
                if (File.Exists(_rememberedAccountFilePath))
                {
                    string json = await Task.Run(() => File.ReadAllText(_rememberedAccountFilePath, Encoding.UTF8));
                    var rememberedAccount = JsonConvert.DeserializeObject<RememberedAccount>(json);
                    
                    if (rememberedAccount != null && !string.IsNullOrEmpty(rememberedAccount.Phone) && !string.IsNullOrEmpty(rememberedAccount.LicenseKey))
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            PhoneTextBox.Text = rememberedAccount.Phone;
                            LicenseKeyTextBox.Text = rememberedAccount.LicenseKey;
                            RememberAccountCheckBox.IsChecked = true;
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"加载记住的账号失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 保存记住的账号信息
        /// </summary>
        private async Task SaveRememberedAccountAsync(string phone, string licenseKey)
        {
            try
            {
                var rememberedAccount = new RememberedAccount
                {
                    Phone = phone,
                    LicenseKey = licenseKey,
                    SaveTime = DateTime.Now
                };
                
                string json = JsonConvert.SerializeObject(rememberedAccount, Formatting.Indented);
                await File.WriteAllTextAsync(_rememberedAccountFilePath, json, Encoding.UTF8);
                Logger.LogInfo($"记住的账号已保存: {phone}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"保存记住的账号失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 删除记住的账号信息
        /// </summary>
        private async Task DeleteRememberedAccountAsync()
        {
            try
            {
                if (File.Exists(_rememberedAccountFilePath))
                {
                    await Task.Run(() => File.Delete(_rememberedAccountFilePath));
                    Logger.LogInfo("记住的账号已删除");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"删除记住的账号失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 记住的账号数据模型
        /// </summary>
        private class RememberedAccount
        {
            public string Phone { get; set; } = "";
            public string LicenseKey { get; set; } = "";
            public DateTime SaveTime { get; set; }
        }

        /// <summary>
        /// 记住账号复选框选中事件
        /// </summary>
        private void RememberAccountCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            // 如果当前有输入账号密码，立即保存
            string phone = PhoneTextBox.Text.Trim();
            string licenseKey = LicenseKeyTextBox.Text.Trim();
            
            if (!string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(licenseKey))
            {
                _ = SaveRememberedAccountAsync(phone, licenseKey);
            }
        }

        /// <summary>
        /// 记住账号复选框取消选中事件
        /// </summary>
        private void RememberAccountCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // 删除保存的账号
            _ = DeleteRememberedAccountAsync();
        }

        /// <summary>
        /// 窗口已关闭事件（窗口关闭后执行）
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // 最终清理（确保所有引用都被清空）
            try
            {
                Logger.LogInfo("========== 最终清理资源（登录窗口） ==========");
                
                // 注意：不要释放全局微信初始化服务，因为主页还要用
                // 全局服务会在应用退出时统一释放
                
                _webSocketService = null;
                _commandService = null;
                
                // 登录页没有托盘图标，不需要释放
                // _trayIconService?.Dispose();
                // _trayIconService = null;
                
                Logger.LogInfo("登录窗口已关闭，所有资源已清理");
            }
            catch (Exception ex)
            {
                Logger.LogError($"登录窗口关闭后清理时出错: {ex.Message}", ex);
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}
