namespace SalesChampion.Windows.Models
{
    /// <summary>
    /// 联系人信息模型
    /// 对应微信好友信息
    /// </summary>
    public class ContactInfo
    {
        /// <summary>
        /// ID（用于App端显示）
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 头像URL
        /// </summary>
        public string Avatar { get; set; }

        /// <summary>
        /// 城市
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 国家
        /// </summary>
        public string Country { get; set; }

        /// <summary>
        /// 标签ID列表（逗号分隔）
        /// </summary>
        public string LabelIds { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 省份
        /// </summary>
        public string Province { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 性别（0-未知，1-男，2-女）
        /// </summary>
        public int Sex { get; set; }

        /// <summary>
        /// 好友ID（微信ID）
        /// </summary>
        public string FriendId { get; set; }

        /// <summary>
        /// 好友编号
        /// </summary>
        public string FriendNo { get; set; }

        /// <summary>
        /// 微信ID（当前登录账号）
        /// </summary>
        public string WeChatId { get; set; }

        /// <summary>
        /// 是否新好友（"1"-是，"0"-否）
        /// </summary>
        public string IsNewFriend { get; set; }
    }
}

