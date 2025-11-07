namespace SalesChampion.Windows.Models
{
    /// <summary>
    /// 标签信息模型
    /// 对应微信好友标签
    /// </summary>
    public class TagInfo
    {
        /// <summary>
        /// 标签ID
        /// </summary>
        public string TagId { get; set; }

        /// <summary>
        /// 标签名称
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// 微信ID（当前登录账号）
        /// </summary>
        public string WeChatId { get; set; }
    }
}

