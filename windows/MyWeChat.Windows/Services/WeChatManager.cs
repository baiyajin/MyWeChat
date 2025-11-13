using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Utils;
using Newtonsoft.Json;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 微信管理器
    /// 封装微信进程检测、启动、连接管理等通用功能
    /// </summary>
    public class WeChatManager : IDisposable
    {
        private WeChatConnectionManager? _connectionManager;
        private DispatcherTimer? _processCheckTimer;
        private bool _isWeChatConnected = false;
        private string? _currentWxid = null;
        private bool _isDisposed = false;
        private readonly object _lock = new object();
        
        // 定时器检测间隔（秒）
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(3);
        
        // 窗口上下文（用于UI更新）
        private Dispatcher? _dispatcher;

        /// <summary>
        /// 是否已连接微信
        /// </summary>
        public bool IsConnected => _isWeChatConnected && _connectionManager != null && _connectionManager.IsConnected;

        /// <summary>
        /// 当前微信ID
        /// </summary>
        public string? CurrentWxid => _currentWxid;

        /// <summary>
        /// 微信连接管理器（供外部使用）
        /// </summary>
        public WeChatConnectionManager? ConnectionManager => _connectionManager;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// 微信ID获取事件（1112回调）
        /// </summary>
        public event EventHandler<string>? OnWxidReceived;

        /// <summary>
        /// 微信消息接收事件（所有消息，包括1112）
        /// </summary>
        public event EventHandler<string>? OnMessageReceived;

        /// <summary>
        /// 账号信息接收事件（1112回调，包含完整账号信息）
        /// </summary>
        public event EventHandler<AccountInfo>? OnAccountInfoReceived;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dispatcher">UI调度器（用于UI线程更新）</param>
        public WeChatManager(Dispatcher? dispatcher = null)
        {
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// 初始化微信管理器
        /// </summary>
        public bool Initialize()
        {
            lock (_lock)
            {
                if (_connectionManager != null)
                {
                    Logger.LogWarning("微信管理器已初始化，跳过重复初始化");
                    return true;
                }

                try
                {
                    _connectionManager = new WeChatConnectionManager();

                    // 订阅连接状态变化事件
                    _connectionManager.OnConnectionStateChanged += (sender, isConnected) =>
                    {
                        _isWeChatConnected = isConnected;
                        OnConnectionStateChanged?.Invoke(this, isConnected);
                    };

                    // 订阅消息接收事件（用于获取1112回调）
                    _connectionManager.OnMessageReceived += OnWeChatMessageReceived;

                    // 初始化连接管理器
                    if (!_connectionManager.Initialize())
                    {
                        Logger.LogError("微信连接管理器初始化失败");
                        _connectionManager = null;
                        return false;
                    }

                    Logger.LogInfo("微信管理器初始化成功");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"初始化微信管理器失败: {ex.Message}", ex);
                    _connectionManager = null;
                    return false;
                }
            }
        }

        /// <summary>
        /// 启动微信进程检测定时器
        /// </summary>
        public void StartProcessCheckTimer()
        {
            lock (_lock)
            {
                if (_processCheckTimer != null)
                {
                    Logger.LogWarning("微信进程检测定时器已启动，跳过重复启动");
                    return;
                }

                try
                {
                    _processCheckTimer = new DispatcherTimer
                    {
                        Interval = _checkInterval
                    };
                    _processCheckTimer.Tick += ProcessCheckTimer_Tick;
                    _processCheckTimer.Start();

                    Logger.LogInfo("微信进程检测定时器已启动");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"启动微信进程检测定时器失败: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// 停止微信进程检测定时器
        /// </summary>
        public void StopProcessCheckTimer()
        {
            lock (_lock)
            {
                if (_processCheckTimer != null)
                {
                    _processCheckTimer.Stop();
                    _processCheckTimer.Tick -= ProcessCheckTimer_Tick;
                    _processCheckTimer = null;
                    Logger.LogInfo("微信进程检测定时器已停止");
                }
            }
        }

        /// <summary>
        /// 手动连接微信
        /// </summary>
        /// <param name="autoStartWeChat">是否自动启动微信（如果微信未运行）。false表示不自动启动，仅检测已运行的微信</param>
        public bool Connect(bool autoStartWeChat = false)
        {
            lock (_lock)
            {
                if (_connectionManager == null)
                {
                    Logger.LogWarning("微信连接管理器未初始化，无法连接");
                    return false;
                }

                try
                {
                    bool result = _connectionManager.Connect(null, autoStartWeChat);
                    if (result)
                    {
                        _isWeChatConnected = true;
                        Logger.LogInfo("微信连接成功");
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"微信连接失败: {ex.Message}", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// 断开微信连接
        /// </summary>
        public void Disconnect()
        {
            lock (_lock)
            {
                if (_connectionManager != null && _connectionManager.IsConnected)
                {
                    _connectionManager.Disconnect();
                    _isWeChatConnected = false;
                    Logger.LogInfo("微信连接已断开");
                }
            }
        }

        /// <summary>
        /// 检查微信进程是否运行
        /// </summary>
        public bool IsWeChatProcessRunning()
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
            catch (Exception ex)
            {
                Logger.LogError($"检查微信进程失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 微信进程检测定时器事件
        /// </summary>
        private void ProcessCheckTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                bool weChatRunning = IsWeChatProcessRunning();

                if (weChatRunning && !_isWeChatConnected)
                {
                    // 发现微信进程，但未连接，尝试连接
                    Logger.LogInfo("检测到微信进程，尝试连接...");

                    if (_connectionManager != null)
                    {
                        bool result = _connectionManager.Connect();
                        if (result)
                        {
                            _isWeChatConnected = true;
                            Logger.LogInfo("微信连接成功");
                        }
                    }
                }
                else if (!weChatRunning && _isWeChatConnected)
                {
                    // 微信进程已退出，断开连接
                    Logger.LogInfo("微信进程已退出，断开连接");
                    _isWeChatConnected = false;
                    if (_connectionManager != null && _connectionManager.IsConnected)
                    {
                        _connectionManager.Disconnect();
                    }
                }
                // 注意：移除了自动启动微信的逻辑，改为手动触发（通过"登录微信"按钮）
            }
            catch (Exception ex)
            {
                Logger.LogError($"微信进程检测失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 智能修复JSON字符串，处理各种截断和不完整的情况
        /// </summary>
        /// <param name="json">需要修复的JSON字符串</param>
        /// <returns>修复后的JSON字符串</returns>
        private string FixJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return json;
            }

            // 步骤1: 找到第一个有效的JSON起始字符
            int startIndex = -1;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '{' || json[i] == '[')
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex < 0)
            {
                return json; // 没有找到JSON起始字符
            }

            // 提取从起始字符开始的内容
            string jsonContent = json.Substring(startIndex).Trim();
            
            // 步骤2: 分析JSON结构，计算各种符号的平衡
            int openBraces = 0;      // { 的数量
            int openBrackets = 0;    // [ 的数量
            bool inString = false;   // 是否在字符串内
            bool escaped = false;    // 是否在转义字符后
            int lastValidPos = -1;    // 最后一个有效字符的位置
            
            for (int i = 0; i < jsonContent.Length; i++)
            {
                char c = jsonContent[i];
                
                if (escaped)
                {
                    escaped = false;
                    lastValidPos = i;
                    continue;
                }
                
                if (c == '\\')
                {
                    escaped = true;
                    lastValidPos = i;
                    continue;
                }
                
                if (c == '"')
                {
                    inString = !inString;
                    lastValidPos = i;
                    continue;
                }
                
                // 只在字符串外计算括号和进行修复
                if (!inString)
                {
                    if (c == '{')
                    {
                        openBraces++;
                        lastValidPos = i;
                    }
                    else if (c == '}')
                    {
                        openBraces--;
                        lastValidPos = i;
                    }
                    else if (c == '[')
                    {
                        openBrackets++;
                        lastValidPos = i;
                    }
                    else if (c == ']')
                    {
                        openBrackets--;
                        lastValidPos = i;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        lastValidPos = i;
                    }
                }
                else
                {
                    // 在字符串内，记录位置
                    lastValidPos = i;
                }
            }
            
            // 步骤3: 截取到最后一个有效位置（移除末尾无效字符）
            string workingJson = jsonContent;
            if (lastValidPos >= 0 && lastValidPos < jsonContent.Length - 1)
            {
                workingJson = jsonContent.Substring(0, lastValidPos + 1);
            }
            
            // 步骤4: 检查并修复字符串引号不平衡
            // 如果字符串没有闭合，尝试闭合它
            bool stillInString = false;
            escaped = false;
            for (int i = 0; i < workingJson.Length; i++)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }
                if (workingJson[i] == '\\')
                {
                    escaped = true;
                    continue;
                }
                if (workingJson[i] == '"')
                {
                    stillInString = !stillInString;
                }
            }
            
            // 如果字符串没有闭合，闭合它
            if (stillInString)
            {
                // 找到最后一个引号的位置，检查是否被转义
                int lastQuoteIndex = workingJson.LastIndexOf('"');
                if (lastQuoteIndex >= 0)
                {
                    // 检查这个引号是否被转义
                    int backslashCount = 0;
                    for (int i = lastQuoteIndex - 1; i >= 0 && workingJson[i] == '\\'; i--)
                    {
                        backslashCount++;
                    }
                    // 如果转义字符数量是偶数，说明这个引号是有效的字符串结束符
                    // 如果转义字符数量是奇数，说明这个引号被转义了，字符串还没结束
                    if (backslashCount % 2 == 0)
                    {
                        // 引号是有效的，但字符串还没闭合，说明后面被截断了
                        // 在末尾添加闭合引号
                        workingJson += "\"";
                    }
                }
            }
            
            // 步骤5: 移除末尾多余的逗号
            string trimmedJson = workingJson.TrimEnd();
            while (trimmedJson.EndsWith(","))
            {
                trimmedJson = trimmedJson.Substring(0, trimmedJson.Length - 1).TrimEnd();
            }
            
            // 步骤6: 补全缺失的闭合括号
            string fixedJson = trimmedJson;
            for (int i = 0; i < openBraces; i++)
            {
                fixedJson += "}";
            }
            for (int i = 0; i < openBrackets; i++)
            {
                fixedJson += "]";
            }
            
            return fixedJson;
        }

        /// <summary>
        /// 处理微信消息（用于获取1112回调）
        /// </summary>
        private void OnWeChatMessageReceived(object? sender, string message)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                // 确保message不为null（编译器警告修复）
                string nonNullMessage = message ?? string.Empty;
                if (string.IsNullOrWhiteSpace(nonNullMessage))
                {
                    return;
                }

                // ========== 全局服务日志：微信消息接收 ==========
                Logger.LogInfo($"========== [全局服务] 收到微信消息 ==========");

                // 先触发通用消息接收事件
                OnMessageReceived?.Invoke(this, nonNullMessage);

                // 清理消息：移除空白和控制字符
                string cleanMessage = nonNullMessage.Trim();
                
                // 清理无效的控制字符（保留JSON必需字符）
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
                    // 输出原始消息的详细信息
                    Logger.LogWarning($"JSON解析失败，尝试修复: {ex.Message}");
                    
                    // 输出完整的原始消息
                    Logger.LogWarning($"原始消息（完整）: {nonNullMessage}");
                    
                    // 输出完整的转义后消息（转义不可见字符）
                    string escapedMessage = System.Text.RegularExpressions.Regex.Replace(
                        nonNullMessage,
                        @"[\x00-\x1F\x7F-\x9F]",
                        m => $"[0x{((int)m.Value[0]):X2}]"
                    );
                    Logger.LogWarning($"原始消息（转义后，完整）: {escapedMessage}");
                    
                    // 策略1: 提取第一个完整的JSON对象
                    int firstBrace = cleanMessage.IndexOf('{');
                    int lastBrace = cleanMessage.LastIndexOf('}');
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        string extractedJson = cleanMessage.Substring(firstBrace, lastBrace - firstBrace + 1);
                        try
                        {
                            messageObj = JsonConvert.DeserializeObject<dynamic>(extractedJson);
                            Logger.LogInfo("通过提取JSON对象成功解析");
                        }
                        catch
                        {
                            // 策略2: 使用智能修复方法
                            try
                            {
                                string fixedJson = FixJsonString(extractedJson);
                                messageObj = JsonConvert.DeserializeObject<dynamic>(fixedJson);
                                Logger.LogInfo("通过智能修复JSON成功解析");
                            }
                            catch (Exception fixEx)
                            {
                                Logger.LogWarning($"智能修复JSON后仍解析失败: {fixEx.Message}，忽略此消息");
                                return;
                            }
                        }
                    }
                    else
                    {
                        // 如果没有找到闭合括号，使用智能修复方法
                        if (firstBrace >= 0)
                        {
                            try
                            {
                                string fixedJson = FixJsonString(cleanMessage);
                                messageObj = JsonConvert.DeserializeObject<dynamic>(fixedJson);
                                Logger.LogInfo("通过智能修复JSON（无闭合括号）成功解析");
                            }
                            catch (Exception fixEx)
                            {
                                Logger.LogWarning($"智能修复JSON后仍解析失败: {fixEx.Message}，忽略此消息");
                                return;
                            }
                        }
                        else
                        {
                            Logger.LogWarning($"无法找到JSON对象起始位置，忽略此消息");
                            return;
                        }
                    }
                }

                if (messageObj == null) return;

                // 获取消息类型
                int messageType = 0;
                if (messageObj.type != null)
                {
                    int.TryParse(messageObj.type.ToString(), out messageType);
                }

                // ========== 全局服务日志：消息类型 ==========
                // 根据消息类型输出相应日志
                switch (messageType)
                {
                    case 1112:
                        // 账号信息回调，在下面处理
                        Logger.LogInfo($"[全局服务] 收到账号信息消息（1112）");
                        break;
                    case 11126:
                        Logger.LogInfo($"[全局服务] 收到联系人列表消息（11126）");
                        break;
                    case 11132:
                        Logger.LogInfo($"[全局服务] 收到文本消息（11132）");
                        break;
                    case 11144:
                        Logger.LogInfo($"[全局服务] 收到朋友圈消息（11144）");
                        break;
                    case 11241:
                        Logger.LogInfo($"[全局服务] 收到标签消息（11241）");
                        break;
                    case 11238:
                        Logger.LogInfo($"[全局服务] 收到其他消息（11238）");
                        break;
                    case 5:
                        Logger.LogInfo($"[全局服务] 收到公众号消息（5）");
                        break;
                    default:
                        if (messageType > 0)
                        {
                            Logger.LogInfo($"[全局服务] 收到未知类型消息: {messageType}");
                        }
                        break;
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
                            lock (_lock)
                            {
                                _currentWxid = wxid;
                            }
                            
                            // ========== 全局服务日志：账号信息解析结果 ==========
                            string nickname = loginInfo.nickname?.ToString() ?? "";
                            string avatar = loginInfo.avatar?.ToString() ?? "";
                            string account = loginInfo.account?.ToString() ?? wxid;
                            
                            // 提取phone字段（支持多种命名方式）
                            string phone = loginInfo.phone?.ToString() ?? "";
                            if (string.IsNullOrEmpty(phone))
                            {
                                phone = loginInfo.Phone?.ToString() ?? "";
                            }
                            
                            // 提取其他字段
                            string deviceId = loginInfo.device_id?.ToString() ?? loginInfo.deviceId?.ToString() ?? "";
                            string wxUserDir = loginInfo.wx_user_dir?.ToString() ?? loginInfo.wxUserDir?.ToString() ?? "";
                            int unreadMsgCount = 0;
                            int.TryParse(loginInfo.unread_msg_count?.ToString() ?? loginInfo.unreadMsgCount?.ToString() ?? "0", out unreadMsgCount);
                            int isFakeDeviceId = 0;
                            int.TryParse(loginInfo.is_fake_device_id?.ToString() ?? loginInfo.isFakeDeviceId?.ToString() ?? "0", out isFakeDeviceId);
                            int pid = 0;
                            int.TryParse(loginInfo.pid?.ToString() ?? "0", out pid);
                            
                            Logger.LogInfo($"[全局服务] ========== 收到微信账号数据（1112回调） ==========");
                            Logger.LogInfo($"[全局服务] wxid: {wxid}");
                            Logger.LogInfo($"[全局服务] nickname: {nickname}");
                            Logger.LogInfo($"[全局服务] avatar: {avatar}");
                            Logger.LogInfo($"[全局服务] account: {account}");
                            Logger.LogInfo($"[全局服务] phone: {phone}");

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

                            // 触发wxid获取事件（保持向后兼容）
                            OnWxidReceived?.Invoke(this, wxid);
                            
                            // 触发账号信息接收事件（包含完整信息）
                            OnAccountInfoReceived?.Invoke(this, accountInfo);
                        }
                        else
                        {
                            Logger.LogWarning($"[全局服务] 收到的wxid无效（可能是进程ID）: {wxid}");
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
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            lock (_lock)
            {
                // 停止定时器
                StopProcessCheckTimer();

                // 断开连接
                Disconnect();

                // 清理事件订阅
                if (_connectionManager != null)
                {
                    _connectionManager.OnConnectionStateChanged -= null;
                    _connectionManager.OnMessageReceived -= null;
                    _connectionManager = null;
                }

                _isDisposed = true;
            }

            Logger.LogInfo("微信管理器已释放");
        }
    }
}

