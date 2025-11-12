using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using MyWeChat.Windows.Core.Connection;
using MyWeChat.Windows.Services.WebSocket;
using MyWeChat.Windows.Utils;

namespace MyWeChat.Windows.Services
{
    /// <summary>
    /// 公众号消息同步服务
    /// 负责从微信获取公众号推送消息并实时同步到服务器
    /// </summary>
    public class OfficialAccountSyncService
    {
        private readonly WeChatConnectionManager _connectionManager;
        private readonly WebSocketService _webSocketService;
        private Func<string>? _getWeChatIdFunc;

        /// <summary>
        /// 构造函数
        /// </summary>
        public OfficialAccountSyncService(WeChatConnectionManager connectionManager, WebSocketService webSocketService, Func<string>? getWeChatIdFunc = null)
        {
            _connectionManager = connectionManager;
            _webSocketService = webSocketService;
            _getWeChatIdFunc = getWeChatIdFunc;
        }

        /// <summary>
        /// 处理公众号消息回调数据
        /// </summary>
        /// <param name="jsonData">JSON格式的公众号消息数据</param>
        public void ProcessOfficialAccountCallback(string jsonData)
        {
            try
            {
                Logger.LogInfo($"收到公众号消息回调，数据: {jsonData}");

                // 解析JSON数据
                var messageObj = JsonConvert.DeserializeObject<dynamic>(jsonData);
                if (messageObj == null)
                {
                    Logger.LogWarning("公众号消息数据为空");
                    return;
                }

                // 获取当前登录的微信ID
                string weChatId = _getWeChatIdFunc?.Invoke() ?? _connectionManager?.ClientId.ToString() ?? "";
                if (string.IsNullOrEmpty(weChatId))
                {
                    Logger.LogWarning("无法获取微信ID，跳过公众号消息同步");
                    return;
                }

                // 获取raw_msg（XML格式）
                string rawMsg = messageObj.raw_msg?.ToString() ?? "";
                if (string.IsNullOrEmpty(rawMsg))
                {
                    Logger.LogWarning("公众号消息raw_msg为空");
                    return;
                }

                // 解析XML格式的raw_msg
                var officialAccountData = ParseRawMsg(rawMsg);
                if (officialAccountData == null)
                {
                    Logger.LogWarning("解析公众号消息XML失败");
                    return;
                }

                // 添加消息基本信息
                officialAccountData["from_wxid"] = messageObj.from_wxid?.ToString() ?? "";
                officialAccountData["msgid"] = messageObj.msgid?.ToString() ?? "";
                officialAccountData["wechat_id"] = weChatId;

                // 实时同步到服务器
                SyncToServer(officialAccountData);
            }
            catch (Exception ex)
            {
                Logger.LogError($"处理公众号消息回调失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 解析XML格式的raw_msg
        /// </summary>
        private Dictionary<string, object>? ParseRawMsg(string rawMsg)
        {
            try
            {
                // 解析XML
                XDocument doc = XDocument.Parse(rawMsg);
                XElement? appmsg = doc.Descendants("appmsg").FirstOrDefault();
                if (appmsg == null)
                {
                    Logger.LogWarning("未找到appmsg节点");
                    return null;
                }

                // 检查type是否为5（公众号消息）
                string type = appmsg.Element("type")?.Value ?? "";
                if (type != "5")
                {
                    Logger.LogInfo($"消息type不是5，跳过处理。type: {type}");
                    return null;
                }

                // 获取mmreader节点
                XElement? mmreader = appmsg.Element("mmreader");
                if (mmreader == null)
                {
                    Logger.LogWarning("未找到mmreader节点");
                    return null;
                }

                // 获取category节点
                XElement? category = mmreader.Element("category");
                if (category == null)
                {
                    Logger.LogWarning("未找到category节点");
                    return null;
                }

                // 获取公众号名称
                string accountName = category.Element("name")?.Value ?? "";

                // 获取publisher节点（公众号信息）
                XElement? publisher = mmreader.Element("publisher");
                string publisherUsername = publisher?.Element("username")?.Value ?? "";
                string publisherNickname = publisher?.Element("nickname")?.Value ?? accountName;

                // 获取文章列表
                var articles = new List<Dictionary<string, object>>();
                var items = category.Elements("item");
                
                foreach (var item in items)
                {
                    var article = new Dictionary<string, object>
                    {
                        ["title"] = item.Element("title")?.Value ?? item.Element("title_v2")?.Value ?? "",
                        ["url"] = item.Element("url")?.Value ?? "",
                        ["cover"] = item.Element("cover")?.Value ?? item.Element("cover_1_1")?.Value ?? "",
                        ["summary"] = item.Element("summary")?.Value ?? "",
                        ["pub_time"] = item.Element("pub_time")?.Value ?? "0"
                    };
                    articles.Add(article);
                }

                // 构建返回数据
                var result = new Dictionary<string, object>
                {
                    ["account_name"] = accountName,
                    ["publisher_username"] = publisherUsername,
                    ["publisher_nickname"] = publisherNickname,
                    ["articles"] = articles,
                    ["receive_time"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };

                Logger.LogInfo($"解析公众号消息成功，公众号: {accountName}, 文章数: {articles.Count}");
                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError($"解析公众号消息XML失败: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// 同步公众号消息到服务器
        /// </summary>
        private void SyncToServer(Dictionary<string, object> officialAccountData)
        {
            try
            {
                Logger.LogInfo($"开始同步公众号消息到服务器: {officialAccountData["account_name"]}");

                // 通过WebSocket发送到服务器
                var syncData = new
                {
                    type = "sync_official_account",
                    data = officialAccountData
                };

                _ = _webSocketService.SendMessageAsync(syncData);

                Logger.LogInfo("公众号消息同步完成");
            }
            catch (Exception ex)
            {
                Logger.LogError($"同步公众号消息到服务器失败: {ex.Message}", ex);
            }
        }
    }
}

