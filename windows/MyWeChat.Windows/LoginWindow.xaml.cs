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
        private string _loginHistoryFilePath;
        
        // 微信连接相关
        private WeChatManager? _weChatManager;
        private CommandService? _commandService;

        // 窗口关闭处理器
        private WindowCloseHandler? _closeHandler;
        
        // 防止TextChanged事件递归调用的标志
        private bool _isUpdatingPhoneText = false;
        private bool _isUpdatingLicenseKeyText = false;

        public LoginWindow()
        {
            InitializeComponent();
            // 设置窗口标题为"w"
            this.Title = "w";
            _serverUrl = ConfigHelper.GetServerUrl();
            _loginHistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_history.json");
            
            // 在构造函数中立即设置输入框属性，确保窗口创建时即可使用
            PhoneTextBox.IsEnabled = true;
            LicenseKeyTextBox.IsEnabled = true;
            PhoneTextBox.Focusable = true;
            LicenseKeyTextBox.Focusable = true;
            PhoneTextBox.IsReadOnly = false;
            LicenseKeyTextBox.IsReadOnly = false;
            PhoneTextBox.IsHitTestVisible = true;
            LicenseKeyTextBox.IsHitTestVisible = true;
            
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 确保窗口标题为"w"（在Loaded事件中再次设置，确保覆盖任何默认值）
            this.Title = "w";
            
            // 确保输入框可以立即使用（必须在UI线程上设置）
            // 这些设置必须在任何异步操作之前完成，确保UI立即可用
            PhoneTextBox.IsEnabled = true;
            LicenseKeyTextBox.IsEnabled = true;
            PhoneTextBox.Focusable = true;
            LicenseKeyTextBox.Focusable = true;
            PhoneTextBox.IsReadOnly = false;
            LicenseKeyTextBox.IsReadOnly = false;
            PhoneTextBox.IsHitTestVisible = true;
            LicenseKeyTextBox.IsHitTestVisible = true;
            
            // 确保Canvas遮罩层不会阻挡输入
            ClosingOverlayCanvas.IsHitTestVisible = false;
            ClosingOverlayCanvas.IsEnabled = false;
            
            // 异步加载登录历史（不阻塞UI）
            _ = Task.Run(() => LoadLoginHistoryAsync());
            
            // 延迟初始化，确保UI完全加载后再开始后台任务
            // 使用更长的延迟，确保UI完全渲染完成
            _ = Task.Delay(300).ContinueWith(_ =>
            {
                // 异步初始化WebSocket连接（不阻塞UI）
                _ = Task.Run(async () => await InitializeWebSocketAsync());
                
                // 异步初始化微信管理器（完全在后台线程，不阻塞UI）
                _ = Task.Run(() => InitializeWeChatManagerAsync());
            }, TaskScheduler.Default);
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
                               string? currentWxid = _weChatManager?.CurrentWxid;
                               if (!string.IsNullOrEmpty(targetWxid) && targetWxid == currentWxid && !string.IsNullOrEmpty(content))
                               {
                                   Logger.LogInfo($"收到发送给自己的消息命令: {content}");
                                   // 这里可以显示验证码消息，或者直接通过CommandService发送
                                   if (_commandService != null && _weChatManager?.ConnectionManager != null && _weChatManager.IsConnected)
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
                
                // 打开主窗口（使用空字符串作为wxid，主窗口会从服务器获取）
                var mainWindow = new MainWindow("");
                    mainWindow.Show();
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

                    // 保存登录状态
                    SaveLoginState(accountInfo.WeChatId);
                    
                    // 保存登录历史
                    SaveLoginHistory(accountInfo);

                    // 打开主窗口
                    var mainWindow = new MainWindow(accountInfo.WeChatId);
                    mainWindow.Show();
                    this.Close();
                }
            }
            else
            {
                ShowError(message);
            }
        }

        /// <summary>
        /// 登录按钮点击事件（手机号+授权码）
        /// </summary>
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
                // 通过WebSocket发送登录请求（手机号+授权码）
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

        /// <summary>
        /// 保存登录状态
        /// </summary>
        private void SaveLoginState(string wxid)
        {
            try
            {
                string stateFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_state.json");
                var state = new { wxid = wxid, loginTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                string json = JsonConvert.SerializeObject(state, Formatting.Indented);
                File.WriteAllText(stateFilePath, json, Encoding.UTF8);
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
                            _loginHistory = history;
                            HistoryItemsControl.ItemsSource = _loginHistory;
                            
                            // 显示/隐藏登录历史标题
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

        /// <summary>
        /// 保存登录历史
        /// </summary>
        private void SaveLoginHistory(AccountInfo accountInfo)
        {
            try
            {
                // 移除已存在的相同wxid的记录
                _loginHistory.RemoveAll(a => a.WeChatId == accountInfo.WeChatId);
                
                // 添加到最前面
                _loginHistory.Insert(0, accountInfo);
                
                // 最多保留10条历史记录
                if (_loginHistory.Count > 10)
                {
                    _loginHistory = _loginHistory.Take(10).ToList();
                }
                
                // 保存到文件
                string json = JsonConvert.SerializeObject(_loginHistory, Formatting.Indented);
                File.WriteAllText(_loginHistoryFilePath, json, Encoding.UTF8);
                
                // 更新UI
                HistoryItemsControl.ItemsSource = null;
                HistoryItemsControl.ItemsSource = _loginHistory;
                
                // 显示/隐藏登录历史标题
                if (HistoryTitleTextBlock != null)
                {
                    HistoryTitleTextBlock.Visibility = _loginHistory.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                
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

        /// <summary>
        /// 授权码输入框获得焦点
        /// </summary>
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

        /// <summary>
        /// 授权码输入框失去焦点
        /// </summary>
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
        /// 授权码输入框文本变化事件 - 自动去除空格
        /// </summary>
        private void LicenseKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingLicenseKeyText) return; // 防止递归调用
            
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                string originalText = textBox.Text;
                // 去除所有空格
                string filteredText = originalText.Replace(" ", "").Replace("\t", "").Replace("\n", "").Replace("\r", "");
                
                // 如果文本被过滤了，更新文本框（避免光标位置问题）
                if (originalText != filteredText)
                {
                    _isUpdatingLicenseKeyText = true;
                    try
                    {
                        int selectionStart = textBox.SelectionStart;
                        int removedSpaces = originalText.Length - filteredText.Length;
                        textBox.Text = filteredText;
                        // 调整光标位置（减去被删除的空格数量）
                        textBox.SelectionStart = Math.Max(0, Math.Min(selectionStart - removedSpaces, filteredText.Length));
                        textBox.SelectionLength = 0;
                    }
                    finally
                    {
                        _isUpdatingLicenseKeyText = false;
                    }
                }
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
        /// </summary>
        private void InitializeWeChatManagerAsync()
        {
            try
            {
                // 在后台线程创建微信管理器
                // 注意：WeChatManager的初始化操作（如版本检测）在后台线程执行
                var weChatManager = new WeChatManager(Dispatcher);
                
                // 订阅连接状态变化事件
                weChatManager.OnConnectionStateChanged += (sender, isConnected) =>
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
                weChatManager.OnWxidReceived += (sender, wxid) =>
                {
                    Logger.LogInfo($"获取到微信ID（登录窗口）: {wxid}");
                    
                    // 初始化命令服务（在UI线程上执行，使用低优先级确保不阻塞UI）
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        if (weChatManager?.ConnectionManager != null && _commandService == null)
                        {
                            _commandService = new CommandService(weChatManager.ConnectionManager);
                        }
                    }, DispatcherPriority.Background);
                };
                
                // 初始化微信管理器（在后台线程执行，包含文件系统操作）
                // 这些操作不会阻塞UI线程，因为它们完全在后台线程执行
                if (!weChatManager.Initialize())
                {
                    Logger.LogError("微信管理器初始化失败（登录窗口）");
                    // 使用 InvokeAsync 避免阻塞，使用低优先级确保不阻塞UI
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        ShowError("微信管理器初始化失败");
                    }, DispatcherPriority.Background);
                    return;
                }
                
                // 保存引用（在后台线程上保存，线程安全）
                _weChatManager = weChatManager;
                
                // 启动进程检测定时器（必须在UI线程上执行，因为DispatcherTimer必须在UI线程创建）
                // 使用正常优先级，因为这是必要的初始化操作
                _ = Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _weChatManager?.StartProcessCheckTimer();
                        
                        // 微信管理器初始化完成后，初始化窗口关闭处理器
                        InitializeCloseHandler();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"启动进程检测定时器失败: {ex.Message}", ex);
                    }
                }, DispatcherPriority.Normal);
                
                Logger.LogInfo("微信管理器初始化成功（登录窗口）");
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
                string? currentWxid = _weChatManager?.CurrentWxid;
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
            var config = new WindowCloseHandler.CleanupConfig
            {
                WeChatManager = _weChatManager,
                WebSocketService = _webSocketService,
                StopAllTimersCallback = StopAllTimers,
                UnsubscribeEventsCallback = UnsubscribeEvents,
                CleanupSyncServicesCallback = null, // LoginWindow 没有同步服务
                ClearAccountListCallback = null, // LoginWindow 没有账号列表
                UpdateProgressCallback = UpdateClosingProgress,
                ShowProgressOverlayCallback = ShowProgressOverlay
            };

            _closeHandler = new WindowCloseHandler(this, config);
        }

        /// <summary>
        /// 显示/隐藏进度遮罩
        /// </summary>
        private void ShowProgressOverlay(bool show)
        {
            if (show)
            {
                ClosingOverlayCanvas.Visibility = Visibility.Visible;
                ClosingOverlayCanvas.IsHitTestVisible = true; // 显示时启用鼠标事件
                ClosingOverlayCanvas.IsEnabled = true; // 显示时启用
                UpdateClosingProgressRing(0);
                ClosingStatusText.Text = "准备关闭...";
                ClosingProgressText.Text = "0%";

                // 居中显示遮罩内容
                if (ClosingOverlayBorder != null)
                {
                    ClosingOverlayCanvas.UpdateLayout();
                    double canvasWidth = ClosingOverlayCanvas.ActualWidth;
                    double canvasHeight = ClosingOverlayCanvas.ActualHeight;
                    double borderWidth = 400;
                    double borderHeight = 280;

                    Canvas.SetLeft(ClosingOverlayBorder, (canvasWidth - borderWidth) / 2);
                    Canvas.SetTop(ClosingOverlayBorder, (canvasHeight - borderHeight) / 2);
                }
            }
            else
            {
                ClosingOverlayCanvas.Visibility = Visibility.Collapsed;
                ClosingOverlayCanvas.IsHitTestVisible = false; // 隐藏时禁用鼠标事件
                ClosingOverlayCanvas.IsEnabled = false; // 隐藏时禁用
            }
        }

        /// <summary>
        /// 更新关闭进度
        /// </summary>
        private void UpdateClosingProgress(int progress, string status)
        {
            UpdateClosingProgressRing(progress);
            ClosingStatusText.Text = status;
            ClosingProgressText.Text = $"{progress}%";
        }

        /// <summary>
        /// 更新关闭进度圆环
        /// </summary>
        private void UpdateClosingProgressRing(int progress)
        {
            try
            {
                if (ClosingProgressArc == null) return;
                
                // 确保进度在0-100范围内
                progress = Math.Max(0, Math.Min(100, progress));
                
                // 计算角度（0度在顶部，顺时针）
                double angle = (progress / 100.0) * 360.0;
                double angleRad = (angle - 90) * Math.PI / 180.0; // 转换为弧度，-90度使起点在顶部
                
                // 圆环中心 (60, 60)，半径 50
                double centerX = 60;
                double centerY = 60;
                double radius = 50;
                
                // 计算终点坐标
                double endX = centerX + radius * Math.Cos(angleRad);
                double endY = centerY + radius * Math.Sin(angleRad);
                
                // 判断是否需要大弧（超过180度）
                bool isLargeArc = progress > 50;
                
                // 更新ArcSegment
                ClosingProgressArc.Point = new System.Windows.Point(endX, endY);
                ClosingProgressArc.IsLargeArc = isLargeArc;
                ClosingProgressArc.Size = new System.Windows.Size(radius, radius);
            }
            catch (Exception ex)
        {
                Logger.LogError($"更新关闭进度圆环失败: {ex.Message}", ex);
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
                    Logger.LogInfo("已停止微信进程检测定时器（登录窗口）");
                }
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
            // 最终清理（确保所有引用都被清空）
            try
            {
                Logger.LogInfo("========== 最终清理资源（登录窗口） ==========");
                
                // 清空所有服务引用
                _weChatManager?.Dispose();
                _weChatManager = null;
                _webSocketService = null;
                _commandService = null;
                
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
