using System;

namespace MyWeChat.Windows.Models
{
    /// <summary>
    /// 聊天消息模型
    /// 对应微信聊天消息
    /// </summary>
    public class ChatMessage
    {
        /// <summary>
        /// 消息ID
        /// </summary>
        public string MsgId { get; set; } = string.Empty;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string MsgText { get; set; } = string.Empty;

        /// <summary>
        /// 接收者微信ID
        /// </summary>
        public string ReceiveWxId { get; set; } = string.Empty;

        /// <summary>
        /// 发送者微信ID
        /// </summary>
        public string SendWxId { get; set; } = string.Empty;

        /// <summary>
        /// 发送类型（0-接收，1-发送）
        /// </summary>
        public int SendType { get; set; }

        /// <summary>
        /// 客户端ID
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// 发送时间
        /// </summary>
        public DateTime SendTime { get; set; }
    }
}

