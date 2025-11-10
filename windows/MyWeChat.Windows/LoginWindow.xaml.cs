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
        private WeChatConnectionManager? _connectionManager;
        private CommandService? _commandService;
        private bool _isWeChatConnected = false;
        private string? _currentWxid = null; // 当前获取到的微信ID
        private DispatcherTimer? _weChatProcessCheckTimer; // 微信进程检测定时器

        public LoginWindow()
        {
            InitializeComponent();
            _serverUrl = ConfigHelper.GetServerUrl();
            _loginHistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login_history.json");
            
            Loaded += LoginWindow_Loaded;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 加载登录历史
            LoadLoginHistory();
            
            // 初始化WebSocket连接
            InitializeWebSocket();
            
            // 初始化微信连接管理器
            InitializeWeChatConnection();
            
            // 启动微信进程检测定时器
            StartWeChatProcessCheckTimer();
        }

        /// <summary>
        /// 初始化WebSocket连接
        /// </summary>
        private async void InitializeWebSocket()
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

                Dispatcher.Invoke(() =>
                {
                    switch (messageType)
                    {
                        case "login_response":
                            HandleLoginResponse(messageObj);
                            break;
                        case "verify_login_code_response":
                            HandleVerifyLoginCodeResponse(messageObj);
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
                        if (!string.IsNullOrEmpty(targetWxid) && targetWxid == _currentWxid && !string.IsNullOrEmpty(content))
                        {
                            Logger.LogInfo($"收到发送给自己的消息命令: {content}");
                            // 这里可以显示验证码消息，或者直接通过CommandService发送
                            if (_commandService != null && _connectionManager != null && _connectionManager.IsConnected)
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
                // 使用ErrorTextBlock显示成功提示（绿色）
                ShowSuccess("验证码已发送到您的微信，请查收");
                
                // 注意：服务器端会通过命令发送验证码消息给微信
                // 如果LoginWindow已经连接了微信并获取到了wxid，可以额外发送一条验证码消息给自己（作为备用）
                // 但验证码是在服务器端生成的，LoginWindow无法获取到
                // 所以这里只显示成功提示，验证码消息由服务器端通过命令发送
                Logger.LogInfo("登录请求成功，等待服务器端发送验证码消息");
            }
            else
            {
                ShowError(message);
            }
        }

        /// <summary>
        /// 处理验证登录码响应
        /// </summary>
        private void HandleVerifyLoginCodeResponse(dynamic? response)
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
        /// 获取验证码按钮点击事件
        /// </summary>
        private async void GetCodeButton_Click(object sender, RoutedEventArgs e)
        {
            string phone = PhoneTextBox.Text.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                ShowError("请输入手机号");
                return;
            }

            GetCodeButton.IsEnabled = false;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                // 通过WebSocket发送登录请求
                if (_webSocketService != null && _webSocketService.IsConnected)
                {
                    await _webSocketService.SendMessageAsync(new
                    {
                        type = "login",
                        phone = phone
                    });
                }
                else
                {
                    ShowError("WebSocket未连接，请稍后重试");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"请求验证码失败: {ex.Message}", ex);
                ShowError($"请求验证码失败: {ex.Message}");
            }
            finally
            {
                GetCodeButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// 登录按钮点击事件
        /// </summary>
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string phone = PhoneTextBox.Text.Trim();
            string code = CodeTextBox.Text.Trim();

            if (string.IsNullOrEmpty(phone))
            {
                ShowError("请输入手机号");
                return;
            }

            if (string.IsNullOrEmpty(code))
            {
                ShowError("请输入验证码");
                return;
            }

            LoginButton.IsEnabled = false;
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            try
            {
                // 通过WebSocket发送验证登录码请求
                if (_webSocketService != null && _webSocketService.IsConnected)
                {
                    await _webSocketService.SendMessageAsync(new
                    {
                        type = "verify_login_code",
                        phone = phone,
                        code = code
                    });
                }
                else
                {
                    ShowError("WebSocket未连接，请稍后重试");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"验证登录码失败: {ex.Message}", ex);
                ShowError($"验证登录码失败: {ex.Message}");
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
                ErrorTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07C160"));
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
            try
            {
                if (File.Exists(_loginHistoryFilePath))
                {
                    string json = File.ReadAllText(_loginHistoryFilePath, Encoding.UTF8);
                    var history = JsonConvert.DeserializeObject<List<AccountInfo>>(json);
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
            if (sender is TextBox textBox)
            {
                // 找到父级Border
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07C160"));
                }
            }
        }

        /// <summary>
        /// 手机号输入框失去焦点
        /// </summary>
        private void PhoneTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
                }
            }
        }

        /// <summary>
        /// 验证码输入框获得焦点
        /// </summary>
        private void CodeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07C160"));
                }
            }
        }

        /// <summary>
        /// 验证码输入框失去焦点
        /// </summary>
        private void CodeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                Border? border = FindParent<Border>(textBox);
                if (border != null)
                {
                    border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
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
        /// 初始化微信连接管理器
        /// </summary>
        private void InitializeWeChatConnection()
        {
            try
            {
                _connectionManager = new WeChatConnectionManager();
                
                // 订阅连接状态变化事件
                _connectionManager.OnConnectionStateChanged += (sender, isConnected) =>
                {
                    _isWeChatConnected = isConnected;
                    if (isConnected)
                    {
                        Logger.LogInfo("微信连接成功（登录窗口）");
                        Dispatcher.Invoke(() =>
                        {
                            ShowSuccess("微信连接成功");
                        });
                    }
                    else
                    {
                        Logger.LogWarning("微信连接断开（登录窗口）");
                        Dispatcher.Invoke(() =>
                        {
                            ShowError("微信连接断开，请检查微信是否正常运行");
                        });
                    }
                };
                
                // 订阅消息接收事件（用于获取1112回调）
                _connectionManager.OnMessageReceived += OnWeChatMessageReceived;
                
                // 初始化连接管理器
                if (!_connectionManager.Initialize())
                {
                    Logger.LogError("微信连接管理器初始化失败（登录窗口）");
                    ShowError("微信连接管理器初始化失败");
                    return;
                }
                
                Logger.LogInfo("微信连接管理器初始化成功（登录窗口）");
            }
            catch (Exception ex)
            {
                Logger.LogError($"初始化微信连接管理器失败: {ex.Message}", ex);
                ShowError($"初始化微信连接管理器失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 处理微信消息（用于获取1112回调）
        /// </summary>
        private void OnWeChatMessageReceived(object? sender, string message)
        {
            try
            {
                var messageObj = JsonConvert.DeserializeObject<dynamic>(message);
                if (messageObj == null) return;
                
                // 获取消息类型
                int messageType = 0;
                if (messageObj.type != null)
                {
                    int.TryParse(messageObj.type.ToString(), out messageType);
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
                            _currentWxid = wxid;
                            Logger.LogInfo($"获取到微信ID（登录窗口）: {wxid}");
                            
                            // 初始化命令服务
                            if (_connectionManager != null && _commandService == null)
                            {
                                _commandService = new CommandService(_connectionManager);
                            }
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
        /// 启动微信进程检测定时器
        /// </summary>
        private void StartWeChatProcessCheckTimer()
        {
            try
            {
                _weChatProcessCheckTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _weChatProcessCheckTimer.Tick += WeChatProcessCheckTimer_Tick;
                _weChatProcessCheckTimer.Start();
                Logger.LogInfo("微信进程检测定时器已启动（登录窗口）");
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
                bool weChatRunning = IsWeChatProcessRunning();
                
                if (weChatRunning && !_isWeChatConnected)
                {
                    // 发现微信进程，但未连接，尝试连接
                    Logger.LogInfo("检测到微信进程，尝试连接（登录窗口）...");
                    
                    if (_connectionManager != null)
                    {
                        bool result = _connectionManager.Connect();
                        if (result)
                        {
                            _isWeChatConnected = true;
                            Logger.LogInfo("微信连接成功（登录窗口）");
                        }
                    }
                }
                else if (!weChatRunning && !_isWeChatConnected)
                {
                    // 微信进程未运行，自动启动微信
                    Logger.LogInfo("微信进程未运行，正在自动启动微信（登录窗口）...");
                    
                    if (_connectionManager != null)
                    {
                        try
                        {
                            bool result = _connectionManager.Connect();
                            if (result)
                            {
                                _isWeChatConnected = true;
                                Logger.LogInfo("微信自动启动成功（登录窗口）");
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
        /// 检查微信进程是否运行
        /// </summary>
        private bool IsWeChatProcessRunning()
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
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 发送验证码消息给自己
        /// </summary>
        private async Task SendVerificationCodeToSelf(string verificationCode)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentWxid))
                {
                    Logger.LogWarning("未获取到微信ID，无法发送验证码消息给自己");
                    return;
                }
                
                if (_commandService == null)
                {
                    Logger.LogWarning("命令服务未初始化，无法发送验证码消息");
                    return;
                }
                
                string message = $"您的验证码是：{verificationCode}，请勿泄露给他人。";
                
                var cmdData = new
                {
                    to_wxid = _currentWxid,
                    content = message
                };
                
                // 通过CommandService发送消息
                string commandJson = JsonConvert.SerializeObject(new
                {
                    command_type = "send_message",
                    command_data = JsonConvert.SerializeObject(cmdData),
                    target_wechat_id = _currentWxid
                });
                
                bool result = _commandService.ProcessCommand(commandJson);
                
                if (result)
                {
                    Logger.LogInfo($"验证码消息已发送给自己: {_currentWxid}");
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
        }
        
        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // 停止定时器
            if (_weChatProcessCheckTimer != null)
            {
                _weChatProcessCheckTimer.Stop();
                _weChatProcessCheckTimer = null;
            }
            
            // 断开微信连接
            if (_connectionManager != null && _connectionManager.IsConnected)
            {
                _connectionManager.Disconnect();
            }
            
            // 断开WebSocket连接
            if (_webSocketService != null)
            {
                // 异步断开连接，不等待完成
                _ = _webSocketService.DisconnectAsync();
            }
            
            base.OnClosed(e);
        }
    }
}
