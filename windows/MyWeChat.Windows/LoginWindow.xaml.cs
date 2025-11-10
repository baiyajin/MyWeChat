using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyWeChat.Windows.Models;
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
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理WebSocket消息失败: {ex.Message}", ex);
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
                MessageBox.Show("验证码已发送到您的微信，请查收", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
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
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
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
            if (sender is TextBox textBox && textBox.Parent is Border border)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07C160"));
            }
        }

        /// <summary>
        /// 手机号输入框失去焦点
        /// </summary>
        private void PhoneTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Parent is Border border)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
            }
        }

        /// <summary>
        /// 验证码输入框获得焦点
        /// </summary>
        private void CodeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Parent is Border border)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#07C160"));
            }
        }

        /// <summary>
        /// 验证码输入框失去焦点
        /// </summary>
        private void CodeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Parent is Border border)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5E5"));
            }
        }

        /// <summary>
        /// 窗口关闭事件
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _webSocketService?.Disconnect();
            base.OnClosed(e);
        }
    }
}
