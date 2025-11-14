using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MyWeChat.Windows.Utils;
using Newtonsoft.Json;

namespace MyWeChat.Windows.Services.WebSocket
{
    /// <summary>
    /// WebSocket客户端服务
    /// 负责与服务器建立WebSocket连接，接收App端命令
    /// </summary>
    public class WebSocketService : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly string _serverUrl;
        private bool _isConnected;
        private CancellationTokenSource? _cancellationTokenSource;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected => _isConnected && _webSocket != null && _webSocket.State == WebSocketState.Open;

        /// <summary>
        /// 收到消息事件
        /// </summary>
        public event EventHandler<string>? OnMessageReceived;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool>? OnConnectionStateChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="serverUrl">服务器WebSocket地址，如"ws://localhost:8000/ws"</param>
        public WebSocketService(string serverUrl)
        {
            _serverUrl = serverUrl;
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                if (_isConnected)
                {
                    Logger.LogWarning("WebSocket已连接，无需重复连接");
                    return true;
                }

                Logger.LogInfo($"正在连接WebSocket服务器: {_serverUrl}");

                // 验证服务器地址格式
                if (string.IsNullOrEmpty(_serverUrl))
                {
                    string errorMsg = "WebSocket服务器地址未配置";
                    Logger.LogError(errorMsg);
                    Logger.LogError("请在 App.config 中配置 WebSocketUrl，格式: ws://localhost:8000/ws");
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    return false;
                }

                if (!Uri.TryCreate(_serverUrl, UriKind.Absolute, out Uri? uri) || 
                    (uri.Scheme != "ws" && uri.Scheme != "wss"))
                {
                    string errorMsg = $"WebSocket服务器地址格式错误: {_serverUrl}";
                    Logger.LogError(errorMsg);
                    Logger.LogError("正确的格式应该是: ws://localhost:8000/ws 或 wss://example.com/ws");
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    return false;
                }

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // 设置连接超时（10秒）
                using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                {
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutCts.Token))
                    {
                        try
                        {
                            await _webSocket.ConnectAsync(uri, linkedCts.Token);
                        }
                        catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested)
                        {
                            string errorMsg = $"WebSocket连接超时（10秒），无法连接到服务器: {_serverUrl}";
                            Logger.LogError(errorMsg);
                            Logger.LogError("可能的原因：");
                            Logger.LogError("  1. WebSocket服务器未启动");
                            Logger.LogError("  2. 服务器地址配置错误");
                            Logger.LogError("  3. 防火墙阻止连接");
                            Logger.LogError("  4. 网络连接问题");
                            Logger.LogError($"请检查服务器是否运行在: {_serverUrl}");
                            _isConnected = false;
                            OnConnectionStateChanged?.Invoke(this, false);
                            return false;
                        }
                    }
                }

                _isConnected = true;
                OnConnectionStateChanged?.Invoke(this, true);
                Logger.LogInfo("WebSocket连接成功");

                // 启动接收消息任务（必须在密钥交换之前启动，以接收服务器公钥）
                _ = Task.Run(ReceiveMessagesAsync);

                // 等待一小段时间，确保接收任务已启动
                await Task.Delay(100);

                // 发送客户端类型（明文，密钥交换前）
                await SendMessageAsyncPlain(new
                {
                    type = "client_type",
                    client_type = "windows"
                });

                return true;
            }
            catch (System.Net.Sockets.SocketException socketEx)
            {
                string errorMsg = $"WebSocket连接失败（网络错误）: {socketEx.Message}";
                Logger.LogError(errorMsg);
                Logger.LogError($"服务器地址: {_serverUrl}");
                Logger.LogError("可能的原因：");
                Logger.LogError("  1. WebSocket服务器未启动");
                Logger.LogError("  2. 服务器地址配置错误");
                Logger.LogError("  3. 防火墙阻止连接");
                Logger.LogError("  4. 网络连接问题");
                Logger.LogError($"请检查服务器是否运行在: {_serverUrl}");
                _isConnected = false;
                OnConnectionStateChanged?.Invoke(this, false);
                return false;
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                string errorMsg = $"WebSocket连接失败（HTTP错误）: {httpEx.Message}";
                Logger.LogError(errorMsg);
                Logger.LogError($"服务器地址: {_serverUrl}");
                Logger.LogError("可能的原因：");
                Logger.LogError("  1. WebSocket服务器未启动");
                Logger.LogError("  2. 服务器地址配置错误");
                Logger.LogError("  3. 服务器不支持WebSocket协议");
                Logger.LogError($"请检查服务器是否运行在: {_serverUrl}");
                _isConnected = false;
                OnConnectionStateChanged?.Invoke(this, false);
                return false;
            }
            catch (Exception ex)
            {
                string errorMsg = $"WebSocket连接失败: {ex.Message}";
                Logger.LogError(errorMsg);
                Logger.LogError($"异常类型: {ex.GetType().Name}");
                Logger.LogError($"服务器地址: {_serverUrl}");
                Logger.LogError("可能的原因：");
                Logger.LogError("  1. WebSocket服务器未启动");
                Logger.LogError("  2. 服务器地址配置错误");
                Logger.LogError("  3. 防火墙阻止连接");
                Logger.LogError("  4. 网络连接问题");
                Logger.LogError($"请检查服务器是否运行在: {_serverUrl}");
                _isConnected = false;
                OnConnectionStateChanged?.Invoke(this, false);
                return false;
            }
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                // 取消接收消息任务
                _cancellationTokenSource?.Cancel();
                
                // 只有在连接仍然打开时才尝试关闭
                if (_webSocket != null)
                {
                    var state = _webSocket.State;
                    if (state == WebSocketState.Open || state == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "关闭连接", CancellationToken.None);
                        }
                        catch (Exception closeEx)
                        {
                            // 如果关闭失败（可能已经关闭），记录但不抛出异常
                            Logger.LogInfo($"WebSocket关闭时状态异常（可能已关闭）: {state}, 错误: {closeEx.Message}");
                        }
                    }
                    // 注意：如果WebSocket已处于关闭状态，不再输出日志，因为下面会统一输出"WebSocket已断开连接"
                }

                _isConnected = false;
                OnConnectionStateChanged?.Invoke(this, false);

                Logger.LogInfo("WebSocket已断开连接");
            }
            catch (Exception ex)
            {
                Logger.LogError($"断开WebSocket连接失败: {ex.Message}", ex);
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 发送消息（对象）
        /// </summary>
        public async Task<bool> SendMessageAsync(object message)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
            return await SendMessageAsync(json);
        }

        /// <summary>
        /// 发送明文消息（用于密钥交换阶段）
        /// </summary>
        private async Task<bool> SendMessageAsyncPlain(object message)
        {
            if (!IsConnected)
            {
                Logger.LogWarning("WebSocket未连接，无法发送消息");
                return false;
            }

            try
            {
                if (_webSocket == null)
                {
                    Logger.LogWarning("WebSocket未初始化");
                    return false;
                }

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(message);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送WebSocket明文消息失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 发送消息（字符串）
        /// </summary>
        public async Task<bool> SendMessageAsync(string message)
        {
            if (!IsConnected)
            {
                Logger.LogWarning("WebSocket未连接，无法发送消息");
                return false;
            }

            try
            {
                if (_webSocket == null)
                {
                    Logger.LogWarning("WebSocket未初始化");
                    return false;
                }

                // 如果会话密钥已设置，使用会话密钥加密；否则发送明文（用于密钥交换）
                if (EncryptionService.HasSessionKey())
                {
                    // 加密消息内容（使用会话密钥）
                    string encryptedMessage;
                    try
                    {
                        encryptedMessage = EncryptionService.EncryptStringForCommunication(message);
                    }
                    catch (Exception encryptEx)
                    {
                        Logger.LogError($"WebSocket消息加密失败: {encryptEx.Message}", encryptEx);
                        return false;
                    }

                    // 包装为JSON格式，包含加密标识
                    var messageWrapper = new
                    {
                        encrypted = true,
                        data = encryptedMessage
                    };
                    string jsonMessage = Newtonsoft.Json.JsonConvert.SerializeObject(messageWrapper);

                    byte[] buffer = Encoding.UTF8.GetBytes(jsonMessage);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    Logger.LogInfo("WebSocket消息已发送（已加密）");
                }
                else
                {
                    // 会话密钥未设置，发送明文（用于密钥交换阶段）
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                    Logger.LogInfo("WebSocket消息已发送（明文，密钥交换阶段）");
                }
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送WebSocket消息失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 发送消息（同步方法）
        /// 注意：此方法可能导致死锁，建议使用异步方法 SendMessageAsync
        /// </summary>
        public bool SendMessage(string message)
        {
            try
            {
                // 使用 ConfigureAwait(false) 避免死锁，但仍然可能阻塞调用线程
                // 建议在后台线程调用此方法，或直接使用 SendMessageAsync
                return SendMessageAsync(message).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogError($"发送WebSocket消息失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 接收消息（异步任务）
        /// </summary>
        private async Task ReceiveMessagesAsync()
        {
            byte[] buffer = new byte[4096];

            while (_isConnected && _webSocket?.State == WebSocketState.Open)
            {
                try
                {
                    if (_webSocket == null || _cancellationTokenSource == null)
                    {
                        break;
                    }
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // 只有在连接仍然打开时才尝试关闭
                        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                        {
                            try
                            {
                                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭连接", CancellationToken.None);
                            }
                            catch (Exception closeEx)
                            {
                                // 如果关闭失败（可能已经关闭），记录但不抛出异常
                                Logger.LogInfo($"WebSocket关闭时已处于关闭状态: {closeEx.Message}");
                            }
                        }
                        _isConnected = false;
                        OnConnectionStateChanged?.Invoke(this, false);
                        Logger.LogInfo("WebSocket连接已关闭（服务器发送关闭消息）");
                        break;
                    }
                    else
                    {
                        string rawMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        
                        // 处理密钥交换消息
                        bool handled = await HandleKeyExchangeMessage(rawMessage);
                        if (handled)
                        {
                            // 密钥交换消息已处理，不触发OnMessageReceived
                            continue;
                        }
                        
                        // 尝试解密消息
                        string decryptedMessage;
                        try
                        {
                            // 尝试解析为JSON格式（可能包含加密标识）
                            var messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(rawMessage);
                            if (messageObj != null)
                            {
                                bool? isEncrypted = messageObj.encrypted as bool?;
                                if (isEncrypted == true && messageObj.data != null)
                                {
                                    // 加密消息，需要解密（使用会话密钥）
                                    string? encryptedData = messageObj.data?.ToString();
                                    if (string.IsNullOrEmpty(encryptedData))
                                    {
                                        decryptedMessage = rawMessage; // 如果 data 为空，使用原始消息
                                    }
                                    else
                                    {
                                        decryptedMessage = EncryptionService.DecryptStringForCommunication(encryptedData);
                                    }
                                }
                                else
                                {
                                    // 非加密消息或格式不正确，直接使用原始消息
                                    decryptedMessage = rawMessage;
                                }
                            }
                            else
                            {
                                // messageObj 为 null，使用原始消息
                                decryptedMessage = rawMessage;
                            }
                        }
                        catch
                        {
                            // 解析失败，可能是非JSON格式的明文消息，直接使用
                            decryptedMessage = rawMessage;
                        }

                        Logger.LogInfo("WebSocket收到消息（已解密）");
                        OnMessageReceived?.Invoke(this, decryptedMessage);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，不再输出日志（这是正常的关闭流程）
                    break;
                }
                catch (WebSocketException wsEx)
                {
                    // WebSocket特定异常，检查连接状态
                    var currentState = _webSocket?.State ?? WebSocketState.None;
                    if (currentState == WebSocketState.Aborted || 
                        currentState == WebSocketState.Closed ||
                        wsEx.Message.Contains("closed") || 
                        wsEx.Message.Contains("close handshake") ||
                        wsEx.Message.Contains("invalid state"))
                    {
                        // 连接已关闭，这是正常情况
                        Logger.LogInfo($"WebSocket连接已关闭: {wsEx.Message} (状态: {currentState})");
                    }
                    else
                    {
                        // 其他WebSocket错误
                        Logger.LogError($"WebSocket错误: {wsEx.Message} (状态: {currentState})", wsEx);
                    }
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    break;
                }
                catch (Exception ex)
                {
                    // 检查是否是连接关闭相关的异常
                    var currentState = _webSocket?.State ?? WebSocketState.None;
                    if (ex.Message.Contains("closed") || 
                        ex.Message.Contains("close handshake") ||
                        ex.Message.Contains("The remote party closed") ||
                        ex.Message.Contains("invalid state") ||
                        currentState == WebSocketState.Closed ||
                        currentState == WebSocketState.Aborted)
                    {
                        // 连接关闭，这是正常情况
                        Logger.LogInfo($"WebSocket连接已关闭: {ex.Message} (状态: {currentState})");
                    }
                    else
                    {
                        // 其他错误
                        Logger.LogError($"接收WebSocket消息失败: {ex.Message} (状态: {currentState})", ex);
                    }
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    break;
                }
            }
            
            // 确保连接状态已更新
            if (_isConnected)
            {
                _isConnected = false;
                OnConnectionStateChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// 处理密钥交换消息
        /// </summary>
        private async Task<bool> HandleKeyExchangeMessage(string rawMessage)
        {
            try
            {
                var messageObj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(rawMessage);
                if (messageObj == null)
                {
                    return false;
                }

                string? messageType = messageObj.type?.ToString();

                // 处理RSA公钥消息
                if (messageType == "rsa_public_key")
                {
                    string? publicKeyPem = messageObj.public_key?.ToString();
                    if (!string.IsNullOrEmpty(publicKeyPem))
                    {
                        // 设置服务器公钥
                        RSAKeyManager.SetServerPublicKey(publicKeyPem);
                        Logger.LogInfo("已接收服务器RSA公钥");

                        // 生成随机会话密钥（32字节，256位）
                        byte[] sessionKey = new byte[32];
                        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
                        {
                            rng.GetBytes(sessionKey);
                        }

                        // 使用RSA公钥加密会话密钥
                        string encryptedSessionKey = RSAKeyManager.EncryptSessionKey(sessionKey);

                        // 设置会话密钥
                        EncryptionService.SetSessionKey(sessionKey);

                        // 发送加密的会话密钥给服务器
                        await SendMessageAsyncPlain(new
                        {
                            type = "session_key",
                            encrypted_key = encryptedSessionKey
                        });

                        Logger.LogInfo("已发送加密的会话密钥给服务器");
                        return true;
                    }
                }

                // 处理密钥交换成功消息
                if (messageType == "key_exchange_success")
                {
                    Logger.LogInfo("密钥交换成功，后续通讯将使用会话密钥加密");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理密钥交换消息失败: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (_isConnected)
                {
                    _ = DisconnectAsync();
                }
                else
                {
                    _webSocket?.Dispose();
                    _webSocket = null;
                    _cancellationTokenSource?.Dispose();
                    _cancellationTokenSource = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"释放WebSocket资源失败: {ex.Message}", ex);
            }
        }
    }
}
