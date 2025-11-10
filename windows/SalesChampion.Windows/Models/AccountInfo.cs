namespace MyWeChat.Windows.Models
{
    /// <summary>
    /// 账号信息模型
    /// 对应已登录的微信账号信息
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// 公司名称
        /// </summary>
        public string CompanyName { get; set; } = string.Empty;

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; } = string.Empty;

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 当前绑定账号
        /// </summary>
        public string BoundAccount { get; set; } = string.Empty;

        /// <summary>
        /// 微信ID
        /// </summary>
        public string WeChatId { get; set; } = string.Empty;

        /// <summary>
        /// 头像URL
        /// </summary>
        public string Avatar { get; set; } = string.Empty;

        /// <summary>
        /// 设备ID
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 手机号
        /// </summary>
        public string Phone { get; set; } = string.Empty;

        /// <summary>
        /// 微信用户目录
        /// </summary>
        public string WxUserDir { get; set; } = string.Empty;

        /// <summary>
        /// 未读消息数
        /// </summary>
        public int UnreadMsgCount { get; set; } = 0;

        /// <summary>
        /// 是否为假设备ID
        /// </summary>
        public int IsFakeDeviceId { get; set; } = 0;

        /// <summary>
        /// 进程ID
        /// </summary>
        public int Pid { get; set; } = 0;
    }
}

