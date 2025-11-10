using System;
using Newtonsoft.Json;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Models;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 聊天消息同步服务
    /// 负责从微信获取聊天消息并实时同步到服务器
    /// </summary>
    public class ChatMessageSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private Func<string>? _getWeChatIdFunc; // 获取真正的wxid的函数

        // 消息类型定义
        private const int MSG_TYPE_TEXT = 11132; // 文本消息
        private const int MSG_TYPE_VOICE = 11144; // 语音消息

        /// <summary>
        /// 构造函数
        /// </summary>
        public ChatMessageSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 处理聊天消息回调数据
        /// </summary>
        /// <param name="messageType">消息类型</param>
        /// <param name="jsonData">JSON格式的聊天消息数据</param>
        public void ProcessChatMessageCallback(int messageType, string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到聊天消息回调，类型: {messageType}, 数据: {jsonData}");

                // 解析JSON数据
                var chatMessageObj = JsonConvert.DeserializeObject<dynamic>(jsonData);
                if (chatMessageObj == null)
                {
                    Logger.LogWarning("聊天消息数据为空");
                    return;
                }

                // 获取当前登录的微信ID（优先使用真正的wxid）
                string weChatId = _getWeChatIdFunc?.Invoke() ?? _connectionManager?.ClientId.ToString() ?? "";
                if (string.IsNullOrEmpty(weChatId))
                {
                    weChatId = _connectionManager?.ClientId.ToString() ?? "";
                    if (string.IsNullOrEmpty(weChatId))
                    {
                        Logger.LogWarning("无法获取微信ID，跳过消息同步");
                        return;
                    }
                    Logger.LogWarning("未获取到真正的wxid，使用ClientId（进程ID）作为fallback");
                }
                else
                {
                    Logger.LogInfo($"使用真正的wxid进行消息同步: {weChatId}");
                }

                // 根据消息类型处理
                ChatMessage? chatMessage = null;
                
                if (messageType == MSG_TYPE_TEXT)
                {
                    // 文本消息
                    chatMessage = ProcessTextMessage(chatMessageObj, weChatId);
                }
                else if (messageType == MSG_TYPE_VOICE)
                {
                    // 语音消息（暂时跳过，后续可扩展）
                    Logger.LogInfo("收到语音消息，暂不处理");
                    return;
                }

                if (chatMessage != null)
                {
                    // 实时同步到服务器
                    SyncToServer(chatMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理聊天消息回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理文本消息
        /// </summary>
        private ChatMessage? ProcessTextMessage(dynamic chatMessageObj, string weChatId)
        {
            try
            {
                // 根据原项目，文本消息格式为：
                // { msgid, msg, to_wxid, from_wxid, timestamp, room_wxid }
                // 如果 room_wxid 不为空，表示是群聊消息，暂时跳过
                string roomWxid = chatMessageObj.room_wxid?.ToString() ?? "";
                if (!string.IsNullOrEmpty(roomWxid))
                {
                    Logger.LogInfo("收到群聊消息，暂不处理");
                    return null;
                }

                string msgId = chatMessageObj.msgid?.ToString() ?? "";
                string msgText = chatMessageObj.msg?.ToString() ?? "";
                string toWxid = chatMessageObj.to_wxid?.ToString() ?? "";
                string fromWxid = chatMessageObj.from_wxid?.ToString() ?? "";
                
                // 时间戳转换
                long timestamp = 0;
                if (chatMessageObj.timestamp != null)
                {
                    long.TryParse(chatMessageObj.timestamp.ToString(), out timestamp);
                }
                
                DateTime sendTime = DateTime.Now;
                if (timestamp > 0)
                {
                    // 时间戳通常是秒级，转换为DateTime
                    DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    sendTime = epoch.AddSeconds(timestamp).ToLocalTime();
                }

                // 判断发送类型：如果 from_wxid == weChatId，表示自己发送的；否则是接收的
                int sendType = (fromWxid == weChatId) ? 1 : 0;

                // 处理特殊字符
                if (msgText.Contains("\\u0"))
                {
                    msgText = msgText.Replace("\\u0", "");
                }

                var chatMessage = new ChatMessage
                {
                    MsgId = msgId,
                    MsgText = msgText,
                    ReceiveWxId = toWxid,
                    SendWxId = fromWxid,
                    SendType = sendType,
                    ClientId = _connectionManager?.ClientId ?? 0,
                    SendTime = sendTime
                };

                return chatMessage;
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理文本消息失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 同步聊天消息到服务器
        /// </summary>
        private void SyncToServer(ChatMessage chatMessage)
        {
            try
            {
                Logger.LogInfo($"开始同步聊天消息到服务器: {chatMessage.MsgId}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_chat_message",
                    data = chatMessage
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                Logger.LogInfo("聊天消息同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步聊天消息到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

