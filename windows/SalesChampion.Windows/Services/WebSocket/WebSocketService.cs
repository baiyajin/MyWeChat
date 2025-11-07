using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SalesChampion.Windows.Utils;

namespace SalesChampion.Windows.Services.WebSocket
{
    /// <summary>
    /// WebSocket客户端服务
    /// 负责与服务器建立WebSocket连接，接收App端命令
    /// </summary>
    public class WebSocketService
    {
        private ClientWebSocket _webSocket;
        private readonly string _serverUrl;
        private bool _isConnected;
        private CancellationTokenSource _cancellationTokenSource;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsConnected => _isConnected && _webSocket != null && _webSocket.State == WebSocketState.Open;

        /// <summary>
        /// 收到消息事件
        /// </summary>
        public event EventHandler<string> OnMessageReceived;

        /// <summary>
        /// 连接状态变化事件
        /// </summary>
        public event EventHandler<bool> OnConnectionStateChanged;

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

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                await _webSocket.ConnectAsync(new Uri(_serverUrl), _cancellationTokenSource.Token);

                _isConnected = true;
                OnConnectionStateChanged?.Invoke(this, true);
                Logger.LogInfo("WebSocket连接成功");

                // 发送客户端类型
                await SendMessageAsync(new
                {
                    type = "client_type",
                    client_type = "windows"
                });

                // 启动接收消息任务
                _ = Task.Run(ReceiveMessagesAsync);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"WebSocket连接失败: {ex.Message}", ex);
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
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "关闭连接", CancellationToken.None);
                }

                _cancellationTokenSource?.Cancel();
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
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                Logger.LogInfo($"WebSocket消息已发送: {message}");
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
        /// </summary>
        public bool SendMessage(string message)
        {
            try
            {
                return SendMessageAsync(message).Result;
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

            while (_isConnected && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "服务器关闭连接", CancellationToken.None);
                        _isConnected = false;
                        OnConnectionStateChanged?.Invoke(this, false);
                        break;
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Logger.LogInfo($"WebSocket收到消息: {message}");
                        OnMessageReceived?.Invoke(this, message);
                    }
                }
                catch (OperationCanceledException)
                {
                    // 正常取消
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError($"接收WebSocket消息失败: {ex.Message}", ex);
                    _isConnected = false;
                    OnConnectionStateChanged?.Invoke(this, false);
                    break;
                }
            }
        }
    }
}
