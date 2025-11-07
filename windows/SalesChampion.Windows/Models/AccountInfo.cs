namespace SalesChampion.Windows.Models
{
    /// <summary>
    /// 账号信息模型
    /// 对应已登录的微信账号信息
    /// </summary>
    public class AccountInfo
    {
        /// <summary>
        /// 客户端ID
        /// </summary>
        public string Client { get; set; } = string.Empty;

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
    }
}

