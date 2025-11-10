namespace MyWeChat.Windows.Models
{
    /// <summary>
    /// 命令信息模型
    /// 用于接收App端发送的命令
    /// </summary>
    public class CommandInfo
    {
        /// <summary>
        /// 命令类型
        /// </summary>
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// 命令数据（JSON格式）
        /// </summary>
        public string CommandData { get; set; } = string.Empty;

        /// <summary>
        /// 目标微信ID
        /// </summary>
        public string TargetWeChatId { get; set; } = string.Empty;

        /// <summary>
        /// 命令ID（用于追踪命令执行结果）
        /// </summary>
        public string CommandId { get; set; } = string.Empty;
    }
}

