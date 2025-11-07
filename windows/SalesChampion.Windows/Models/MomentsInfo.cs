namespace SalesChampion.Windows.Models
{
    /// <summary>
    /// 朋友圈信息模型
    /// 对应微信朋友圈内容
    /// </summary>
    public class MomentsInfo
    {
        /// <summary>
        /// 朋友圈ID
        /// </summary>
        public string MomentId { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 好友ID（发布者微信ID）
        /// </summary>
        public string FriendId { get; set; }

        /// <summary>
        /// 朋友圈内容
        /// </summary>
        public string Moments { get; set; }

        /// <summary>
        /// 微信ID（当前登录账号）
        /// </summary>
        public string WeChatId { get; set; }

        /// <summary>
        /// 发布时间
        /// </summary>
        public string ReleaseTime { get; set; }

        /// <summary>
        /// 类型（0-文本，1-图片，2-视频等）
        /// </summary>
        public int Type { get; set; }

        /// <summary>
        /// JSON文本（包含图片、视频等详细信息）
        /// </summary>
        public string JsonText { get; set; }
    }
}

