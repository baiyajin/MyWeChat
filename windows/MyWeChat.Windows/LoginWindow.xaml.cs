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
        
        // 微信连接相关（使用全局单例服务）
        private CommandService? _commandService;

        // 统一窗口关闭服务
        private UnifiedWindowCloseService? _unifiedCloseService;
        
        // 强制关闭标志（登录成功后不显示关闭确认对话框）
        private bool _forceClose = false;
        
        private bool _isUpdatingPhoneText = false;
        
        // 启动进度相关
        private int _currentStep = 0;
        private const int _totalSteps = 3; // 总步骤数：1.加载登录历史 2.初始化WebSocket 3.初始化微信管理器

        public LoginWindow()
        {
            InitializeComponent();
            this.Title = "w";
            _serverUrl = ConfigHelper.GetServerUrl();
            _loginHistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_history.json");
            
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
            
            // 注意：CloseOverlay 控件在初始化时会自动设置 IsHitTestVisible 和 IsEnabled
            // 这里不需要手动设置，因为遮罩默认是隐藏的
            
            UpdateLoadingStatus(1, "正在加载登录历史...");
            
            _ = Task.Run(async () =>
            {
                await LoadLoginHistoryAsync();
                Dispatcher.Invoke(() => UpdateLoadingStatus(2, "正在初始化WebSocket连接..."));
                
                await Task.Delay(300);
                
                await InitializeWebSocketAsync();
                Dispatcher.Invoke(() => UpdateLoadingStatus(3, "正在初始化微信管理器..."));
                
                await Task.Run(() => InitializeWeChatManagerAsync());
                
                Dispatcher.Invoke(() =>
                {
                    UpdateLoadingStatus(_totalSteps, "初始化完成");
                    Task.Delay(2000).ContinueWith(_ =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var loadingBorder = FindName("LoadingStatusBorder") as Border;
                            if (loadingBorder != null)
                            {
                                loadingBorder.Visibility = Visibility.Collapsed;
                            }
                        });
                    });
                });
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
                    Logger.LogInfo("WebSocket连接成功（登录窗口）");
                }
                else
                {
                    Logger.LogWarning("WebSocket连接失败（登录窗口）");
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

            LoginButton.IsEnabled = false;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            try
            {
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
                string stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_state.json");
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
        /// 更新加载状态
        /// </summary>
        private void UpdateLoadingStatus(int step, string message)
        {
            _currentStep = step;
            if (LoadingStatusText != null)
            {
                LoadingStatusText.Text = $"{message} ({step}/{_totalSteps})";
            }
            
            // 根据进度状态切换图标显示
            bool isCompleted = step >= _totalSteps;
            
            if (LoadingIndicator != null)
            {
                LoadingIndicator.Visibility = isCompleted ? Visibility.Collapsed : Visibility.Visible;
            }
            
            if (CompletedIndicator != null)
            {
                CompletedIndicator.Visibility = isCompleted ? Visibility.Visible : Visibility.Collapsed;
            }
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
                
                // 订阅连接状态变化事件
                service.OnConnectionStateChanged += (sender, isConnected) =>
                {
                    if (isConnected)
                    {
                        Logger.LogInfo("微信连接成功（登录窗口）");
                        // 使用 InvokeAsync 避免阻塞，使用低优先级确保不阻塞UI
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            ShowSuccess("微信连接成功");
                        }, DispatcherPriority.Background);
                    }
                    else
                    {
                        Logger.LogWarning("微信连接断开（登录窗口）");
                        // 使用 InvokeAsync 避免阻塞，使用低优先级确保不阻塞UI
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            ShowError("微信连接断开，请检查微信是否正常运行");
                        }, DispatcherPriority.Background);
                    }
                };
                
                // 订阅微信ID获取事件（1112回调）
                service.OnWxidReceived += (sender, wxid) =>
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
            _unifiedCloseService.MinimizeToTrayCallback = () =>
            {
                this.WindowState = WindowState.Minimized;
                this.Hide();
            };
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
                Logger.LogInfo("登录窗口关闭，定时器由全局服务管理");
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
            // WeChatManager的事件订阅在Dispose时自动清理
            
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
                return;
            }
            
            // 如果统一关闭服务已初始化，使用它处理关闭
            if (_unifiedCloseService != null)
            {
                _unifiedCloseService.HandleClosing(e);
            }
            else
            {
                // 如果关闭服务未初始化，直接关闭窗口
                e.Cancel = false;
            }
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
