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
        public string Client { get; set; }

        /// <summary>
        /// 公司名称
        /// </summary>
        public string CompanyName { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 当前绑定账号
        /// </summary>
        public string BoundAccount { get; set; }

        /// <summary>
        /// 微信ID
        /// </summary>
        public string WeChatId { get; set; }

        /// <summary>
        /// 头像URL
        /// </summary>
        public string Avatar { get; set; }
    }
}

